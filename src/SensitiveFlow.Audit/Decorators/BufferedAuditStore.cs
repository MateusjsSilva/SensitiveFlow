using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Decorators;

/// <summary>
/// Buffers audit appends through a bounded in-process queue and flushes them on a background worker.
/// </summary>
/// <remarks>
/// Use this decorator only when the application can tolerate the operational trade-offs of
/// in-process buffering. A crash can lose records that were accepted by the buffer but not yet
/// flushed to the durable store. Query operations are delegated directly to the inner store and
/// may not include records still waiting in the buffer.
/// </remarks>
public sealed class BufferedAuditStore : IBatchAuditStore, IAsyncDisposable, IDisposable
{
    private static readonly Meter Meter = new(SensitiveFlowDiagnostics.MeterName);

    private static readonly Counter<long> DroppedItemsCounter = Meter.CreateCounter<long>(
        name: SensitiveFlowDiagnostics.BufferDroppedItemsName,
        unit: "items",
        description: "Audit records dropped due to buffer overflow or failure.");

    private static readonly Counter<long> FlushFailuresCounter = Meter.CreateCounter<long>(
        name: SensitiveFlowDiagnostics.BufferFlushFailuresName,
        unit: "failures",
        description: "Flush failures in the background worker.");

    private readonly IAuditStore _inner;
    private readonly BufferedAuditStoreOptions _options;
    private readonly ILogger<BufferedAuditStore>? _logger;
    private readonly Channel<AuditRecord> _channel;
    private readonly Task _worker;
    private Exception? _backgroundFailure;
    private long _droppedCount;
    private long _flushFailureCount;

    /// <summary>Initializes a new instance of <see cref="BufferedAuditStore"/>.</summary>
    public BufferedAuditStore(
        IAuditStore inner,
        BufferedAuditStoreOptions? options = null,
        ILogger<BufferedAuditStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        _options = options ?? new BufferedAuditStoreOptions();
        _logger = logger;

        if (_options.Capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Buffer capacity must be greater than zero.");
        }

        if (_options.MaxBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Max batch size must be greater than zero.");
        }

        _channel = Channel.CreateBounded<AuditRecord>(new BoundedChannelOptions(_options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

        // Register the observable gauge callback so OpenTelemetry can poll pending count.
        Meter.CreateObservableGauge(
            name: SensitiveFlowDiagnostics.BufferPendingItemsName,
            observeValue: () => new Measurement<long>(_channel.Reader.Count),
            unit: "items",
            description: "Audit records currently waiting in the buffer.");

        _worker = Task.Run(ProcessAsync);
    }

    /// <summary>
    /// Returns a snapshot of the buffer's health: pending items, dropped count, flush failure count,
    /// and whether the background worker has failed.
    /// </summary>
    public BufferedAuditStoreHealth GetHealth() => new(
        PendingItems: _channel.Reader.Count,
        DroppedItems: Interlocked.Read(ref _droppedCount),
        FlushFailures: Interlocked.Read(ref _flushFailureCount),
        IsFaulted: _backgroundFailure is not null,
        BackgroundFailure: _backgroundFailure?.Message);

    /// <inheritdoc />
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ThrowIfBackgroundFailed();

        try
        {
            await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            Interlocked.Increment(ref _droppedCount);
            DroppedItemsCounter.Add(1);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AppendRangeAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        ThrowIfBackgroundFailed();

        foreach (var record in records)
        {
            try
            {
                await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                Interlocked.Increment(ref _droppedCount);
                DroppedItemsCounter.Add(1);
                throw;
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _inner.QueryAsync(from, to, skip, take, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _inner.QueryByDataSubjectAsync(dataSubjectId, from, to, skip, take, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
        => _inner.QueryAsync(query, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<AuditRecord> QueryStreamAsync(AuditQuery query, CancellationToken cancellationToken = default)
        => _inner.QueryStreamAsync(query, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Prefer <c>await using</c> / <see cref="DisposeAsync"/>. This synchronous overload
    /// signals the worker to complete and waits up to <see cref="BufferedAuditStoreOptions.ShutdownTimeout"/>
    /// for the pending batch to flush. Records still in flight after the timeout are abandoned
    /// (counted in <see cref="GetHealth"/>.<see cref="BufferedAuditStoreHealth.DroppedItems"/>);
    /// this overload never blocks indefinitely so it is safe in synchronous shutdown paths.
    /// </remarks>
    public void Dispose()
    {
        _channel.Writer.TryComplete();
        if (!_worker.Wait(_options.ShutdownTimeout))
        {
            // Worker did not finish in time. Count the still-pending records as dropped
            // so health/metrics reflect the loss instead of silently swallowing them.
            var abandoned = _channel.Reader.Count;
            if (abandoned > 0)
            {
                Interlocked.Add(ref _droppedCount, abandoned);
                DroppedItemsCounter.Add(abandoned);
                _logger?.LogWarning(
                    "BufferedAuditStore.Dispose timed out after {Timeout}; {Abandoned} records were not flushed.",
                    _options.ShutdownTimeout, abandoned);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }

    private async Task ProcessAsync()
    {
        var batch = new List<AuditRecord>(_options.MaxBatchSize);

        try
        {
            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var record))
                {
                    batch.Add(record);

                    if (batch.Count >= _options.MaxBatchSize)
                    {
                        await FlushAsync(batch).ConfigureAwait(false);
                    }
                }

                if (batch.Count > 0)
                {
                    await FlushAsync(batch).ConfigureAwait(false);
                }
            }

            // Channel was completed. Drain any records that landed between WaitToReadAsync
            // returning false and TryComplete (the channel guarantees they remain readable
            // after Complete until drained).
            while (_channel.Reader.TryRead(out var record))
            {
                batch.Add(record);
            }
            if (batch.Count > 0)
            {
                await FlushAsync(batch).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _backgroundFailure = ex;
            Interlocked.Increment(ref _flushFailureCount);
            FlushFailuresCounter.Add(1);
            _logger?.LogError(ex, "Buffered audit store background flush failed.");
            _channel.Writer.TryComplete(ex);
        }
    }

    private async Task FlushAsync(List<AuditRecord> batch)
    {
        if (_inner is IBatchAuditStore batchStore)
        {
            await batchStore.AppendRangeAsync(batch).ConfigureAwait(false);
        }
        else
        {
            foreach (var record in batch)
            {
                await _inner.AppendAsync(record).ConfigureAwait(false);
            }
        }

        batch.Clear();
    }

    private void ThrowIfBackgroundFailed()
    {
        if (_backgroundFailure is not null)
        {
            throw new InvalidOperationException("Buffered audit store background flush has failed.", _backgroundFailure);
        }
    }
}

/// <summary>Options controlling <see cref="BufferedAuditStore"/> queueing behavior.</summary>
public sealed class BufferedAuditStoreOptions
{
    /// <summary>Maximum number of audit records accepted into the in-process queue. Default <c>1024</c>.</summary>
    public int Capacity { get; set; } = 1024;

    /// <summary>Maximum number of queued audit records flushed to the inner store at once. Default <c>100</c>.</summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum time the synchronous <see cref="BufferedAuditStore.Dispose"/> waits for the
    /// background worker to drain. Records still in flight after this timeout are counted as
    /// dropped instead of blocking shutdown indefinitely. Default <c>5 seconds</c>.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Snapshot of the <see cref="BufferedAuditStore"/>'s health, suitable for health-check endpoints
/// and dashboards.
/// </summary>
/// <param name="PendingItems">Number of audit records currently waiting in the buffer.</param>
/// <param name="DroppedItems">Total records dropped due to buffer overflow or channel closure.</param>
/// <param name="FlushFailures">Total flush failures in the background worker.</param>
/// <param name="IsFaulted">Whether the background worker has failed permanently.</param>
/// <param name="BackgroundFailure">Message from the exception that faulted the worker, if any.</param>
public sealed record BufferedAuditStoreHealth(
    int PendingItems,
    long DroppedItems,
    long FlushFailures,
    bool IsFaulted,
    string? BackgroundFailure);
