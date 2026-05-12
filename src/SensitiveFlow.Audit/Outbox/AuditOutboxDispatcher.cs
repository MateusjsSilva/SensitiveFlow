using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditOutboxDispatcherOptions _options;
    private readonly ILogger<AuditOutboxDispatcher>? _logger;

    /// <summary>Initializes a new instance.</summary>
    public AuditOutboxDispatcher(
        IDurableAuditOutbox? outbox,
        IServiceScopeFactory scopeFactory,
        AuditOutboxDispatcherOptions options,
        ILogger<AuditOutboxDispatcher>? logger = null)
    {
        _outbox = outbox;
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_outbox is null)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SensitiveFlowAuditDiagnostics.RecordFailed();
                _logger?.LogError(
                    ex,
                    "Audit outbox dispatcher failed while polling. Ensure the durable outbox schema exists and the database is reachable.");

                if (_options.SuspendOnInfrastructureFailure)
                {
                    _logger?.LogWarning(
                        "Audit outbox dispatcher polling was suspended after an infrastructure failure. Restart the application after fixing the schema or database connection.");
                    break;
                }

                await Task.Delay(_options.InfrastructureFailureRetryDelay, stoppingToken);
                continue;
            }

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

        using var scope = _scopeFactory.CreateScope();
        var publishers = scope.ServiceProvider.GetServices<IAuditOutboxPublisher>().ToArray();
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
