using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.TokenStore.Redis;

/// <summary>
/// Extension methods for registering Redis-backed token stores in the DI container.
/// </summary>
public static class RedisTokenStoreExtensions
{
    /// <summary>
    /// Registers a Redis-backed token store with the provided connection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="redisConnection">Connected Redis client.</param>
    /// <param name="keyPrefix">Prefix for token keys in Redis (default: "tokens:").</param>
    /// <param name="defaultExpiry">Optional TTL for token keys.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisTokenStore(
        this IServiceCollection services,
        IConnectionMultiplexer redisConnection,
        string keyPrefix = "tokens:",
        TimeSpan? defaultExpiry = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(redisConnection);

        services.AddSingleton<ITokenStore>(sp =>
            new RedisTokenStore(redisConnection, keyPrefix, defaultExpiry));

        return services;
    }

    /// <summary>
    /// Registers a Redis-backed token store by connection string.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Redis connection string (e.g., "localhost:6379").</param>
    /// <param name="keyPrefix">Prefix for token keys in Redis (default: "tokens:").</param>
    /// <param name="defaultExpiry">Optional TTL for token keys.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method creates a singleton Redis connection. Ensure it is disposed properly
    /// when the application shuts down.
    /// </remarks>
    public static IServiceCollection AddRedisTokenStore(
        this IServiceCollection services,
        string connectionString,
        string keyPrefix = "tokens:",
        TimeSpan? defaultExpiry = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(connectionString));

        services.AddSingleton<ITokenStore>(sp =>
            new RedisTokenStore(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                keyPrefix,
                defaultExpiry));

        return services;
    }
}
