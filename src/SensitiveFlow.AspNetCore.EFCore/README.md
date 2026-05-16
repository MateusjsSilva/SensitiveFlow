# SensitiveFlow.AspNetCore.EFCore

Automatic response redaction for ASP.NET Core HTTP responses using EF Core integration.

## Main Components

### Response Redaction Filter
- **`RedactingResultFilter`** — ActionFilter for controllers
  - Intercepts response serialization
  - Applies `[Redaction(ApiResponse=...)]` per field
  - Transparent to controller code

### Endpoint Configuration
- **`AddSensitiveFlowResponseRedaction()`** — Registers filter globally

## How It Works

```
Controller returns entity
    ↓
RedactingResultFilter intercepts
    ↓
Checks all properties for [PersonalData]
    ↓
Applies [Redaction(ApiResponse=...)]
    ↓
Serializes with redaction
    ↓
Client receives redacted response
```

## Usage

### Registration
```csharp
builder.Services.AddControllers()
    .AddSensitiveFlowResponseRedaction();
```

### Automatic Redaction
```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerResponse>> Get(string id)
    {
        var customer = await _db.Customers.FindAsync(id);
        // Email and Phone automatically redacted in response
        return Ok(new CustomerResponse(customer));
    }
}
```

### Configuration Per Endpoint
```csharp
[HttpGet("{id}")]
[SensitiveFlowRedaction(OutputRedactionAction.Omit)]  // Remove field entirely
public ActionResult Get(string id) { ... }
```

## Redaction Contexts

Each endpoint can have different redaction:

```csharp
[PersonalData]
[Redaction(
    ApiResponse = OutputRedactionAction.Omit,  // Not returned to client
    Export = OutputRedactionAction.None,  // Full value in export
    Logs = OutputRedactionAction.Mask,  // Partially masked in logs
    Audit = OutputRedactionAction.Mask  // Partially masked in audit
)]
public string Email { get; set; }
```

## Performance

- Filter runs per request (minimal overhead)
- Reflection cached via `SensitiveMemberCache`
- Zero allocation for non-sensitive responses

## Integration with Json Package

Works seamlessly with `SensitiveFlow.Json` converters:

```csharp
var settings = new JsonSerializerSettings();
settings.Converters.Add(
    new SensitiveDataNewtonsoftConverter(OutputRedactionAction.Mask)
);

// Both filter AND converter apply redaction (independent layers)
```

## Advanced Features

### DTO Mapping
Automatically map entities to non-sensitive DTOs to exclude sensitive fields entirely:

```csharp
builder.Services.AddSensitiveFlowDtoMapping(options =>
{
    options.MapEntity<Customer, CustomerDto>();
    options.MapEntity<User, UserDto>();
});

var mapper = sp.GetRequiredService<DtoMapper>();
var dto = mapper.Map(customer); // Returns CustomerDto with only mapped properties
```

**Components:**
- `DtoMappingOptions` — Register entity-to-DTO mappings
- `DtoMapper` — Maps entities using reflection-based property copying

### Role-Based Redaction
Apply different redaction levels based on user roles:

```csharp
builder.Services.AddSensitiveFlowRoleBasedRedaction(options =>
{
    options.DefaultMode = JsonRedactionMode.Mask;        // Default for all users
    options.ConfigureRole("admin", JsonRedactionMode.None);  // Admins see full data
    options.ConfigureRole("support", JsonRedactionMode.Mask); // Support sees masked data
});

var roleOptions = sp.GetRequiredService<RoleBasedRedactionOptions>();
var mode = roleOptions.GetModeForRoles(user.Roles);
```

**Components:**
- `RoleBasedRedactionOptions` — Configurable role-to-redaction-mode mapping
- `GetModeForRoles()` — Resolves redaction mode for user's roles (first match wins)

### Header Control
Allow clients to request specific redaction levels via HTTP headers:

```csharp
// Client sends: X-Redaction-Level: None (or Mask, Omit)
var request = context.Request;
var requestedMode = RedactionLevelHeader.TryExtractFromHeaders(request);

RedactionLevelHeader.StoreInContext(context, requestedMode);

// Later in middleware/filter:
var mode = RedactionLevelHeader.TryGetFromContext(context);
```

**Components:**
- `RedactionLevelHeader.TryExtractFromHeaders()` — Parses `X-Redaction-Level` header
- `RedactionLevelHeader.StoreInContext()` / `TryGetFromContext()` — Context storage

### Performance Metrics
Track redaction operation frequency and latency:

```csharp
var collector = sp.GetRequiredService<RedactionMetricsCollector>();

// Record a redaction operation
collector.RecordOperation("Email", fieldsAffected: 1, elapsedMilliseconds: 5);

// Query metrics
var emailMetrics = collector.GetMetric("Email");
Console.WriteLine($"Total redactions: {collector.TotalOperations}");
Console.WriteLine($"Avg time per operation: {collector.AverageTimeMs}ms");
```

**Components:**
- `RedactionMetricsCollector` — Thread-safe metrics collection
- `RedactionMetric` — Per-field metrics (count, total time, average time)
