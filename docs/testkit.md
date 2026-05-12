# TestKit

`SensitiveFlow.TestKit` ships test-time helpers so your test suite catches privacy regressions before they ship.

## Conformance suites

If you write a custom `IAuditStore` or `ITokenStore` implementation, inherit the conformance base class to get a baseline of expected behavior verified for free.

```csharp
public sealed class MyAuditStoreContractTests : AuditStoreContractTests
{
    protected override Task<IAuditStore> CreateStoreAsync()
        => Task.FromResult<IAuditStore>(new MyAuditStore(...));
}
```

The base class runs a fixed set of `[Fact]` tests against your implementation: append-then-query, time-range filtering, pagination, query-by-subject, etc. If your store passes, downstream packages (`SensitiveFlow.EFCore`, etc.) will work against it.

The same applies for `TokenStoreContractTests` — inherit and override `CreateStoreAsync` with your `ITokenStore` implementation.

## SensitiveDataAssert.DoesNotLeak

Catches the most common privacy regression: a refactor that removes redaction without anyone noticing. The assertion fails the test if any sensitive value of an entity appears verbatim inside a string.

```csharp
using SensitiveFlow.TestKit.Assertions;

[Fact]
public async Task GetCustomer_DoesNotReturnRawEmail()
{
    var customer = new Customer { Name = "Alice", Email = "alice@example.com" };
    await SeedAsync(customer);

    var response = await _client.GetAsync($"/customers/{customer.Id}");
    var body = await response.Content.ReadAsStringAsync();

    SensitiveDataAssert.DoesNotLeak(body, customer);
}
```

The assertion walks every property annotated with `[PersonalData]` or `[SensitiveData]` on the supplied entity and checks the payload string for an exact substring match. Matches throw `XunitException` listing every leaked property.

The overload that accepts multiple entities is useful when a request involves more than one record:

```csharp
SensitiveDataAssert.DoesNotLeak(logSink.Output, request, loadedEntity, currentUser);
```

### What this catches

- A new `JsonConverter` that forgot to apply the `WithSensitiveDataRedaction` modifier
- A debug `_logger.LogInformation("payload: {@Payload}", customer)` that bypassed the redactor
- A copy-paste of an internal DTO into a public response shape
- A serializer registered globally without going through DI

### What this does not catch

- Partial leaks (e.g. only the local part of an e-mail)
- Pseudonymized values (the substring is not the original)
- Values that never lived on the entity in the first place (free-form notes, computed fields)

For those, write specific assertions on the field values you expect to see.

## SensitiveDataAssert.DoesNotContainAny

Checks a payload against explicit string values — no entity or annotation needed. Use this when you know exactly which values should not leak.

```csharp
using SensitiveFlow.TestKit.Assertions;

[Fact]
public async Task GetCustomer_DoesNotLeakEmailOrPhone()
{
    var customer = new Customer { Email = "alice@example.com", Phone = "555-1234" };
    await SeedAsync(customer);

    var response = await _client.GetAsync($"/customers/{customer.Id}");
    var body = await response.Content.ReadAsStringAsync();

    SensitiveDataAssert.DoesNotContainAny(body, customer.Email, customer.Phone);
}
```

Empty and null values are silently skipped — they would otherwise match every string.

## SensitiveDataAssert.DoesNotLeakKnownValues

Same as `DoesNotContainAny` but accepts an `IEnumerable<string>` for readability when you already have a collection of known sensitive values.

```csharp
var knownValues = new[] { customer.Email, customer.Phone, customer.TaxId };
SensitiveDataAssert.DoesNotLeakKnownValues(body, knownValues);
```

## Expanded contract suites

Beyond `AuditStoreContractTests` and `TokenStoreContractTests`, TestKit now includes:

- `AuditSnapshotStoreContractTests`
- `PseudonymizerContractTests`
- `MaskerContractTests`
- `AnonymizerContractTests`
- `RetentionExpirationHandlerContractTests`

## Additional assertions

```csharp
SensitiveDataAssert.ContainsMaskedEmail(body);
SensitiveDataAssert.DoesNotContainRawValues(body, customer);
SensitiveDataAssert.JsonDoesNotExposeAnnotatedProperties(body, typeof(Customer));
SensitiveDataAssert.LogsDoNotContainSensitiveValues(logSink.Output, knownValues);
```
