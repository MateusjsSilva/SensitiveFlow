using SensitiveFlow.Anonymization.Pseudonymizers;
using StackExchange.Redis;

const string defaultConnectionString = "localhost:6379";
var connectionString = Environment.GetEnvironmentVariable("SENSITIVEFLOW_REDIS") ?? defaultConnectionString;

Console.WriteLine("SensitiveFlow Redis token store sample");
Console.WriteLine($"Connecting to Redis: {connectionString}");

var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);

try
{
    var tokenStore = new Redis.Sample.RedisTokenStore(connection);
    var pseudonymizer = new TokenPseudonymizer(tokenStore);

    var rawValue = "192.168.10.55";
    var token = await pseudonymizer.PseudonymizeAsync(rawValue);
    var tokenAgain = await pseudonymizer.PseudonymizeAsync(rawValue);
    var resolved = await pseudonymizer.ReverseAsync(token);

    Console.WriteLine($"Raw value    : {rawValue}");
    Console.WriteLine($"Token        : {token}");
    Console.WriteLine($"Same token?  : {string.Equals(token, tokenAgain, StringComparison.Ordinal)}");
    Console.WriteLine($"Resolved back: {resolved}");
}
finally
{
    await connection.CloseAsync();
}
