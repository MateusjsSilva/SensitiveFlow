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

## Advanced Features

### 1. Conditional Redaction by Role

Resolve the redaction context based on user claims or roles:

```csharp
var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] 
{ 
    new Claim(ClaimTypes.Role, "Admin") 
}));
var resolver = new ClaimsPrincipalRedactionContextResolver(principal);
var context = resolver.ResolveContext(); // Returns RedactionContext.AdminView

var options = new JsonRedactionOptions 
{ 
    ContextResolver = resolver 
};
```

Built-in mappings:
- "Admin" role → `RedactionContext.AdminView` (no redaction)
- "Support" role → `RedactionContext.SupportView` (partial redaction)
- "Customer" role → `RedactionContext.CustomerView` (heavy redaction)
- No matching role → `RedactionContext.ApiResponse` (default)

### 2. Custom Masking Strategies

Register pluggable masking strategies for flexible field masking:

```csharp
var registry = new JsonMaskingStrategyRegistry();

// Use built-in strategies
var emailMasked = registry.GetStrategy("email")?.Mask("john@example.com");

// Register custom strategy
registry.Register("custom-ssn", new CustomSsnMaskingStrategy());
var ssnMasked = registry.GetStrategy("custom-ssn")?.Mask("123-45-6789");

var options = new JsonRedactionOptions 
{ 
    MaskingStrategies = registry 
};
```

Built-in strategies: `email`, `phone`, `creditcard`, `ssn`, `ipaddress`

### 3. Lazy Redaction

Defer masking until actual serialization occurs. Beneficial for large object graphs:

```csharp
var wrapper = new LazyRedactionWrapper<string>(
    "secret-value",
    value => value?.ToUpper() ?? string.Empty
);

// Value not masked until ToString() is called
var masked = wrapper.ToString(); // "SECRET-VALUE"
var isResolved = wrapper.IsResolved; // true

var options = new JsonRedactionOptions 
{ 
    EnableLazyRedaction = true 
};
```

### 4. Schema Stripping for OpenAPI

Identify sensitive properties in a type for documentation:

```csharp
var redactedProps = SensitiveDataSchemaFilter.GetRedactedProperties(
    typeof(Customer), 
    RedactionContext.ApiResponse
);

var sensitiveProps = SensitiveDataSchemaFilter.GetSensitiveProperties(typeof(Customer));

var omittedProps = SensitiveDataSchemaFilter.GetOmittedProperties(typeof(Customer));

// Integrate into your ISchemaFilter
public void Apply(OpenApiSchema schema, SchemaFilterContext context)
{
    var omitted = SensitiveDataSchemaFilter.GetOmittedProperties(context.Type);
    foreach (var prop in omitted)
    {
        schema.Properties.Remove(prop);
    }
}
```

### 5. Performance Metrics

Track JSON redaction operations via OpenTelemetry:

```csharp
var collector = new JsonRedactionMetricsCollector();

collector.RecordRedaction("Email", OutputRedactionAction.Mask, RedactionContext.ApiResponse);
collector.RecordPropertySerialized();
collector.RecordRedactionDuration(1.5); // milliseconds

var options = new JsonRedactionOptions 
{ 
    MetricsCollector = collector 
};

// Metrics exported:
// - sensitiveflow_json_redaction_total{property_name, action, context}
// - sensitiveflow_json_properties_serialized_total
// - sensitiveflow_json_redaction_duration_ms histogram
```
