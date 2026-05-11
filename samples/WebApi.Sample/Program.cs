using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore.Outbox.Extensions;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Diagnostics.Extensions;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.HealthChecks.Extensions;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Json.Extensions;
using SensitiveFlow.Logging.Extensions;
using SensitiveFlow.Retention.Extensions;
using SensitiveFlow.TokenStore.EFCore;
using SensitiveFlow.TokenStore.EFCore.Extensions;
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

    builder.Services.AddEfCoreAuditStore(options => options.UseSqlite(auditConnection));
    builder.Services.AddEfCoreAuditOutbox(options =>
    {
        // Configure durable outbox with sensible defaults
        options.PollInterval = TimeSpan.FromSeconds(5);     // Poll every 5 seconds
        options.BatchSize = 100;                             // Process up to 100 entries per batch
        options.MaxAttempts = 5;                             // Retry up to 5 times before dead-lettering
    });
    builder.Services.AddScoped<SampleAuditOutboxPublisher>();
    builder.Services.AddScoped<IAuditOutboxPublisher>(sp => sp.GetRequiredService<SampleAuditOutboxPublisher>());
    builder.Services.AddAuditStoreRetry();
    builder.Services.AddSensitiveFlowDiagnostics();
    builder.Services.AddEfCoreTokenStore(options => options.UseSqlite(tokenConnection));
    builder.Services.AddCachingTokenStore();
    builder.Services.AddDataSubjectExport();
    builder.Services.AddDataSubjectErasure();

    var sensitiveFlowOptions = SensitiveFlowPolicyConfiguration.Create(options =>
    {
        options.UseProfile(SensitiveFlowProfile.Strict);
        options.Policies.ForCategory(DataCategory.Contact)
            .MaskInLogs()
            .RedactInJson()
            .AuditOnChange();
        options.Policies.ForSensitiveCategory(SensitiveDataCategory.Other)
            .OmitInJson()
            .RequireAudit();
    });

    builder.Services.AddSingleton(sensitiveFlowOptions);
    builder.Services.AddSensitiveFlowLogging(options =>
    {
        options.Policies = sensitiveFlowOptions.Policies;
    });
    builder.Services.AddSensitiveFlowEFCore();
    builder.Services.AddSensitiveFlowAspNetCore();
    builder.Services.AddSensitiveFlowValidation(options =>
    {
        options.RequireAuditStore = true;
        options.RequireTokenStore = true;
        options.RequireJsonRedaction = true;
        options.RequireRetention = true;
    });
    builder.Services.AddSensitiveFlowJsonRedaction(options =>
    {
        options.DefaultMode = JsonRedactionMode.Mask;
        options.Policies = sensitiveFlowOptions.Policies;
    });
    builder.Services.AddRetention();
    builder.Services.AddRetentionExecutor();
    builder.Services.AddSensitiveFlowHealthChecks()
        .AddAuditStoreCheck()
        .AddTokenStoreCheck();

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
                new JsonRedactionOptions
                {
                    DefaultMode = JsonRedactionMode.Mask,
                    Policies = sensitiveFlowOptions.Policies,
                }));
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

        await using var tokenDb = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<TokenDbContext>>()
            .CreateDbContextAsync();
        await tokenDb.Database.EnsureCreatedAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseSensitiveFlowAudit();
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
