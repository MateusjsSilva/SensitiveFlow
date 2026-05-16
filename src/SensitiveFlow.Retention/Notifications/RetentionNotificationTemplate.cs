using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Notifications;

/// <summary>
/// Template for formatting retention completion notifications.
/// </summary>
public sealed class RetentionNotificationTemplate
{
    /// <summary>
    /// Gets or sets the notification subject.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification body template.
    /// Supports placeholders: {AnonymizedCount}, {DeletePendingCount}, {RunAt}
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the communication channel for this notification.
    /// </summary>
    public RetentionNotificationChannel Channel { get; set; } = RetentionNotificationChannel.Email;

    /// <summary>
    /// Formats the template body by substituting placeholders with values from the execution report.
    /// </summary>
    /// <param name="report">The retention execution report.</param>
    /// <returns>The formatted message body.</returns>
    public string Format(RetentionExecutionReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var body = Body;
        body = body.Replace("{AnonymizedCount}", report.AnonymizedFieldCount.ToString(), StringComparison.OrdinalIgnoreCase);
        body = body.Replace("{DeletePendingCount}", report.DeletePendingEntityCount.ToString(), StringComparison.OrdinalIgnoreCase);
        body = body.Replace("{RunAt}", DateTimeOffset.UtcNow.ToString("O"), StringComparison.OrdinalIgnoreCase);

        return body;
    }
}
