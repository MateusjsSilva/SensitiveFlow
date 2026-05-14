# SensitiveFlow.AspNetCore

ASP.NET Core integration providing HTTP context-aware audit information and response redaction.

## Main Components

### Audit Context
- **`HttpAuditContext`** — Extracts audit context from HttpContext
  - `ActorId`: From `User.FindFirst(ClaimTypes.NameIdentifier)` or `User.Identity.Name`
  - `IpAddressToken`: From `HttpContext.Connection.RemoteIpAddress`
  - Automatically registered when calling `AddSensitiveFlowAspNetCore()`

### Configuration
- **`AddSensitiveFlowAspNetCore()`** — Registers HttpAuditContext, replaces NullAuditContext
- Works with any authentication scheme (JWT, Cookie, OAuth, etc.)

## How It Works

### Audit Extraction
```
HTTP Request arrives
    ↓
HttpAuditContext reads User claims + IP
    ↓
SensitiveDataAuditInterceptor uses context values
    ↓
AuditRecord includes:
- ActorId: "user-123" (from claims)
- IpAddressToken: "192.168.1.100" (from RemoteIpAddress)
```

## Usage

### Registration
```csharp
builder.Services
    .AddAuthentication(/* your scheme */)
    .AddJwtBearer(/* config */);

builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAspNetCore(); // Replaces NullAuditContext
```

### Result
Audit records automatically include user identity and IP:

```json
{
  "dataSubjectId": "user-123",
  "entity": "Customer",
  "field": "Email",
  "operation": "Update",
  "timestamp": "2024-01-15T10:30:00Z",
  "actorId": "admin-456",
  "ipAddressToken": "192.168.1.100",
  "details": "Audit redaction action: Mask; value: a****@x.com."
}
```

## ActorId Resolution

Precedence (first match wins):
1. `User.FindFirst(ClaimTypes.NameIdentifier)?.Value`
2. `User.Identity.Name`
3. `null` (anonymous)

Configure via custom `IAuditContext`:

```csharp
public sealed class CustomAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomAuditContext(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public string? ActorId =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("custom-user-id")?.Value;

    public string? IpAddressToken =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

builder.Services.AddScoped<IAuditContext, CustomAuditContext>();
```

## IP Address Extraction

### Standard Configuration
```csharp
// HttpContext.Connection.RemoteIpAddress
// Reliable in most scenarios
```

### Behind Proxy (X-Forwarded-For)
```csharp
public sealed class ProxyAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _http;

    public ProxyAuditContext(IHttpContextAccessor http) => _http = http;

    public string? IpAddressToken =>
        _http.HttpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault()
        ?? _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? ActorId => _http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

builder.Services.AddScoped<IAuditContext, ProxyAuditContext>();
```

### Cloudflare/CDN
```csharp
// Read: CF-Connecting-IP (Cloudflare)
public string? IpAddressToken =>
    _http.HttpContext?.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
    ?? _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
```

## Response Redaction

See `SensitiveFlow.AspNetCore.EFCore` for automatic response envelope redaction.

## Middleware Integration

`HttpAuditContext` is automatically scoped per-request:

```csharp
app.Use(async (context, next) =>
{
    // HttpAuditContext available here via DI
    await next();
});
```

## Anonymous User Handling

- **Anonymous access**: `ActorId` = `null`, `IpAddressToken` = client IP
- **Audit still recorded**: Supports data export and analysis by IP
- **Recommendation**: Require authentication for data-modifying operations (via `[Authorize]`)

## Possible Improvements

1. **User session tracking** — Include SessionId for request correlation
2. **Request correlation IDs** — X-Correlation-ID propagation
3. **Tenant isolation** — Multi-tenant ActorId scoping
4. **Custom claim extraction** — Configurable claim names per deployment
5. **IP masking** — Last octet masking for privacy (192.168.1.XXX)
