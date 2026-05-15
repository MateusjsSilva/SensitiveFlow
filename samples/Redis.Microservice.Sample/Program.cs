using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TokenStore.Redis;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);

builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddRedisTokenStore(redis, keyPrefix: "app:tokens:");
builder.Services.AddScoped<IPseudonymizer, TokenPseudonymizer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
