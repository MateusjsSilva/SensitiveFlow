using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.TokenStore.EFCore.Audit;

/// <summary>
/// Decorator that wraps an <see cref="ITokenStore"/> and records all operations to an <see cref="ITokenAuditSink"/>.
/// Operations are recorded transparently without altering the wrapped store's behavior.
/// </summary>
public sealed class AuditingTokenStore : ITokenStore
{
    private readonly ITokenStore _inner;
    private readonly ITokenAuditSink _sink;
    private readonly string? _actorId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditingTokenStore"/> class.
    /// </summary>
    /// <param name="inner">The underlying token store to decorate.</param>
    /// <param name="sink">The audit sink to record operations to.</param>
    /// <param name="actorId">Optional identifier of the actor performing operations.</param>
    public AuditingTokenStore(ITokenStore inner, ITokenAuditSink sink, string? actorId = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(sink);

        _inner = inner;
        _sink = sink;
        _actorId = actorId;
    }

    /// <inheritdoc />
    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var token = await _inner.GetOrCreateTokenAsync(value, cancellationToken).ConfigureAwait(false);

        var record = new TokenAuditRecord(token, TokenAuditOperation.Created, DateTimeOffset.UtcNow, _actorId);
        await _sink.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        return token;
    }

    /// <inheritdoc />
    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var value = await _inner.ResolveTokenAsync(token, cancellationToken).ConfigureAwait(false);

        var record = new TokenAuditRecord(token, TokenAuditOperation.Resolved, DateTimeOffset.UtcNow, _actorId);
        await _sink.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        return value;
    }
}
