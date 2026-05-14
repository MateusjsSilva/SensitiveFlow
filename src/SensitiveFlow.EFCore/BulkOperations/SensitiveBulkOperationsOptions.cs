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
    private int _maxAuditedRows;

    /// <summary>
    /// Upper bound on the number of subjects a single auditing helper call will load and
    /// audit. Default is computed heuristically based on available memory; explicitly set
    /// to override when you know the operation must process a larger set. Minimum 1, maximum 1,000,000.
    /// </summary>
    public int MaxAuditedRows
    {
        get => _maxAuditedRows == 0 ? ComputeDefaultLimit() : _maxAuditedRows;
        set
        {
            if (value < 1 || value > 1_000_000)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxAuditedRows must be between 1 and 1,000,000");
            }
            _maxAuditedRows = value;
        }
    }

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

    /// <summary>
    /// Computes a heuristic default limit based on available managed memory.
    /// Conservative: 10_000 for &lt;1GB, 50_000 for &lt;4GB, 100_000 for &gt;4GB.
    /// </summary>
    private static int ComputeDefaultLimit()
    {
        var totalMemory = GC.GetTotalMemory(false);
        return totalMemory switch
        {
            < 1_000_000_000 => 10_000,      // &lt; 1 GB
            < 4_000_000_000 => 50_000,      // &lt; 4 GB
            _ => 100_000                    // &gt;= 4 GB
        };
    }
}
