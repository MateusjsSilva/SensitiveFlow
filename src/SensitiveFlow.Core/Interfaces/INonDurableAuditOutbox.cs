namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Marker interface for <see cref="IAuditOutbox"/> implementations that are
/// <b>not durable across process restarts</b> (e.g. in-memory queues used in tests or
/// local development).
/// </summary>
/// <remarks>
/// Diagnostics, health checks, and startup validators key off this marker to warn when a
/// non-durable outbox is configured outside <c>Development</c>. Application code should
/// never depend on this interface directly — it carries no behavior, only intent.
/// </remarks>
public interface INonDurableAuditOutbox : IAuditOutbox
{
}
