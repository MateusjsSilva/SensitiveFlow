using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TokenStore.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace SensitiveFlow.Benchmarks;

/// <summary>
/// Benchmarks for Redis Token Store operations.
///
/// Measures:
/// - Token creation latency
/// - Token reversal latency
/// - Throughput (ops/sec)
/// - Memory allocation
/// - Impact of key prefix configuration
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class RedisTokenStoreBenchmarks
{
    private RedisContainer? _container;
    private IConnectionMultiplexer? _connection;
    private ITokenStore? _tokenStore;
    private readonly List<string> _testValues = new();
    private readonly List<string> _generatedTokens = new();

    /// <summary>
    /// Simulates typical scenarios with different data patterns
    /// </summary>
    public enum TestScenario
    {
        /// <summary>Email addresses - high cardinality, frequently accessed</summary>
        Emails,

        /// <summary>IP addresses - medium cardinality, pseudonymized</summary>
        IpAddresses,

        /// <summary>UUIDs - very high cardinality, unique</summary>
        UuidsGuid,

        /// <summary>Customer IDs - low cardinality, frequently repeated</summary>
        CustomerIds
    }

    [ParamsAllValues]
    public TestScenario Scenario { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        Task.Run(async () =>
        {
            // Start Redis container
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();

            // Connect
            _connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());

            // Create token store
            _tokenStore = new RedisTokenStore(_connection, keyPrefix: "bench:", defaultExpiry: TimeSpan.FromHours(1));

            // Generate test data based on scenario
            GenerateTestData();

            // Warm up with a few operations
            foreach (var value in _testValues.Take(5))
            {
                await _tokenStore.GetOrCreateTokenAsync(value);
            }
        }).Wait();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Task.Run(async () =>
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }

            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
            }
        }).Wait();
    }

    /// <summary>
    /// Benchmark: Create or retrieve a new token (first time)
    /// </summary>
    [Benchmark(Description = "GetOrCreateToken (new value)")]
    public async Task<string> BenchmarkGetOrCreateTokenNew()
    {
        var value = $"{Scenario}_{Guid.NewGuid()}";
        return await _tokenStore!.GetOrCreateTokenAsync(value);
    }

    /// <summary>
    /// Benchmark: Retrieve an existing token (cache hit)
    /// </summary>
    [Benchmark(Description = "GetOrCreateToken (existing value)")]
    public async Task<string> BenchmarkGetOrCreateTokenExisting()
    {
        var value = _testValues[Random.Shared.Next(_testValues.Count)];
        return await _tokenStore!.GetOrCreateTokenAsync(value);
    }

    /// <summary>
    /// Benchmark: Resolve token to value
    /// </summary>
    [Benchmark(Description = "ResolveToken")]
    public async Task<string> BenchmarkResolveToken()
    {
        var token = _generatedTokens[Random.Shared.Next(_generatedTokens.Count)];
        return await _tokenStore!.ResolveTokenAsync(token);
    }

    /// <summary>
    /// Benchmark: Bulk operation - create multiple tokens sequentially
    /// </summary>
    [Benchmark(Description = "Bulk GetOrCreateToken (10 operations)")]
    public async Task BenchmarkBulkCreate()
    {
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 10; i++)
        {
            var value = $"{Scenario}_bulk_{i}_{Guid.NewGuid()}";
            tasks.Add(_tokenStore!.GetOrCreateTokenAsync(value));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Benchmark: Concurrent operations (simulates high-load scenario)
    /// </summary>
    [Benchmark(Description = "Concurrent GetOrCreateToken (5 parallel tasks)")]
    public async Task BenchmarkConcurrentOperations()
    {
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 5; i++)
        {
            var value = $"{Scenario}_concurrent_{i}_{Guid.NewGuid()}";
            tasks.Add(_tokenStore!.GetOrCreateTokenAsync(value));
        }
        await Task.WhenAll(tasks);
    }

    private void GenerateTestData()
    {
        _testValues.Clear();
        _generatedTokens.Clear();

        var count = Scenario switch
        {
            TestScenario.Emails => 100,        // High cardinality
            TestScenario.IpAddresses => 50,    // Medium cardinality
            TestScenario.UuidsGuid => 200,     // Very high cardinality
            TestScenario.CustomerIds => 10,    // Low cardinality (repeated)
            _ => 100
        };

        for (int i = 0; i < count; i++)
        {
            var value = Scenario switch
            {
                TestScenario.Emails => $"user{i}@example.com",
                TestScenario.IpAddresses => $"192.168.{i / 256}.{i % 256}",
                TestScenario.UuidsGuid => Guid.NewGuid().ToString(),
                TestScenario.CustomerIds => $"CUST{i % 5:D4}",  // Only 5 unique
                _ => $"value_{i}"
            };

            _testValues.Add(value);
        }

        // Pre-populate tokens
        var populateTasks = _testValues.Select(v =>
            Task.Run(async () =>
            {
                var token = await _tokenStore!.GetOrCreateTokenAsync(v);
                lock (_generatedTokens)
                {
                    _generatedTokens.Add(token);
                }
            })
        );

        Task.WaitAll(populateTasks.ToArray());
    }
}

/// <summary>
/// Benchmarks comparing different token store configurations
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class RedisTokenStoreConfigurationBenchmarks
{
    private RedisContainer? _container;
    private IConnectionMultiplexer? _connection;
    private ITokenStore? _defaultStore;
    private ITokenStore? _customPrefixStore;
    private ITokenStore? _shortTtlStore;

    [GlobalSetup]
    public void GlobalSetup()
    {
        Task.Run(async () =>
        {
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();

            _connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());

            // Default configuration
            _defaultStore = new RedisTokenStore(_connection);

            // Custom prefix (multi-tenant scenario)
            _customPrefixStore = new RedisTokenStore(_connection, keyPrefix: "tenant-123:");

            // Short TTL (session tokens)
            _shortTtlStore = new RedisTokenStore(_connection, defaultExpiry: TimeSpan.FromMinutes(30));
        }).Wait();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Task.Run(async () =>
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }

            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
            }
        }).Wait();
    }

    [Benchmark(Baseline = true)]
    public async Task<string> DefaultConfiguration()
    {
        return await _defaultStore!.GetOrCreateTokenAsync($"test_{Guid.NewGuid()}");
    }

    [Benchmark]
    public async Task<string> CustomPrefixConfiguration()
    {
        return await _customPrefixStore!.GetOrCreateTokenAsync($"test_{Guid.NewGuid()}");
    }

    [Benchmark]
    public async Task<string> ShortTtlConfiguration()
    {
        return await _shortTtlStore!.GetOrCreateTokenAsync($"test_{Guid.NewGuid()}");
    }
}

