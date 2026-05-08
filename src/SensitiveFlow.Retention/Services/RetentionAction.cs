namespace SensitiveFlow.Retention.Services;

/// <summary>
/// Action taken (or required) by <see cref="RetentionExecutor"/> for an expired field.
/// </summary>
public enum RetentionAction
{
    /// <summary>No action taken (policy matched no executor).</summary>
    None = 0,

    /// <summary>The field value was reset to its anonymous default.</summary>
    Anonymized,

    /// <summary>
    /// The entity should be deleted by the caller. Executors do not delete entities themselves
    /// because that requires knowledge of the persistence layer.
    /// </summary>
    DeletePending,

    /// <summary>The owner/operator should be notified out-of-band.</summary>
    NotifyPending,

    /// <summary>The data is flagged as blocked but not modified.</summary>
    Blocked,
}
