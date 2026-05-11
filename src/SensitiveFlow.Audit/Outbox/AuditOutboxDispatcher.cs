using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.Outbox;

/// <summary>
/// Hosted service that dispatches durable audit outbox entries through registered publishers.
/// </summary>
public sealed class AuditOutboxDispatcher : BackgroundService
{
    private readonly IDurableAuditOutbox? _outbox;
    private readonly IEnumerable<IAuditOutboxPublisher> _publishers;
    private readonly AuditOutboxDispatcherOptions _options;
    private readonly ILogger<AuditOutboxDispatcher>? _logger;

    /// <summary>Initializes a new instance.</summary>
    public AuditOutboxDispatcher(
        IDurableAuditOutbox? outbox,
        IEnumerable<IAuditOutboxPublisher> publishers,
        AuditOutboxDispatcherOptions options,
        ILogger<AuditOutboxDispatcher>? logger = null)
    {
        _outbox = outbox;
        _publishers = publishers ?? [];
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_outbox is null || !_publishers.Any())
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchOnceAsync(stoppingToken);
            await Task.Delay(_options.PollInterval, stoppingToken);
        }
    }

    /// <summary>Runs one dispatcher polling cycle. Exposed for tests.</summary>
    public async Task DispatchOnceAsync(CancellationToken cancellationToken = default)
    {
        if (_outbox is null)
        {
            return;
        }

        var publishers = _publishers.ToArray();
        if (publishers.Length == 0)
        {
            return;
        }

        var batch = await _outbox.DequeueBatchAsync(_options.BatchSize, cancellationToken);
        foreach (var entry in batch)
        {
            if (ShouldDeadLetter(entry.Attempts))
            {
                await _outbox.MarkDeadLetteredAsync(entry.Id, "Max audit outbox attempts reached.", cancellationToken);
                SensitiveFlowAuditDiagnostics.RecordDeadLettered();
                continue;
            }

            try
            {
                await DelayForBackoffAsync(entry.Attempts, cancellationToken);
                foreach (var publisher in publishers)
                {
                    await publisher.PublishAsync(entry, cancellationToken);
                }

                await _outbox.MarkProcessedAsync([entry.Id], cancellationToken);
                SensitiveFlowAuditDiagnostics.RecordDispatched();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Audit outbox dispatch failed for entry {EntryId}.", entry.Id);
                await _outbox.MarkFailedAsync(entry.Id, ex.Message, cancellationToken);
                SensitiveFlowAuditDiagnostics.RecordFailed();
            }
        }
    }

    private bool ShouldDeadLetter(int attempts)
        => _options.DeadLetterAfterMax && attempts >= _options.MaxAttempts;

    private Task DelayForBackoffAsync(int attempts, CancellationToken cancellationToken)
    {
        if (attempts <= 0 || _options.Backoff == BackoffStrategy.None)
        {
            return Task.CompletedTask;
        }

        var multiplier = _options.Backoff == BackoffStrategy.Exponential
            ? Math.Pow(2, Math.Min(attempts, 6))
            : attempts;
        var delay = TimeSpan.FromMilliseconds(Math.Min(1000, 100 * multiplier));
        return Task.Delay(delay, cancellationToken);
    }
}
