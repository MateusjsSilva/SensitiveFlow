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
using SensitiveFlow.TokenStore.EFCore;
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
        ?? SqliteConnectionInContentRoot(builder.Environment.ContentRootPath, "sensitiveflow-webapi.db");
    var auditConnection = builder.Configuration.GetConnectionString("Audit")
        ?? SqliteConnectionInContentRoot(builder.Environment.ContentRootPath, "sensitiveflow-webapi-audit.db");
    var tokenConnection = builder.Configuration.GetConnectionString("Tokens")
        ?? SqliteConnectionInContentRoot(builder.Environment.ContentRootPath, "sensitiveflow-webapi-tokens.db");

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

    var app = builder.Build();

    await InitializeSampleDatabasesAsync(app.Services);

    app.UseHttpsRedirection();
    app.UseSensitiveFlow();
    app.UseAuthorization();
    app.MapHealthChecks("/health/sensitiveflow");
    app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>SensitiveFlow Web API Sample</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; max-width: 1100px; color: #172033; }
    main { display: grid; gap: 1rem; }
    form, section { border: 1px solid #d8dee9; border-radius: 8px; padding: 1rem; }
    label { display: grid; gap: .25rem; margin: .75rem 0; font-weight: 600; }
    input { padding: .65rem; border: 1px solid #b8c0cc; border-radius: 6px; font: inherit; }
    button { margin: .2rem .2rem .2rem 0; padding: .65rem .9rem; border: 0; border-radius: 6px; background: #175ddc; color: white; font-weight: 700; cursor: pointer; }
    pre { background: #111827; color: #d1fae5; padding: 1rem; border-radius: 8px; overflow-x: hidden; overflow-y: auto; min-height: 10rem; white-space: pre-wrap; overflow-wrap: anywhere; word-break: break-word; }
    .note { background: #fff7ed; border-color: #fed7aa; }
    .grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }
  </style>
</head>
<body>
<main>
  <h1>SensitiveFlow Web API Sample</h1>
  <section class="note">
    <strong>Sample database.</strong> This sample creates its local SQLite tables on startup so the routes work immediately.
    Production apps should use EF Core migrations or deployment-owned SQL scripts instead.
  </section>
  <div class="grid">
    <form id="create">
      <h2>Create customer</h2>
      <label>Name <input name="name" value="Alice Example"></label>
      <label>Email <input name="email" value="alice@example.test"></label>
      <label>Phone <input name="phone" value="+1 555 0100"></label>
      <label>Tax ID <input name="taxId" value="12345678900"></label>
      <button type="submit">POST /customers</button>
    </form>
    <form id="lookup">
      <h2>Customer workflows</h2>
      <label>Customer ID or DataSubjectId <input name="id" placeholder="Paste id or dataSubjectId"></label>
      <button type="button" id="list">GET /customers</button>
      <button type="submit">GET /customers/{id}</button>
      <button type="button" data-action="json">JSON redaction</button>
      <button type="button" data-action="audit">Audit trail</button>
      <button type="button" data-action="export">Export</button>
      <button type="button" data-action="erase">Erase</button>
    </form>
    <section>
      <h2>Retention</h2>
      <button type="button" id="dryRun">POST /retention/dry-run</button>
      <button type="button" id="runRetention">POST /retention/run</button>
      <button type="button" id="health">GET /health/sensitiveflow</button>
    </section>
  </div>
  <section>
    <h2>Response</h2>
    <pre id="output">Ready.</pre>
  </section>
</main>
<script>
const output = document.querySelector('#output');
const show = async response => {
  const text = await response.text();
  let body = text;
  try {
    body = JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    body = text;
  }
  output.textContent = `${response.status} ${response.statusText}\n${body}`;
};
const rawId = () => new FormData(document.querySelector('#lookup')).get('id')?.trim();
const idValue = () => encodeURIComponent(rawId());
const requireId = () => {
  if (rawId()) return true;
  output.textContent = 'Enter a customer id/dataSubjectId or use GET /customers first.';
  return false;
};
document.querySelector('#create').addEventListener('submit', async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget));
  await show(await fetch('/customers', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(data)
  }));
});
document.querySelector('#lookup').addEventListener('submit', async event => {
  event.preventDefault();
  if (!requireId()) return;
  await show(await fetch(`/customers/${idValue()}`));
});
document.querySelectorAll('[data-action]').forEach(button => {
  button.addEventListener('click', async () => {
    if (!requireId()) return;
    const id = idValue();
    const action = button.dataset.action;
    const map = {
      json: [`/customers/${id}/json`, 'GET'],
      audit: [`/customers/${id}/audit`, 'GET'],
      export: [`/customers/${id}/export`, 'GET'],
      erase: [`/customers/${id}/erase`, 'POST']
    };
    const [url, method] = map[action];
    await show(await fetch(url, { method }));
  });
});
document.querySelector('#list').addEventListener('click', async () => show(await fetch('/customers')));
document.querySelector('#dryRun').addEventListener('click', async () => show(await fetch('/retention/dry-run', { method: 'POST' })));
document.querySelector('#runRetention').addEventListener('click', async () => show(await fetch('/retention/run', { method: 'POST' })));
document.querySelector('#health').addEventListener('click', async () => show(await fetch('/health/sensitiveflow')));
</script>
</body>
</html>
""", "text/html"));
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

static async Task InitializeSampleDatabasesAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();

    await scope.ServiceProvider
        .GetRequiredService<SampleDbContext>()
        .Database
        .EnsureCreatedAsync();

    await using var auditDb = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<AuditDbContext>>()
        .CreateDbContextAsync();
    await auditDb.Database.EnsureCreatedAsync();

    await using var tokenDb = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<TokenDbContext>>()
        .CreateDbContextAsync();
    await tokenDb.Database.EnsureCreatedAsync();
}

static string SqliteConnectionInContentRoot(string contentRoot, string fileName)
    => $"Data Source={Path.Combine(contentRoot, fileName)}";
