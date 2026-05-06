using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Tests.Stores;

/// <summary>
/// In-memory <see cref="ITokenStore"/> for tests only.
/// Mappings are lost on process exit — do NOT use in production.
/// </summary>
internal sealed class InMemoryTokenStore : ITokenStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _valueToToken = new();
    private readonly Dictionary<string, string> _tokenToValue = new();

    public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_valueToToken.TryGetValue(value, out var existing))
            {
                return Task.FromResult(existing);
            }

            var token = Guid.NewGuid().ToString();
            _valueToToken[value] = token;
            _tokenToValue[token] = value;
            return Task.FromResult(token);
        }
    }

    public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_tokenToValue.TryGetValue(token, out var value))
            {
                return Task.FromResult(value);
            }
        }

        throw new KeyNotFoundException($"Token '{token}' was not found.");
    }
}
