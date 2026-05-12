using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Decorators;

/// <summary>
/// Decorates an <see cref="IAuditStore"/> (or <see cref="IBatchAuditStore"/>) with bounded
/// exponential-backoff retries on append failures. Query operations are not retried.
/// </summary>
/// <remarks>
/// <para>
/// Audit appends sit in the hot path of <c>SaveChanges</c>. Wrapping the durable store with
/// retries prevents transient store outages (lock contention, brief network blip, leader
/// election) from cascading into <c>SaveChanges</c> failures.
/// </para>
/// <para>
/// The decorator intentionally does <b>not</b> swallow exhausted-retry exceptions: a persistent
/// failure must surface to the caller so the application can fail loudly rather than silently
/// drop audit records.
/// </para>
/// </remarks>
public sealed class RetryingAuditStore : IBatchAuditStore
{
    private readonly IAuditStore _inner;
    private readonly RetryingAuditStoreOptions _options;
    private readonly ILogger<RetryingAuditStore>? _logger;

    /// <summary>Initializes a new instance of <see cref="RetryingAuditStore"/>.</summary>
    public RetryingAuditStore(
        IAuditStore inner,
        RetryingAuditStoreOptions? options = null,
        ILogger<RetryingAuditStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _options = options ?? new RetryingAuditStoreOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        => RetryAsync(ct => _inner.AppendAsync(record, ct), nameof(AppendAsync), cancellationToken);

    /// <inheritdoc />
    public Task AppendRangeAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default)
        => RetryAsync(ct =>
        {
            if (_inner is IBatchAuditStore batch)
            {
                return batch.AppendRangeAsync(records, ct);
            }
            return AppendOneByOneAsync(records, ct);
        }, nameof(AppendRangeAsync), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => _inner.QueryAsync(from, to, skip, take, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => _inner.QueryByDataSubjectAsync(dataSubjectId, from, to, skip, take, cancellationToken);

    private async Task AppendOneByOneAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken ct)
    {
        foreach (var record in records)
        {
            await _inner.AppendAsync(record, ct).ConfigureAwait(false);
        }
    }

    private async Task RetryAsync(Func<CancellationToken, Task> operation, string operationName, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var delay = _options.InitialDelay;

        while (true)
        {
            try
            {
                await operation(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (
                attempt < _options.MaxAttempts - 1 &&
                _options.ShouldRetry(ex) &&
                !cancellationToken.IsCancellationRequested)
            {
                attempt++;

                // Apply equal-jitter backoff: half of the computed delay is fixed, half is random.
                // This breaks the synchronized retry waves that cause "thundering herd" when many
                // instances recover from the same upstream failure simultaneously.
                var jitterFactor = _options.JitterFactor;
                var jittered = jitterFactor <= 0
                    ? delay
                    : TimeSpan.FromTicks((long)(delay.Ticks * (1.0 - jitterFactor + (Random.Shared.NextDouble() * jitterFactor * 2.0))));

                _logger?.LogWarning(ex,
                    "Audit store {Operation} attempt {Attempt}/{MaxAttempts} failed; retrying in {DelayMs}ms.",
                    operationName, attempt, _options.MaxAttempts, jittered.TotalMilliseconds);

                await Task.Delay(jittered, cancellationToken).ConfigureAwait(false);

                delay = TimeSpan.FromTicks(Math.Min(
                    (long)(delay.Ticks * _options.BackoffMultiplier),
                    _options.MaxDelay.Ticks));
            }
        }
    }
}

/// <summary>Options controlling <see cref="RetryingAuditStore"/> retry behavior.</summary>
public sealed class RetryingAuditStoreOptions
{
    /// <summary>Total number of attempts (initial + retries). Default <c>3</c>.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Initial delay before the first retry. Default <c>100ms</c>.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Maximum delay between retries. Default <c>2s</c>.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Multiplier applied to the delay after each failed attempt. Default <c>2.0</c>.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Random jitter applied to each backoff delay, expressed as a fraction in <c>[0, 1)</c>.
    /// Each delay is multiplied by a random value in <c>[1 - JitterFactor, 1 + JitterFactor)</c>
    /// to prevent synchronized retry waves ("thundering herd") across replicas. Set to <c>0</c>
    /// to disable jitter. Default <c>0.25</c> (±25%).
    /// </summary>
    public double JitterFactor { get; set; } = 0.25;

    /// <summary>
    /// Predicate that decides whether a thrown exception should trigger a retry.
    /// Defaults to retrying on any exception that is not a <see cref="OperationCanceledException"/>
    /// or <see cref="ArgumentException"/> (input errors are not transient).
    /// </summary>
    public Func<Exception, bool> ShouldRetry { get; set; } = static ex =>
        ex is not OperationCanceledException &&
        ex is not ArgumentException;
}
