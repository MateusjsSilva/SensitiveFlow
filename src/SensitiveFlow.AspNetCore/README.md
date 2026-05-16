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

## Advanced Features

### User Session Tracking
Track HTTP session IDs for request correlation in multi-request flows:

```csharp
builder.Services.AddSession(); // Session middleware
builder.Services.AddSensitiveFlowAspNetCore(options =>
{
    options.TrackSessionId = true; // Opt-in
});

app.UseSession(); // Must come before UseSensitiveFlowAudit
app.UseSensitiveFlowAudit();

// Later in HttpAuditContext:
var sessionId = auditContext.SessionId; // "session-abc123"
```

**Components:**
- `SessionIdExtractor` — Safe session ID extraction (no exceptions if sessions not configured)
- `HttpAuditContext.SessionId` — Virtual property for overriding behavior

### Request Correlation IDs
Propagate and generate correlation IDs for tracing requests across services:

```csharp
builder.Services.AddSensitiveFlowAspNetCore(options =>
{
    options.CorrelationId.HeaderName = "X-Correlation-ID";      // default
    options.CorrelationId.GenerateIfMissing = true;             // default
});

// Inbound request with X-Correlation-ID: abc-123
// or generated if missing: abcdef1234567890abcdef1234567890

var correlationId = auditContext.CorrelationId; // propagated to logs
```

**Components:**
- `CorrelationIdOptions` — Header name and auto-generation config
- `HttpAuditContext.CorrelationId` — Virtual property for reading

### Tenant Isolation
Extract tenant ID from claims or headers for multi-tenant audit scoping:

```csharp
builder.Services.AddSensitiveFlowAspNetCore(options =>
{
    options.Tenant.ClaimName = "tid";           // Azure AD tenant claim (or null to skip)
    options.Tenant.HeaderName = "X-Tenant-ID";  // fallback header
});

// Resolution: claim first, then header, then null
var tenantId = auditContext.TenantId; // "tenant-456"
```

**Components:**
- `TenantIdOptions` — Configurable claim name and header fallback
- `HttpAuditContext.TenantId` — Virtual property for reading

### Custom Claim Extraction
Replace hard-coded claim resolution with configurable lists:

```csharp
builder.Services.AddSensitiveFlowAspNetCore(options =>
{
    options.ActorId.ClaimNames = new[] { "oid", ClaimTypes.NameIdentifier, "sub" }
        .ToList();
});

// ActorId iterates in order: oid → NameIdentifier → sub → Identity.Name
var actorId = auditContext.ActorId;
```

**Components:**
- `ActorIdClaimOptions` — Ordered list of claim names to check
- `HttpAuditContext.ActorId` — Updated to use configurable claims

### IP Masking
Mask IP addresses instead of reversible pseudonymization for simpler privacy:

```csharp
builder.Services.AddSensitiveFlowAspNetCore(options =>
{
    options.IpMasking.Enabled = true;     // Enable masking
    options.IpMasking.MaskSuffix = "XXX"; // or custom suffix
});

// Inbound request from 192.168.1.42
// Stored as: 192.168.1.XXX (non-reversible, privacy-preserving)
// IPv6: fe80::1 → fe80::XXX
```

**Components:**
- `IpMaskingOptions` — Enabled flag and custom suffix
- `IpMaskingHelper` — IPv4/IPv6 masking logic
- `SensitiveFlowAuditMiddleware` — Uses masking instead of pseudonymizer when enabled
