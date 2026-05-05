using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record ProcessingOperationRecord
{
    public required string Id { get; init; }
    public required string Entity { get; init; }
    public ProcessingAgentRole AgentRole { get; init; } = ProcessingAgentRole.Controller;
    public IReadOnlyList<string> Fields { get; init; } = [];
    public ProcessingPurpose Purpose { get; init; } = ProcessingPurpose.Other;
    public LegalBasis LegalBasis { get; init; } = LegalBasis.Consent;
    public IReadOnlyList<ProcessingPrinciple> Principles { get; init; } = [];
    public int? RetentionYears { get; init; }
    public IReadOnlyList<DataSharingRecord> Sharing { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Description { get; init; } = string.Empty;
}
