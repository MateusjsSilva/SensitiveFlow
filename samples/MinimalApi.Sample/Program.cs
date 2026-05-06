// ---------------------------------------------
// SensitiveFlow - Minimal API Sample
//
// Real production stack:
//   - SQLite via EF Core (durable AuditStore + TokenStore)
//   - Serilog (structured logging with file sink)
//   - OpenTelemetry (ASP.NET Core + HTTP instrumentation, console exporter)
//   - SensitiveFlow full integration
// ---------------------------------------------

using Microsoft.EntityFrameworkCore;
using MinimalApi.Sample.Infrastructure;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Masking;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.Logging.Extensions;

// ── Serilog bootstrap ──────────────────────────────────────────────────────
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

    // ── Serilog ──────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) => config
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/minimalapi-sample-.log", rollingInterval: RollingInterval.Day));

    // ── EF Core / SQLite ──────────────────────────────────────────────────
    builder.Services.AddDbContext<SampleDbContext>(o =>
        o.UseSqlite(builder.Configuration.GetConnectionString("Default")
            ?? "Data Source=sensitiveflow-minimalapi.db"));

    // ── SensitiveFlow ─────────────────────────────────────────────────────
    builder.Services.AddScoped<IAuditStore, EfCoreAuditStore>();
    builder.Services.AddScoped<ITokenStore, EfCoreTokenStore>();
    builder.Services.AddScoped<IPseudonymizer>(sp =>
        new TokenPseudonymizer(sp.GetRequiredService<ITokenStore>()));

    builder.Services.AddSensitiveFlowLogging();
    builder.Services.AddSensitiveFlowEFCore();
    builder.Services.AddSensitiveFlowAspNetCore();

    // ── OpenTelemetry ─────────────────────────────────────────────────────
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("SensitiveFlow.MinimalApi.Sample"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter());

    builder.Services.AddOpenApi();

    var app = builder.Build();

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
    app.UseSensitiveFlowAudit();

    // ── POST /customers ───────────────────────────────────────────────────
    app.MapPost("/customers", async (
        CreateCustomerRequest request,
        SampleDbContext db,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        var customer = new Customer
        {
            DataSubjectId = Guid.NewGuid().ToString(),
            Name          = request.Name,
            Email         = request.Email,
            TaxId         = request.TaxId,
            Phone         = request.Phone,
            CreatedAt     = DateTimeOffset.UtcNow,
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Customer created — name: {Name}, email: {[Sensitive]Email}",
            customer.Name.MaskName(),
            customer.Email);

        return Results.Created($"/customers/{customer.DataSubjectId}",
            ToResponse(customer));
    })
    .WithName("CreateCustomer");

    // ── GET /customers/{id} ───────────────────────────────────────────────
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
            DataSubjectId  = customer.DataSubjectId,
            Entity         = nameof(Customer),
            Field          = "*",
            Operation      = AuditOperation.Access,
            ActorId        = auditContext.ActorId,
            IpAddressToken = auditContext.IpAddressToken,
        }, ct);

        logger.LogInformation("Customer {Id} accessed — email: {[Sensitive]Email}",
            id, customer.Email);

        return Results.Ok(ToResponse(customer));
    })
    .WithName("GetCustomer");

    // ── GET /customers/{id}/audit ─────────────────────────────────────────
    app.MapGet("/customers/{id}/audit", async (
        string id,
        IAuditStore auditStore,
        CancellationToken ct) =>
    {
        var records = await auditStore.QueryByDataSubjectAsync(id, cancellationToken: ct);
        return Results.Ok(records);
    })
    .WithName("GetCustomerAudit");

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

// ── DTOs ──────────────────────────────────────────────────────────────────

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
