using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Pseudonymizers;

/// <summary>
/// Reversible pseudonymization backed by a persistent <see cref="ITokenStore"/>.
/// Each unique value receives a stable token; the mapping survives restarts as long as
/// the store implementation is durable (database, Redis, etc.).
/// The data remains personal and all privacy obligations apply.
/// </summary>
/// <remarks>
/// For tests and batch processing within a single session, use
/// <c>InMemoryTokenStore</c> as the backing store.
/// For production, provide a durable implementation (SQL, Redis, etc.).
/// </remarks>
public sealed class TokenPseudonymizer : IPseudonymizer
{
    private readonly ITokenStore _store;

    /// <summary>Initializes a new instance with the provided token store.</summary>
    /// <param name="store">Durable store that persists token ↔ value mappings.</param>
    public TokenPseudonymizer(ITokenStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="value"/> is non-empty.</summary>
    public bool CanPseudonymize(string value) => !string.IsNullOrEmpty(value);

    /// <summary>
    /// Pseudonymizes <paramref name="value"/> synchronously by blocking on the store.
    /// <b>⚠ NOT SAFE IN ASP.NET CORE:</b> This method uses <c>GetAwaiter().GetResult()</c> which causes deadlocks
    /// under high concurrency. Use <see cref="PseudonymizeAsync"/> in all async contexts.
    /// Safe only in console apps, Windows services, or offline batch processing.
    /// </summary>
    public string Pseudonymize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _store.GetOrCreateTokenAsync(value).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Pseudonymizes <paramref name="value"/> asynchronously via the backing <see cref="ITokenStore"/>.
    /// </summary>
    public Task<string> PseudonymizeAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _store.GetOrCreateTokenAsync(value, cancellationToken);
    }

    /// <summary>
    /// Reverses <paramref name="token"/> to the original value synchronously by blocking on the store.
    /// <b>⚠ NOT SAFE IN ASP.NET CORE:</b> This method uses <c>GetAwaiter().GetResult()</c> which causes deadlocks
    /// under high concurrency. Use <see cref="ReverseAsync"/> in all async contexts.
    /// Safe only in console apps, Windows services, or offline batch processing.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the token is not found in the store.</exception>
    public string Reverse(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return _store.ResolveTokenAsync(token).GetAwaiter().GetResult();
    }

    /// <summary>Async version of <see cref="Reverse"/>.</summary>
    public Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        return _store.ResolveTokenAsync(token, cancellationToken);
    }
}


