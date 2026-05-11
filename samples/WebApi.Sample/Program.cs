using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using SensitiveFlow.AspNetCore.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
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
    var tokenConnection = builder.Configuration.GetConnectionString("Tokens")
        ?? "Data Source=sensitiveflow-webapi-tokens.db";

    builder.Services.AddDbContext<SampleDbContext>((sp, options) =>
        options.UseSqlite(appConnection)
            .AddInterceptors(sp.GetRequiredService<SensitiveDataAuditInterceptor>()));

    builder.Services.AddSensitiveFlowWeb(options =>
    {
        options.UseProfile(SensitiveFlowProfile.Strict);

        options.UseEfCoreStores(
            audit => audit.UseSqlite(auditConnection),
            tokens => tokens.UseSqlite(tokenConnection));

        options.EnableEfCoreAudit();
        options.EnableAspNetCoreContext();
        options.EnableJsonRedaction(json =>
        {
            json.DefaultMode = JsonRedactionMode.Mask;
        });
        options.EnableLoggingRedaction();
        options.EnableValidation();
        options.EnableDiagnostics();
        options.EnableAuditStoreRetry();
        options.EnableCachingTokenStore();
        options.EnableDataSubjectExport();
        options.EnableDataSubjectErasure();
        options.EnableRetention().EnableRetentionExecutor();
        options.EnableOutbox(outbox =>
        {
            outbox.PollInterval = TimeSpan.FromSeconds(5);
            outbox.BatchSize = 100;
            outbox.MaxAttempts = 5;
        });
        options.EnableHealthChecks();

        // Custom policy overrides on top of Strict profile
        options.ConfigurePolicies(policies =>
        {
            policies.Policies.ForCategory(DataCategory.Contact)
                .MaskInLogs()
                .RedactInJson()
                .AuditOnChange();
            policies.Policies.ForSensitiveCategory(SensitiveDataCategory.Other)
                .OmitInJson()
                .RequireAudit();
        });
    });

    // Register the outbox publisher (app-level concern)
    builder.Services.AddScoped<SampleAuditOutboxPublisher>();
    builder.Services.AddScoped<IAuditOutboxPublisher>(sp =>
        sp.GetRequiredService<SampleAuditOutboxPublisher>());

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

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseSensitiveFlow();
    app.UseAuthorization();
    app.MapHealthChecks("/health/sensitiveflow");
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
