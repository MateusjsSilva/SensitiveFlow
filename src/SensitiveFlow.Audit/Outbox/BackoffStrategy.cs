namespace SensitiveFlow.Audit.Outbox;

/// <summary>Backoff strategy used by the audit outbox dispatcher.</summary>
public enum BackoffStrategy
{
    /// <summary>No delay beyond the polling interval.</summary>
    None,

    /// <summary>Linear delay based on attempt count.</summary>
    Linear,

    /// <summary>Exponential delay based on attempt count.</summary>
    Exponential,
}
