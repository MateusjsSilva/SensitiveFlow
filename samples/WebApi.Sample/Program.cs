using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Diagnostics.Extensions;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Json.Extensions;
using SensitiveFlow.Logging.Extensions;
using SensitiveFlow.Retention.Extensions;
using WebApi.Sample.Infrastructure;

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

    builder.Host.UseSerilog((ctx, services, config) => config
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/webapi-sample-.log", rollingInterval: RollingInterval.Day));

    var appConnection = builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=sensitiveflow-webapi.db";
    var auditConnection = builder.Configuration.GetConnectionString("Audit")
        ?? "Data Source=sensitiveflow-webapi-audit.db";

    builder.Services.AddDbContext<SampleDbContext>((sp, options) =>
        options.UseSqlite(appConnection)
            .AddInterceptors(sp.GetRequiredService<SensitiveDataAuditInterceptor>()));

    builder.Services.AddEfCoreAuditStore(options => options.UseSqlite(auditConnection));
    builder.Services.AddAuditStoreRetry();
    builder.Services.AddSensitiveFlowDiagnostics();

    // This sample registers ITokenStore and IPseudonymizer manually to show the
    // explicit wiring. For a simpler setup, use the first-party EF Core extension:
    //   builder.Services.AddEfCoreTokenStore(options => options.UseSqlite(...));
    // That single call registers both ITokenStore (Singleton) and IPseudonymizer (Scoped).
    builder.Services.AddScoped<ITokenStore, EfCoreTokenStore>();
    builder.Services.AddScoped<IPseudonymizer, TokenPseudonymizer>();
    builder.Services.AddCachingTokenStore();
    builder.Services.AddDataSubjectExport();
    builder.Services.AddDataSubjectErasure();

    builder.Services.AddSensitiveFlowLogging();
    builder.Services.AddSensitiveFlowEFCore();
    builder.Services.AddSensitiveFlowAspNetCore();
    builder.Services.AddSensitiveFlowJsonRedaction(options => options.DefaultMode = JsonRedactionMode.Mask);
    builder.Services.AddRetention();
    builder.Services.AddRetentionExecutor();

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("SensitiveFlow.WebApi.Sample"))
            .AddSource(SensitiveFlowDiagnostics.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter())
        .WithMetrics(metrics => metrics
            .AddMeter(SensitiveFlowDiagnostics.MeterName)
            .AddConsoleExporter());

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.WithSensitiveDataRedaction(
                new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask }));
    builder.Services.AddOpenApi();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<SampleDbContext>()
            .Database.EnsureCreatedAsync();

        await using var auditDb = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AuditDbContext>>()
            .CreateDbContextAsync();
        await auditDb.Database.EnsureCreatedAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
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
