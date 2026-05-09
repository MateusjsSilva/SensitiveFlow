namespace SensitiveFlow.TokenStore.EFCore.Entities;

/// <summary>
/// EF Core persistence shape for token-to-value mappings used by <see cref="Stores.EfCoreTokenStore{TContext}"/>.
/// </summary>
public sealed class TokenMappingEntity
{
    /// <summary>Surrogate key used by the database.</summary>
    public long Id { get; set; }

    /// <summary>Original value that was pseudonymized.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Stable token assigned to the value.</summary>
    public string Token { get; set; } = string.Empty;
}
