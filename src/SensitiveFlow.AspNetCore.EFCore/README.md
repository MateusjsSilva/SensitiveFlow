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

## Possible Improvements

1. **DTO mapping** — Auto-map to non-sensitive DTOs
2. **Role-based redaction** — Different redaction per user role
3. **Header control** — Client specifies redaction level
4. **Performance metrics** — Track redaction frequency
