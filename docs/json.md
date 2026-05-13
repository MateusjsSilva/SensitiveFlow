# JSON redaction

`SensitiveFlow.Json` plugs into `System.Text.Json` so that properties annotated with `[PersonalData]` or `[SensitiveData]` are automatically masked, replaced, or omitted whenever the type is serialized — including HTTP responses returned by ASP.NET Core, logs that serialize objects, and any direct call to `JsonSerializer.Serialize`.

The middleware in `SensitiveFlow.Logging` covers log lines. This package covers the response body itself.

## Install

```bash
dotnet add package SensitiveFlow.Json
```

## Quick start

```csharp
using System.Text.Json;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Json.Extensions;

var options = new JsonSerializerOptions().WithSensitiveDataRedaction(
    new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask });

var json = JsonSerializer.Serialize(customer, options);
// {"Name":"A****","Email":"a****@example.com","Id":42}
```

## Modes

| Mode | Behavior | When to use |
|------|----------|-------------|
| `Mask` (default) | Partial mask that preserves the value's general shape (`alice@x.com` → `a****@x.com`) | UI fields where users need a hint of the value |
| `Redacted` | Replace with a placeholder (default `[REDACTED]`) | Internal APIs where the original shape is irrelevant |
| `Omit` | Remove the property from the payload entirely | Most secure; use when clients don't depend on the key being present |
| `None` | Emit the raw value | Per-property override only — use sparingly |

The mask heuristic recognizes property names containing `Email`, `Phone`, and `Name` and applies the matching masker from `SensitiveFlow.Anonymization`. Anything else falls back to a generic "first character + asterisks" mask.

## Non-string values

Sensitive numeric, date, boolean, and collection properties cannot be safely "partially masked" without either changing their JSON type or leaking information. `SensitiveFlow.Json` therefore lets you choose how annotated non-string values are represented:

| Option | Output | Security trade-off | Compatibility trade-off |
|--------|--------|--------------------|-------------------------|
| `Null` (default) | `"Salary": null` | Does not emit fake values or leak magnitude | Clients must accept nullable values |
| `Placeholder` | `"Salary": "[NUMBER_REDACTED]"` | Explicit and unambiguous | Changes JSON type from number/date/bool to string |
| `Omit` | property is removed | Strictest option | Clients that expect the key may break |

Configure it with `JsonRedactionOptions.NonStringRedactionMode`:

```csharp
builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.WithSensitiveDataRedaction(new JsonRedactionOptions
    {
        DefaultMode = JsonRedactionMode.Mask,
        NonStringRedactionMode = JsonNonStringRedactionMode.Null // default
    }));
```

For highly sensitive numeric values such as salary, account balance, credit limits, or tax rates, avoid magnitude-preserving masks such as `25K` or `25***`: they still reveal useful information. Prefer `Null`, `Placeholder`, or `Omit`.

## Dynamic dictionaries

Attributes do not exist on individual dynamic entries inside `Dictionary<string, object?>`, so SensitiveFlow does not redact dictionary values by key name. This avoids surprising consumers: an `email` key may be public in one endpoint (for example, a seller contact) and personal data in another.

If a dynamic payload is sensitive, mark the dictionary property itself:

```csharp
public class CustomerMetadataResponse
{
    [SensitiveData(Category = SensitiveDataCategory.Other)]
    public Dictionary<string, object?> Metadata { get; set; } = [];
}
```

With the default non-string mode, the whole dictionary serializes as `null`. With `JsonNonStringRedactionMode.Omit`, the property is removed. Prefer typed DTOs with `[PersonalData]` / `[SensitiveData]` whenever possible when only some fields in the dynamic shape are sensitive.

## Per-property overrides

`[JsonRedaction]` overrides the global default for a single property:

```csharp
public class Customer
{
    [PersonalData(Category = DataCategory.Identification)]
    [JsonRedaction(JsonRedactionMode.Omit)]   // never serialize this field
    public string FullName { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    [JsonRedaction(JsonRedactionMode.Mask)]   // explicit even when global default is the same
    public string Email { get; set; } = string.Empty;
}
```

For generic masks, you can preserve a specific number of leading characters on a property:

```csharp
public class PaymentResponse
{
    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [JsonRedaction(RedactionMode = JsonRedactionMode.Mask, PreservePrefixLength = 3)]
    public string InternalToken { get; set; } = string.Empty;
    // "abcdef12345" -> "abc********"
}
```

Use a small prefix only when revealing those characters is acceptable for the domain. For salaries, balances, birth dates, tax rates, and other highly inferable values, prefer non-string `Null`, `Placeholder`, or `Omit` instead of prefix-preserving masks.

## ASP.NET Core integration

Wire the modifier into the JSON options used by your controllers / minimal APIs:

```csharp
builder.Services.AddSensitiveFlowJsonRedaction(opt => opt.DefaultMode = JsonRedactionMode.Mask);

builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.WithSensitiveDataRedaction(
        new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask }));
```

For controllers using Newtonsoft.Json, you'll need a separate adapter — this package targets `System.Text.Json` only.

## What this package does NOT do

- It does not redact values during **deserialization** — incoming requests must validate sensitive fields with your own logic.
- It does not encrypt anything; it just rewrites property values during serialization.
- It does not affect log redaction. Use `SensitiveFlow.Logging` for that.
- It does not inspect already-serialized JSON strings returned via `Results.Text`, `ContentResult`, files, queues, or cache writes. Serialize with configured `JsonSerializerOptions` or return typed objects/DTOs.
- It does not integrate with Newtonsoft.Json. Use `System.Text.Json` or a separate Newtonsoft adapter.

## Using with DTOs

When you return a DTO (Data Transfer Object) from an endpoint, you must explicitly annotate the DTO properties that correspond to sensitive entity fields. The library only sees the type being serialized and applies redaction based on its annotations.

```csharp
// Entity (domain model)
public class Customer
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}

// DTO (response model) - must replicate annotations
public class CustomerResponse
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }  // ← Now masked in the response
}
```

See [DTO Pattern](dto-pattern.md) for the complete pattern and best practices.
