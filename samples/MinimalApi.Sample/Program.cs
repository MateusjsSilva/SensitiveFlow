using Microsoft.EntityFrameworkCore;
using MinimalApi.Sample.Infrastructure;
using SensitiveFlow.AspNetCore.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.TokenStore.EFCore;

var builder = WebApplication.CreateBuilder(args);

var appConnection = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=sensitiveflow-minimalapi.db";
var auditConnection = builder.Configuration.GetConnectionString("Audit")
    ?? "Data Source=sensitiveflow-minimalapi-audit.db";
var tokenConnection = builder.Configuration.GetConnectionString("Tokens")
    ?? "Data Source=sensitiveflow-minimalapi-tokens.db";

builder.Services.AddDbContext<SampleDbContext>((sp, options) =>
    options.UseSqlite(appConnection)
        .AddInterceptors(sp.GetRequiredService<SensitiveFlow.EFCore.Interceptors.SensitiveDataAuditInterceptor>()));

builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);

    options.UseEfCoreStores(
        audit => audit.UseSqlite(auditConnection),
        tokens => tokens.UseSqlite(tokenConnection));

    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
    options.EnableJsonRedaction();
    options.EnableLoggingRedaction();
    options.EnableValidation();
    options.EnableHealthChecks();
});

var app = builder.Build();

await InitializeSampleDatabasesAsync(app.Services);

app.UseHttpsRedirection();
app.UseSensitiveFlow();
app.MapHealthChecks("/health/sensitiveflow");

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>SensitiveFlow Minimal API Sample</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; max-width: 960px; color: #172033; }
    main { display: grid; gap: 1rem; }
    form, section { border: 1px solid #d8dee9; border-radius: 8px; padding: 1rem; }
    label { display: grid; gap: .25rem; margin: .75rem 0; font-weight: 600; }
    input { padding: .65rem; border: 1px solid #b8c0cc; border-radius: 6px; font: inherit; }
    button { padding: .65rem .9rem; border: 0; border-radius: 6px; background: #175ddc; color: white; font-weight: 700; cursor: pointer; }
    pre { background: #111827; color: #d1fae5; padding: 1rem; border-radius: 8px; overflow: auto; min-height: 8rem; }
    .note { background: #fff7ed; border-color: #fed7aa; }
    .grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); }
  </style>
</head>
<body>
<main>
  <h1>SensitiveFlow Minimal API Sample</h1>
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
      <h2>Read customer</h2>
      <label>DataSubjectId <input name="id" placeholder="Paste returned dataSubjectId"></label>
      <button type="button" id="list">GET /customers</button>
      <button type="submit" data-path="/customers/{id}">GET /customers/{id}</button>
      <button type="button" id="json">GET /customers/{id}/json</button>
    </form>
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
  output.textContent = `${response.status} ${response.statusText}\n${text}`;
};
const rawId = () => new FormData(document.querySelector('#lookup')).get('id')?.trim();
const idValue = () => encodeURIComponent(rawId());
const requireId = () => {
  if (rawId()) return true;
  output.textContent = 'Enter a DataSubjectId or use GET /customers first.';
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
document.querySelector('#json').addEventListener('click', async () => {
  if (!requireId()) return;
  await show(await fetch(`/customers/${idValue()}/json`));
});
document.querySelector('#list').addEventListener('click', async () => {
  await show(await fetch('/customers'));
});
</script>
</body>
</html>
""", "text/html"));

app.MapGet("/customers", async (
    SampleDbContext db,
    CancellationToken ct) =>
{
    var customers = await db.Customers
        .Take(100)
        .ToListAsync(ct);

    return Results.Ok(customers
        .OrderByDescending(c => c.CreatedAt)
        .Take(20)
        .Select(ToResponse));
})
.WithName("ListCustomers");

app.MapPost("/customers", async (
    CreateCustomerRequest request,
    SampleDbContext db,
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

    return Results.Created($"/customers/{customer.DataSubjectId}", ToResponse(customer));
})
.WithName("CreateCustomer");

app.MapGet("/customers/{id}", async (
    string id,
    SampleDbContext db,
    CancellationToken ct) =>
{
    var customer = await db.Customers
        .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

    return customer is null ? Results.NotFound() : Results.Ok(customer);
})
.WithName("GetCustomer");

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

app.Run();

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

static CustomerResponse ToResponse(Customer c) => new(
    c.DataSubjectId,
    c.Name,
    c.Email,
    c.Phone);

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
