// ---------------------------------------------
// SensitiveFlow - Web API (Controllers) Sample
//
// Real production stack:
//   - SQLite via EF Core (durable AuditStore + TokenStore)
//   - Serilog (structured logging with file sink)
//   - OpenTelemetry (ASP.NET Core + HTTP instrumentation, console exporter)
//   - SensitiveFlow full integration:
//       AddAuditStore, AddTokenStore, AddSensitiveFlowEFCore,
//       AddSensitiveFlowAspNetCore, AddSensitiveFlowLogging
// ---------------------------------------------

using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.Logging.Extensions;
using WebApi.Sample.Infrastructure;

// ── Serilog bootstrap ──────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/webapi-sample-.log",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) => config
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/webapi-sample-.log", rollingInterval: RollingInterval.Day));

    // ── EF Core / SQLite ──────────────────────────────────────────────────
    builder.Services.AddDbContext<SampleDbContext>(o =>
        o.UseSqlite(builder.Configuration.GetConnectionString("Default")
            ?? "Data Source=sensitiveflow-webapi.db"));

    // ── SensitiveFlow ─────────────────────────────────────────────────────
    // Register durable stores — EF Core / SQLite implementations.
    builder.Services.AddScoped<IAuditStore, EfCoreAuditStore>();
    builder.Services.AddScoped<ITokenStore, EfCoreTokenStore>();

    // AddAuditStore<T> and AddTokenStore<T> register scoped stores;
    // for scoped EF Core stores we register directly and wire the pseudonymizer manually.
    builder.Services.AddSensitiveFlowLogging();
    builder.Services.AddSensitiveFlowEFCore();
    builder.Services.AddSensitiveFlowAspNetCore();

    // Wire the TokenPseudonymizer to the scoped EfCoreTokenStore.
    builder.Services.AddScoped<IPseudonymizer>(sp =>
    {
        var store = sp.GetRequiredService<ITokenStore>();
        return new SensitiveFlow.Anonymization.Pseudonymizers.TokenPseudonymizer(store);
    });

    // ── OpenTelemetry ─────────────────────────────────────────────────────
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("SensitiveFlow.WebApi.Sample"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter());

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Ensure DB is created on startup.
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<SampleDbContext>()
            .Database.EnsureCreatedAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    // UseSensitiveFlowAudit pseudonymizes the remote IP and stores the token
    // in HttpContext.Items before any controller executes.
    app.UseSensitiveFlowAudit();

    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
