# DTO Pattern with JSON Redaction

When returning Data Transfer Objects (DTOs) from your API endpoints, SensitiveFlow's JSON redaction middleware requires **explicit annotation** on the DTO properties to mask sensitive data.

## Why Annotate DTOs?

The `SensitiveJsonModifier` processes types **at serialization time**. When your endpoint returns a `CustomerResponse` DTO, the modifier only knows about the properties on that DTO class — it has no information about the original `Customer` entity you're mapping from.

```csharp
// Entity (domain model)
public class Customer
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }  // ← Known to be sensitive
}

// DTO (response model)
public class CustomerResponse
{
    public string Email { get; set; }  // ← No annotation = not masked
}

// Result: GET /customers returns unmasked email ❌
```

## Solution: Annotate the DTO

Replicate the sensitive data annotations from your entity to the DTO:

```csharp
public class CustomerResponse
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }  // ← Now masked ✓
}
```

## Example: Complete Pattern

**Entity (domain model):**
```csharp
public class Customer
{
    public int Id { get; set; }

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; }

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; }

    [PersonalData(Category = DataCategory.Contact)]
    public string Phone { get; set; }
}
```

**DTO (response model):**
```csharp
public sealed record CustomerResponse(
    string DataSubjectId,

    [property: PersonalData(Category = DataCategory.Identification)]
    string Name,

    [property: PersonalData(Category = DataCategory.Contact)]
    string Email,

    [property: PersonalData(Category = DataCategory.Contact)]
    string Phone);
```

**Endpoint:**
```csharp
app.MapGet("/customers/{id}", async (
    string id,
    SampleDbContext db,
    CancellationToken ct) =>
{
    var customer = await db.Customers
        .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

    if (customer is null)
        return Results.NotFound();

    var response = new CustomerResponse(
        customer.DataSubjectId,
        customer.Name,
        customer.Email,
        customer.Phone);

    return Results.Ok(response);  // ← Email, Name, Phone are masked
})
.WithName("GetCustomer");
```

## With Records

When using `record` types, apply annotations to the property declaration:

```csharp
public sealed record CustomerResponse(
    string DataSubjectId,
    [property: PersonalData(Category = DataCategory.Contact)]
    string Email);
```

The `[property: ...]` syntax ensures the attribute applies to the generated property, not the constructor parameter.

## Security Note

**Forgetting to annotate DTO properties is the most common cause of data leaks.** The library cannot automatically infer which DTO properties correspond to sensitive entity properties — this requires explicit declaration by the developer.

Treat DTO annotations as part of your security model, just like endpoint authorization.

## See Also

- [Attributes Reference](attributes.md) — Full list of `[PersonalData]`, `[SensitiveData]`, and redaction options
- [JSON Redaction](json.md) — Configuration and behavior
- [ASP.NET Core Integration](aspnetcore.md) — How redaction fits into the request/response pipeline
