namespace SensitiveFlow.EFCore.BulkOperations;

/// <summary>
/// Configuration for auditing of <c>ExecuteUpdateAsync</c> and <c>ExecuteDeleteAsync</c>
/// against entities decorated with <see cref="Core.Attributes.PersonalDataAttribute"/> or
/// <see cref="Core.Attributes.SensitiveDataAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Bulk operations bypass <c>SaveChanges</c> and the <see cref="Interceptors.SensitiveDataAuditInterceptor"/>
/// chain entirely. The auditing path implemented by
/// <see cref="SensitiveBulkOperationsExtensions"/> performs a <c>SELECT</c> of all affected
/// <c>DataSubjectId</c>s before the modification so that one <c>AuditRecord</c> can be emitted
/// per (subject, field) pair — matching the granularity of <c>SaveChanges</c>-based audits.
/// </para>
/// <para>
/// Because the prefetch <c>SELECT</c> materializes one row per affected subject, very large
/// bulk operations can be expensive. <see cref="MaxAuditedRows"/> bounds that cost: when the
/// prefetch returns more rows than the limit, the auditing helper throws so the caller can
/// make an explicit decision (raise the limit, narrow the predicate, or process in batches).
/// </para>
/// </remarks>
public sealed class SensitiveBulkOperationsOptions
{
    /// <summary>
    /// Upper bound on the number of subjects a single auditing helper call will load and
    /// audit. The default of <c>10_000</c> is chosen to keep the prefetch <c>SELECT</c> and
    /// the audit-record fan-out well below the cost of a typical request and to make the
    /// failure obvious before it degrades production. Set explicitly when you know the
    /// operation must process a larger set.
    /// </summary>
    public int MaxAuditedRows { get; set; } = 10_000;

    /// <summary>
    /// When <see langword="true"/>, the <see cref="SensitiveBulkOperationsGuardInterceptor"/>
    /// throws if a direct <c>ExecuteUpdateAsync</c> or <c>ExecuteDeleteAsync</c> is detected
    /// against an entity that has annotated properties. This is the safe default: bulk
    /// modifications of personal data should go through the auditing helpers so that the
    /// audit trail is not silently bypassed.
    /// </summary>
    /// <remarks>
    /// Disable only when the consuming application has its own auditing layer in front of
    /// EF Core and bulk operations on annotated entities are known to be safe.
    /// </remarks>
    public bool RequireExplicitAuditing { get; set; } = true;
}
