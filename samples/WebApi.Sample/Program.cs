// ---------------------------------------------
// SensitiveFlow - Web API (MVC Controllers) Sample
//
// Demonstrates SensitiveFlow in a controller-based ASP.NET Core application:
//   - [PersonalData] / [SensitiveData] on model properties
//   - Middleware that pseudonymizes the remote IP before it reaches any controller
//   - IAuditStore receiving AuditRecords via IAuditContext
//   - ILogger redaction stripping sensitive values from structured logs
//   - Masking in responses so raw PII never leaves the API boundary
//
// IMPORTANT: This sample uses a stub IAuditStore that discards records.
// In production, replace it with an implementation backed by a durable database
// (SQL via EF Core, MongoDB, etc.) using:
//   builder.Services.AddAuditStore<YourDurableAuditStore>();
// ---------------------------------------------

using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.Logging.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// --- SensitiveFlow: audit store (replace with a durable store in production) ---
builder.Services.AddAuditStore<NullAuditStore>();

// --- SensitiveFlow: token store + pseudonymizer (replace with a durable store in production) ---
builder.Services.AddTokenStore<NullTokenStore>();

// --- SensitiveFlow: EF Core interceptor + ASP.NET Core audit context ---
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAspNetCore();

// --- SensitiveFlow: structured log redaction ---
builder.Services.AddSensitiveFlowLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// UseSensitiveFlowAudit must come before UseAuthentication so the IP token
// is available for every downstream controller.
app.UseSensitiveFlowAudit();

app.UseAuthorization();
app.MapControllers();

app.Run();

// -----------------------------------------------------------------------
// Stubs — replace both with durable implementations in production.
// -----------------------------------------------------------------------

public sealed class NullAuditStore : IAuditStore
{
    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuditRecord>>([]);

    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuditRecord>>([]);
}

public sealed class NullTokenStore : ITokenStore
{
    public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(token);
}
