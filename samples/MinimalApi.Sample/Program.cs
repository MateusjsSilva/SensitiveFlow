using Microsoft.EntityFrameworkCore;
using MinimalApi.Sample.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using SensitiveFlow.Anonymization.Erasure;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Export;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Discovery;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
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
using SensitiveFlow.Retention.Services;
using SensitiveFlow.TokenStore.EFCore;
using SensitiveFlow.TokenStore.EFCore.Extensions;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/minimalapi-sample-.log",
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
        .WriteTo.File("logs/minimalapi-sample-.log", rollingInterval: RollingInterval.Day));

    var appConnection = builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=sensitiveflow-minimalapi.db";
    var auditConnection = builder.Configuration.GetConnectionString("Audit")
        ?? "Data Source=sensitiveflow-minimalapi-audit.db";
    var tokenConnection = builder.Configuration.GetConnectionString("Tokens")
        ?? "Data Source=sensitiveflow-minimalapi-tokens.db";

    builder.Services.AddDbContext<SampleDbContext>((sp, options) =>
        options.UseSqlite(appConnection)
            .AddInterceptors(sp.GetRequiredService<SensitiveDataAuditInterceptor>()));

    builder.Services.AddEfCoreAuditStore(options => options.UseSqlite(auditConnection));
    builder.Services.AddAuditStoreRetry();
    builder.Services.AddSensitiveFlowDiagnostics();
    builder.Services.AddEfCoreTokenStore(options => options.UseSqlite(tokenConnection));
    builder.Services.AddCachingTokenStore();
    builder.Services.AddDataSubjectExport();
    builder.Services.AddDataSubjectErasure();

    var sensitiveFlowOptions = SensitiveFlowPolicyConfiguration.Create(options =>
    {
        options.UseProfile(SensitiveFlowProfile.Balanced);
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
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.WithSensitiveDataRedaction(
            new JsonRedactionOptions
            {
                DefaultMode = JsonRedactionMode.Mask,
                Policies = sensitiveFlowOptions.Policies,
            }));

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("SensitiveFlow.MinimalApi.Sample"))
            .AddSource(SensitiveFlowDiagnostics.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter())
        .WithMetrics(metrics => metrics
            .AddMeter(SensitiveFlowDiagnostics.MeterName)
            .AddConsoleExporter());

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
    app.MapHealthChecks("/health/sensitiveflow");

    app.MapPost("/customers", async (
        CreateCustomerRequest request,
        SampleDbContext db,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        var customer = new Customer
        {
            DataSubjectId = Guid.NewGuid().ToString(),
            Name = request.Name,
            Email = request.Email,
            TaxId = request.TaxId,
            Phone = request.Phone,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Customer created - name: {Name}, email: {[Sensitive]Email}",
            customer.Name.MaskName(),
            customer.Email.MaskEmail());

        return Results.Created($"/customers/{customer.DataSubjectId}", ToResponse(customer));
    })
    .WithName("CreateCustomer");

    app.MapGet("/customers/{id}", async (
        string id,
        SampleDbContext db,
        IAuditStore auditStore,
        IAuditContext auditContext,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        if (customer is null)
        {
            return Results.NotFound();
        }

        await auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = customer.DataSubjectId,
            Entity = nameof(Customer),
            Field = "*",
            Operation = AuditOperation.Access,
            ActorId = auditContext.ActorId,
            IpAddressToken = auditContext.IpAddressToken,
        }, ct);

        logger.LogInformation("Customer {Id} accessed - email: {[Sensitive]Email}",
            id.SanitizeForLog(), customer.Email.MaskEmail());

        return Results.Ok(ToResponse(customer));
    })
    .WithName("GetCustomer");

    app.MapGet("/customers/{id}/audit", async (
        string id,
        IAuditStore auditStore,
        CancellationToken ct) =>
    {
        var records = await auditStore.QueryByDataSubjectAsync(id, cancellationToken: ct);
        return Results.Ok(records);
    })
    .WithName("GetCustomerAudit");

    app.MapGet("/customers/{id}/json", async (
        string id,
        SampleDbContext db,
        CancellationToken ct) =>
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        return customer is null ? Results.NotFound() : Results.Ok(customer);
    })
    .WithName("GetCustomerWithJsonRedaction");

    app.MapGet("/customers/{id}/export", async (
        string id,
        SampleDbContext db,
        IDataSubjectExporter exporter,
        CancellationToken ct) =>
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        return customer is null ? Results.NotFound() : Results.Ok(exporter.Export(customer));
    })
    .WithName("ExportCustomerData");

    app.MapPost("/customers/{id}/erase", async (
        string id,
        SampleDbContext db,
        IDataSubjectErasureService erasure,
        IAuditStore auditStore,
        IAuditContext auditContext,
        CancellationToken ct) =>
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        if (customer is null)
        {
            return Results.NotFound();
        }

        var changed = erasure.Erase(customer);
        await db.SaveChangesAsync(ct);

        await auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = customer.DataSubjectId,
            Entity = nameof(Customer),
            Field = "*",
            Operation = AuditOperation.Anonymize,
            ActorId = auditContext.ActorId,
            IpAddressToken = auditContext.IpAddressToken,
            Details = $"Erased {changed} annotated fields.",
        }, ct);

        return Results.Ok(new { changed });
    })
    .WithName("EraseCustomerData");

    app.MapPost("/retention/run", async (
        SampleDbContext db,
        RetentionExecutor retention,
        CancellationToken ct) =>
    {
        var customers = await db.Customers.ToListAsync(ct);
        var report = await retention.ExecuteAsync(
            customers,
            entity => ((Customer)entity).CreatedAt,
            ct);

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            report.AnonymizedFieldCount,
            report.DeletePendingEntityCount,
            Entries = report.Entries.Select(e => new
            {
                e.FieldName,
                e.ExpiredAt,
                Action = e.Action.ToString(),
            }),
        });
    })
    .WithName("RunRetention");

    app.MapPost("/retention/dry-run", async (
        SampleDbContext db,
        RetentionExecutor retention,
        CancellationToken ct) =>
    {
        var customers = await db.Customers.AsNoTracking().ToListAsync(ct);
        var report = await retention.DryRunAsync(
            customers,
            entity => ((Customer)entity).CreatedAt,
            ct);

        return Results.Ok(new
        {
            report.AnonymizedFieldCount,
            report.DeletePendingEntityCount,
            Entries = report.Entries.Select(e => new
            {
                e.FieldName,
                e.ExpiredAt,
                Action = e.Action.ToString(),
            }),
        });
    })
    .WithName("DryRunRetention");

    app.MapGet("/sensitiveflow/discovery", () =>
    {
        var report = SensitiveDataDiscovery.Scan(typeof(Customer).Assembly);
        return Results.Text(report.ToMarkdown(), "text/markdown");
    })
    .WithName("SensitiveFlowDiscovery");

    app.MapGet("/sensitiveflow/diagnostics", (IServiceProvider services) =>
    {
        var report = services.ValidateSensitiveFlow();
        return Results.Ok(new
        {
            report.IsValid,
            report.Diagnostics,
        });
    })
    .WithName("SensitiveFlowDiagnostics");

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

static CustomerResponse ToResponse(Customer c) => new(
    c.DataSubjectId,
    c.Name.MaskName(),
    c.Email.MaskEmail(),
    c.Phone.MaskPhone());

public sealed record CreateCustomerRequest(
    string Name,
    string Email,
    string TaxId,
    string Phone);

public sealed record CustomerResponse(
    string DataSubjectId,
    string Name,
    string Email,
    string Phone);
