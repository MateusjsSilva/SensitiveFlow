using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Models;

/// <summary>
/// Record of a personal data processing operation under applicable privacy regulations.
/// </summary>
public sealed record ProcessingOperationRecord
{
    /// <summary>Unique identifier of the processing operation.</summary>
    public required string Id { get; init; }

    /// <summary>Entity or aggregate involved in the processing operation.</summary>
    public required string Entity { get; init; }

    /// <summary>Role of the processing agent for this operation.</summary>
    public ProcessingAgentRole AgentRole { get; init; } = ProcessingAgentRole.Controller;

    /// <summary>Fields or data elements involved in the operation.</summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>Purpose of the processing operation.</summary>
    public ProcessingPurpose Purpose { get; init; } = ProcessingPurpose.Other;

    /// <summary>Legal basis that authorizes the operation.</summary>
    public LegalBasis LegalBasis { get; init; } = LegalBasis.Consent;

    /// <summary>Privacy principles considered for the operation.</summary>
    public IReadOnlyList<ProcessingPrinciple> Principles { get; init; } = [];

    /// <summary>Retention period in years, when declared.</summary>
    public int? RetentionYears { get; init; }

    /// <summary>Third-party sharing records for this operation.</summary>
    public IReadOnlyList<DataSharingRecord> Sharing { get; init; } = [];

    /// <summary>Timestamp when the operation record was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Human-readable description of the operation.</summary>
    public string Description { get; init; } = string.Empty;
}


