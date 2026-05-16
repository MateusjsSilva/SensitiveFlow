namespace SensitiveFlow.TokenStore.EFCore.Audit;

/// <summary>
/// Immutable record of a pseudonymization token operation.
/// Contains the token (not the original value), the operation type, timestamp, and optional actor.
/// </summary>
/// <param name="Token">The pseudonymous token (never the original value).</param>
/// <param name="Operation">The operation that occurred.</param>
/// <param name="OccurredAt">The UTC timestamp when the operation occurred.</param>
/// <param name="ActorId">Optional identifier of the actor who triggered the operation.</param>
public sealed record TokenAuditRecord(
    string Token,
    TokenAuditOperation Operation,
    DateTimeOffset OccurredAt,
    string? ActorId);
