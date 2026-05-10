using System.Collections.Concurrent;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TestKit;

namespace SensitiveFlow.TestKit.Tests;

public sealed class TokenStoreContractTestsTests : TokenStoreContractTests
{
    protected override Task<ITokenStore> CreateStoreAsync() =>
        Task.FromResult<ITokenStore>(new InMemoryTokenStore());

    private sealed class InMemoryTokenStore : ITokenStore
    {
        private readonly ConcurrentDictionary<string, string> _tokensByValue = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _valuesByToken = new(StringComparer.Ordinal);

        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
        {
            var token = _tokensByValue.GetOrAdd(value, static v => "tok-" + v);
            _valuesByToken.TryAdd(token, value);
            return Task.FromResult(token);
        }

        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (_valuesByToken.TryGetValue(token, out var value))
            {
                return Task.FromResult(value);
            }

            throw new KeyNotFoundException($"Token '{token}' not found.");
        }
    }
}
