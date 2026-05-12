using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace WebApi.Sample.Infrastructure;

/// <summary>
/// Sample IAuditOutboxPublisher implementation that logs audit entries using Serilog/ILogger.
/// In production, this would be replaced with implementations that send to:
/// - A SIEM system (e.g., Splunk, ELK, Datadog)
/// - A webhook/HTTP endpoint
/// - A message queue (e.g., RabbitMQ, Kafka)
/// - A cloud service (e.g., Azure Event Hubs, AWS Kinesis)
/// </summary>
public class SampleAuditOutboxPublisher : IAuditOutboxPublisher
{
    private readonly ILogger<SampleAuditOutboxPublisher> _logger;

    public SampleAuditOutboxPublisher(ILogger<SampleAuditOutboxPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(AuditOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        // Log the audit entry with full details
        _logger.LogInformation(
            "Publishing audit outbox entry: Id={OutboxId}, RecordId={RecordId}, " +
            "DataSubjectId={DataSubjectId}, Entity={Entity}, Field={Field}, " +
            "Operation={Operation}, Attempts={Attempts}",
            entry.Id,
            entry.Record.Id,
            entry.Record.DataSubjectId,
            entry.Record.Entity,
            entry.Record.Field,
            entry.Record.Operation,
            entry.Attempts);

        // In production, here you would:
        // - Send HTTP request to a webhook
        // - Publish to a message queue
        // - Stream to a SIEM system
        // - etc.

        return Task.CompletedTask;
    }
}
