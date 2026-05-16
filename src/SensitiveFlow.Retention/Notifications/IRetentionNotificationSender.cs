using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Notifications;

/// <summary>
/// Sends retention completion notifications via configured channels.
/// </summary>
public interface IRetentionNotificationSender
{
    /// <summary>
    /// Sends a notification based on the template and execution report.
    /// </summary>
    /// <param name="template">The notification template to use.</param>
    /// <param name="report">The retention execution report providing data for substitution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(RetentionNotificationTemplate template, RetentionExecutionReport report, CancellationToken cancellationToken = default);
}
