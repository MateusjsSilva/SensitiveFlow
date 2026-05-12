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
| `ActorId` | `User.FindFirst("sub")?.Value` → `User.FindFirst(ClaimTypes.NameIdentifier)?.Value` → `User.Identity.Name` |
| `IpAddressToken` | `HttpContext.Items["SensitiveFlow.IpToken"]` (set by the middleware) |

Both properties return `null` when no HTTP context is active (e.g., background jobs).

> **JWT `sub` claim mapping.** `JwtBearerOptions.MapInboundClaims` defaults to `true`, which renames the `sub` claim to `ClaimTypes.NameIdentifier` before it reaches the principal. The double-check above means `ActorId` resolves correctly under both default and `MapInboundClaims = false` configurations — you do not have to disable claim mapping to use this library.

## Behind a reverse proxy / load balancer

The middleware reads `HttpContext.Connection.RemoteIpAddress`, which by default is the IP of the immediate caller — typically your load balancer or reverse proxy, not the real client. Configure ASP.NET Core's [forwarded headers middleware](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer) **before** `UseSensitiveFlowAudit` so the original client IP is restored:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseSensitiveFlowAudit();
```

Without this, every audit record's `IpAddressToken` will resolve back to the proxy's address.

## IP pseudonymization

The middleware depends on `IPseudonymizer` from `SensitiveFlow.Core`. Register a concrete implementation — for example, `HmacPseudonymizer` from `SensitiveFlow.Anonymization`:

```csharp
builder.Services.AddSingleton<IPseudonymizer>(
    new HmacPseudonymizer(secretKey: configuration["SensitiveFlow:HmacKey"]!));

builder.Services.AddSensitiveFlowAspNetCore();
```

## Combining with EF Core

```csharp
// Register your durable IAuditStore and ITokenStore.
builder.Services.AddEfCoreAuditStore<AppDbContext>();
builder.Services.AddTokenStore<EfCoreTokenStore>();

// Register interceptor (NullAuditContext as default, replaced below).
builder.Services.AddSensitiveFlowEFCore();

// Replace NullAuditContext with the HTTP-aware implementation.
builder.Services.AddSensitiveFlowAspNetCore();

// Wire the interceptor into your DbContext.
builder.Services.AddDbContext<AppDbContext>((sp, opts) =>
{
    opts.UseSqlServer(connectionString);
    opts.AddInterceptors(sp.GetRequiredService<SensitiveDataAuditInterceptor>());
});
```

## Response DTOs and JSON Redaction

When endpoints return DTOs instead of entities, enable JSON redaction at the response middleware level and annotate DTO properties with `[PersonalData]` or `[SensitiveData]` to match the entity annotations:

```csharp
builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.WithSensitiveDataRedaction(
        new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask }));
```

See [DTO Pattern](dto-pattern.md) for complete examples and best practices.

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
