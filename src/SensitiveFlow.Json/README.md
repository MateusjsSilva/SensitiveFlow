# SensitiveFlow.Json

JSON serialization support with automatic sensitive data redaction for Newtonsoft.Json and System.Text.Json.

## Main Components

### Newtonsoft.Json Integration
- **`SensitiveDataNewtonsoftConverter`** — JsonConverter for Newtonsoft/Json.NET
  - Detects `[PersonalData]` and `[SensitiveData]` attributes
  - Applies `[Redaction(...Audit=OutputRedactionAction.*)]` per field
  - Integrates with `JsonSerializerSettings`

### System.Text.Json Integration
- **`SensitiveJsonModifier`** — JsonTypeInfo modifier for System.Text.Json
  - Works with source-generated JSON serializers
  - Applies redaction to properties during serialization
  - Zero allocation when no redaction needed

## How It Works

### Newtonsoft.Json Flow
1. Serializer encounters object with annotated properties
2. `SensitiveDataNewtonsoftConverter` intercepts
3. For each property, checks `[Redaction]` attributes
4. Applies action per context (ApiResponse, Logs, Export)
5. Writes redacted value to JSON output

### System.Text.Json Flow
1. Source-generated serializer created with type info
2. `SensitiveJsonModifier` wraps property converters
3. During serialization, modifiers intercept property values
4. Applies redaction based on `[Redaction]` attributes
5. Returns modified value to JSON writer

## Usage

### Newtonsoft.Json
```csharp
var settings = new JsonSerializerSettings();
settings.Converters.Add(new SensitiveDataNewtonsoftConverter(
    RedactionContext.ApiResponse
));

var json = JsonConvert.SerializeObject(customer, settings);
```

### System.Text.Json
```csharp
var context = new MyAppJsonSerializerContext(
    new JsonSerializerOptions(JsonSerializerDefaults.Web)
);

// Modify for sensitive data redaction
SensitiveJsonModifier.ApplyToContext(
    context,
    RedactionContext.ApiResponse
);

var json = JsonSerializer.Serialize(customer, context);
```

## Redaction Actions

- `OutputRedactionAction.None` — Include full value
- `OutputRedactionAction.Mask` — Partially masked (email: a****@x.com)
- `OutputRedactionAction.Redact` — Replace with `[REDACTED]`
- `OutputRedactionAction.Omit` — Exclude property entirely from JSON
- `OutputRedactionAction.Pseudonymize` — Replace with pseudonym token

## Context-Aware Redaction

Same property can have different redaction per context:

```csharp
[PersonalData]
[Redaction(
    ApiResponse = OutputRedactionAction.Mask,
    Logs = OutputRedactionAction.Redact,
    Export = OutputRedactionAction.None,  // Full value in DSAR
    Audit = OutputRedactionAction.Mask
)]
public string Email { get; set; }
```

When serializing for API response: Email is masked
When serializing for logs: Email is redacted
When serializing for data export: Full email included
In audit details: Email is masked

## Performance Considerations

### Newtonsoft.Json
- Reflection per serialization to find `[Redaction]` attributes
- Cached via `SensitiveMemberCache` to avoid repeated lookups
- Minimal overhead for non-sensitive properties (early skip)

### System.Text.Json
- Uses source generation (zero reflection)
- Modifiers add per-property interception (minimal allocation)
- Recommended for high-throughput scenarios

## Nested Objects

Both implementations handle nested sensitive objects:

```csharp
public class Order
{
    [PersonalData]
    public string CustomerEmail { get; set; }

    public List<LineItem> Items { get; set; }
}

public class LineItem
{
    [PersonalData]
    public string InternalNotes { get; set; }
    public decimal Price { get; set; }
}
```

Serialization respects redaction for all nested `[PersonalData]` properties.

## Null and Empty Handling

- Null values: Pass through as `null`
- Empty strings: Masked as `""`
- Omit action: Property key excluded entirely (not `null`)

## Possible Improvements

1. **Conditional redaction by role** — Allow `[Redaction]` to check user claims
2. **Custom masking strategies** — Pluggable per data type (email, phone, SSN)
3. **Lazy redaction** — Defer masking until serialization for large objects
4. **Schema stripping** — Option to remove sensitive properties from OpenAPI schema
5. **Performance metrics** — Built-in counters for redaction frequency
