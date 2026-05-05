using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Models;

/// <summary>
/// Record of a data subject rights request under Art. 18 of the LGPD.
/// </summary>
public sealed record DataSubjectRequest
{
    /// <summary>Unique identifier of the request.</summary>
    public required string Id { get; init; }

    /// <summary>Identifier of the data subject that submitted the request.</summary>
    public required string DataSubjectId { get; init; }

    /// <summary>Age-related classification of the data subject.</summary>
    public DataSubjectKind DataSubjectKind { get; init; } = DataSubjectKind.Adult;

    /// <summary>Type of right being exercised.</summary>
    public required DataSubjectRequestType Type { get; init; }

    /// <summary>Current lifecycle status of the request.</summary>
    public DataSubjectRequestStatus Status { get; init; } = DataSubjectRequestStatus.Open;

    /// <summary>Timestamp when the request was submitted.</summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Expected response deadline, when tracked.</summary>
    public DateTimeOffset? ResponseDueAt { get; init; }

    /// <summary>Timestamp when the request was completed, when applicable.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Reason why the request was rejected, required when <see cref="Status"/> is
    /// <see cref="DataSubjectRequestStatus.Rejected"/>.
    /// <para>
    /// Art. 18, §4 of the LGPD requires the controller to communicate the factual and
    /// legal grounds for not fulfilling a request. Leaving this field empty on a rejected
    /// request is a compliance gap.
    /// </para>
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>Additional notes or handling context.</summary>
    public string? Notes { get; init; }
}

