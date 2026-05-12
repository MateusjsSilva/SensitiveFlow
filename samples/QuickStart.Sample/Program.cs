using Microsoft.EntityFrameworkCore;
using QuickStart.Sample;
using SensitiveFlow.AspNetCore.EFCore.Extensions;
using SensitiveFlow.Core.Profiles;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=quickstart.db";

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
        .AddInterceptors(sp.GetRequiredService<SensitiveFlow.EFCore.Interceptors.SensitiveDataAuditInterceptor>()));

builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);

    options.UseEfCoreStores(
        audit => audit.UseSqlite("Data Source=quickstart-audit.db"),
        tokens => tokens.UseSqlite("Data Source=quickstart-tokens.db"));

    options.EnableJsonRedaction();
    options.EnableLoggingRedaction();
    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
    options.EnableValidation();
    options.EnableHealthChecks();
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseSensitiveFlow();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>SensitiveFlow QuickStart</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; max-width: 920px; color: #172033; }
    main { display: grid; gap: 1rem; }
    form, section { border: 1px solid #d8dee9; border-radius: 8px; padding: 1rem; }
    label { display: grid; gap: .25rem; margin: .75rem 0; font-weight: 600; }
    input { padding: .65rem; border: 1px solid #b8c0cc; border-radius: 6px; font: inherit; }
    button { padding: .65rem .9rem; border: 0; border-radius: 6px; background: #175ddc; color: white; font-weight: 700; cursor: pointer; }
    pre { background: #111827; color: #d1fae5; padding: 1rem; border-radius: 8px; overflow: auto; min-height: 8rem; }
    .note { background: #fff7ed; border-color: #fed7aa; }
  </style>
</head>
<body>
<main>
  <h1>SensitiveFlow QuickStart</h1>
  <section class="note">
    <strong>Schema required.</strong> This sample does not create tables. Create/migrate the app, audit, and token databases before testing writes.
  </section>
  <form id="create">
    <h2>Create customer</h2>
    <label>Name <input name="name" value="Alice Example"></label>
    <label>Email <input name="email" value="alice@example.test"></label>
    <label>Tax ID <input name="taxId" value="12345678900"></label>
    <button type="submit">POST /customers</button>
  </form>
  <form id="get">
    <h2>Get customer</h2>
    <label>DataSubjectId <input name="id" placeholder="Paste returned dataSubjectId"></label>
    <button type="button" id="list">GET /customers</button>
    <button type="submit">GET /customers/{id}</button>
  </form>
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
document.querySelector('#create').addEventListener('submit', async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget));
  await show(await fetch('/customers', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(data)
  }));
});
document.querySelector('#get').addEventListener('submit', async event => {
  event.preventDefault();
  const id = new FormData(event.currentTarget).get('id')?.trim();
  if (!id) {
    output.textContent = 'Enter a DataSubjectId or use GET /customers first.';
    return;
  }
  await show(await fetch(`/customers/${encodeURIComponent(id)}`));
});
document.querySelector('#list').addEventListener('click', async () => {
  await show(await fetch('/customers'));
});
</script>
</body>
</html>
""", "text/html"));

app.MapGet("/customers", async (AppDbContext db, CancellationToken ct) =>
{
    var customers = await db.Customers
        .Take(100)
        .ToListAsync(ct);

    return Results.Ok(customers
        .OrderByDescending(c => c.CreatedAt)
        .Take(20));
});

app.MapPost("/customers", async (CreateCustomerRequest request, AppDbContext db, CancellationToken ct) =>
{
    var customer = new Customer
    {
        DataSubjectId = Guid.NewGuid().ToString(),
        Name = request.Name,
        Email = request.Email,
        TaxId = request.TaxId,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    db.Customers.Add(customer);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/customers/{customer.DataSubjectId}", customer);
});

app.MapGet("/customers/{id}", async (string id, AppDbContext db, CancellationToken ct) =>
{
    var customer = await db.Customers.FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});

app.Run();

public sealed record CreateCustomerRequest(string Name, string Email, string TaxId);
