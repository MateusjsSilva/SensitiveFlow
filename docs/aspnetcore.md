# ASP.NET Core Integration

`SensitiveFlow.AspNetCore` enriches the audit pipeline with HTTP request context: the current actor (from the JWT `sub` claim) and a pseudonymized IP token.

## Middleware: SensitiveFlowAuditMiddleware

The middleware runs early in the pipeline and:

1. Reads `HttpContext.Connection.RemoteIpAddress`.
2. Pseudonymizes it via `IPseudonymizer` — the raw IP is **never** stored.
3. Stores the token in `HttpContext.Items[SensitiveFlow.IpToken]`.

The pseudonymized token is then read by `HttpAuditContext` and attached to every `AuditRecord` produced during the request.

## Registration

```csharp
builder.Services.AddSensitiveFlowAspNetCore();
```

This registers:
- `IHttpContextAccessor`
- `HttpAuditContext` as scoped `IAuditContext`

Add the middleware before authentication:

```csharp
app.UseSensitiveFlowAudit();   // pseudonymizes IP
app.UseAuthentication();
app.UseAuthorization();
```

## HttpAuditContext

`HttpAuditContext` implements `IAuditContext` by reading from the current `HttpContext`:

| Property | Source |
|----------|--------|
| `ActorId` | `User.FindFirst("sub")?.Value`, falls back to `User.Identity.Name` |
| `IpAddressToken` | `HttpContext.Items["SensitiveFlow.IpToken"]` (set by the middleware) |

Both properties return `null` when no HTTP context is active (e.g., background jobs).

## IP pseudonymization

The middleware depends on `IPseudonymizer` from `SensitiveFlow.Core`. Register a concrete implementation — for example, `HmacPseudonymizer` from `SensitiveFlow.Anonymization`:

```csharp
builder.Services.AddSingleton<IPseudonymizer>(
    new HmacPseudonymizer(secretKey: configuration["SensitiveFlow:HmacKey"]!));

builder.Services.AddSensitiveFlowAspNetCore();
```

## Combining with EF Core

```csharp
// Register audit store
builder.Services.AddInMemoryAuditStore();

// Register interceptor with NullAuditContext as default
builder.Services.AddSensitiveFlowEFCore();

// Replace NullAuditContext with the HTTP-aware implementation
builder.Services.AddSensitiveFlowAspNetCore();

// Wire the interceptor
builder.Services.AddDbContext<AppDbContext>((sp, opts) =>
{
    opts.UseSqlServer(connectionString);
    opts.AddInterceptors(sp.GetRequiredService<SensitiveDataAuditInterceptor>());
});
```

## Testing

Use `Microsoft.AspNetCore.TestHost` to test middleware behaviour in isolation:

```csharp
using var host = new HostBuilder()
    .ConfigureWebHost(web =>
    {
        web.UseTestServer();
        web.ConfigureServices(services => services.AddSingleton<IPseudonymizer>(pseudonymizer));
        web.Configure(app =>
        {
            app.UseMiddleware<SensitiveFlowAuditMiddleware>();
            app.Run(ctx =>
            {
                var token = ctx.Items[SensitiveFlowAuditMiddleware.IpTokenKey] as string;
                // assert token here
                return Task.CompletedTask;
            });
        });
    })
    .Build();
```
