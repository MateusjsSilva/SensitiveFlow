using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Implementations;

/// <summary>
/// Adapts a streaming result set to provide both async enumeration and metadata.
/// </summary>
internal sealed class AuditStreamQueryAdapter : IAuditStreamQuery
{
    private readonly IAsyncEnumerable<AuditRecord> _source;
    private readonly AuditQuery _query;
    private readonly Func<CancellationToken, Task<int>>? _countFactory;

    public AuditQuery QueryCriteria => _query;

    public AuditStreamQueryAdapter(
        IAsyncEnumerable<AuditRecord> source,
        AuditQuery query,
        Func<CancellationToken, Task<int>>? countFactory = null)
    {
        _source = source;
        _query = query;
        _countFactory = countFactory;
    }

    public IAsyncEnumerator<AuditRecord> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => _source.GetAsyncEnumerator(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _countFactory?.Invoke(cancellationToken) ?? Task.FromResult(0);
}
