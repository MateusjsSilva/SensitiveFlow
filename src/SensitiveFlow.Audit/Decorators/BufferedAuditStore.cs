using System.Threading.Channels;
using Microsoft.Extensions.Logging;
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
    private readonly IAuditStore _inner;
    private readonly BufferedAuditStoreOptions _options;
    private readonly ILogger<BufferedAuditStore>? _logger;
    private readonly Channel<AuditRecord> _channel;
    private readonly Task _worker;
    private Exception? _backgroundFailure;

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
        _worker = Task.Run(ProcessAsync);
    }

    /// <inheritdoc />
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ThrowIfBackgroundFailed();

        await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AppendRangeAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        ThrowIfBackgroundFailed();

        foreach (var record in records)
        {
            await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
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
    public void Dispose()
        => DisposeAsync().AsTask().GetAwaiter().GetResult();

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
        }
        catch (Exception ex)
        {
            _backgroundFailure = ex;
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
}
