namespace SensitiveFlow.Retention.Notifications;

/// <summary>
/// Specifies the communication channel for retention notifications.
/// </summary>
public enum RetentionNotificationChannel
{
    /// <summary>Send notification via email.</summary>
    Email = 0,

    /// <summary>Send notification via Slack.</summary>
    Slack = 1,

    /// <summary>Send notification via HTTP webhook.</summary>
    Webhook = 2
}
