# Anonymization, Masking, and Pseudonymization

`SensitiveFlow.Anonymization` provides three distinct levels of data protection, each with different legal implications under applicable regulations.

## Installation

```bash
dotnet add package SensitiveFlow.Anonymization
```

## The three levels — what each one means legally

| Technique | Reversible | Data remains personal? | Regulatory scope | Use when |
|-----------|-----------|------------------------|------------|----------|
| **Anonymization** | No | **No** | Usually out of scope | You no longer need to identify the subject |
| **Masking** | No | **Yes** | Still in scope | Reducing accidental exposure in UIs or logs |
| **Pseudonymization** | Yes (with store/key) | **Yes** | Still in scope | You need the mapping for operational reasons |

> **Important:** only anonymization can remove data from personal-data scope. Masking and pseudonymization are risk-reduction techniques — all privacy obligations continue to apply.

---

## Anonymization

Anonymizers implement `IAnonymizer`. The result carries no information that can re-identify the person — it may no longer be personal data, depending on context and re-identification risk.

### BrazilianTaxIdAnonymizer

Replaces every digit with `*`, preserving only punctuation. CPF and CNPJ have 11 and 14 digits respectively — with all digits masked and no remaining structure, re-identification is not feasible.

```csharp
var anon = new BrazilianTaxIdAnonymizer();

anon.Anonymize("123.456.789-09");      // "***.***.***-**"
anon.Anonymize("12.345.678/0001-95"); // "**.***.***/****-**"
```

Extension method:

```csharp
"123.456.789-09".AnonymizeTaxId();    // "***.***.***-**"
```

---

## Masking

Maskers implement `IMasker`. They reduce the identifiability of a value for display or logging, but **the result remains personal data** — the structure, length, or partial content retained is often sufficient for re-identification in context.

### EmailMasker

Keeps the first character of the local part and the full domain.

```csharp
var masker = new EmailMasker();

masker.Mask("joao.silva@example.com");  // "j*********@example.com"
masker.Mask("a@example.com");           // "*@example.com"
```

> Inputs with multiple `@` — `CanMask` returns `false` and the value is returned unchanged.

### PhoneMasker

Preserves the last two digits and the formatting structure.

```csharp
var masker = new PhoneMasker();

masker.Mask("(11) 99999-8877");         // "(**) *****-**77"
masker.Mask("+55 11 99999-8877");       // "+** ** *****-**77"
```

### NameMasker

Keeps the first letter of each word.

```csharp
var masker = new NameMasker();

masker.Mask("João da Silva");           // "J*** d* S****"
```

> For full removal of a name from personal-data scope, delete the field entirely or replace it with a `TokenPseudonymizer` token. Keeping the initial and word length can be sufficient to re-identify common names.

### Extension methods

```csharp
using SensitiveFlow.Anonymization.Extensions;

// Anonymization
string taxId = "123.456.789-09".AnonymizeTaxId();

// Masking — risk reduction, data remains personal
string email = "joao@example.com".MaskEmail();
string phone = "(11) 99999-8877".MaskPhone();
string name  = "João da Silva".MaskName();
```

> Shared instances are reused across calls — safe for high-throughput scenarios.

---

## IP addresses — why truncation is not anonymization

IP truncation (`192.168.1.42` → `192.168.1.0`) is a common practice but **does not constitute true anonymization** — a truncated IP combined with any other metadata can still single out an individual. Treat it as pseudonymization, not anonymization, and keep the same controls (audit, retention, access restrictions) you would apply to the raw value.

**The correct treatment for IP addresses:**

| Context | Suggested treatment | Notes |
|---------|--------------------|-------|
| Audit / security logs | **Pseudonymize** with `TokenPseudonymizer` | Keeps the value reversible for incident response without storing it in the clear |
| Analytics / reporting | Do not store, or collect only after explicit user opt-in | Aggregate counters are usually enough and avoid the problem entirely |

```csharp
// Pseudonymize IP before writing to the audit log
var ipToken = pseudonymizer.Pseudonymize(request.RemoteIpAddress?.ToString() ?? string.Empty);

var record = new AuditRecord
{
    DataSubjectId  = userId,
    Entity         = "Order",
    Field          = "Payment",
    Operation      = AuditOperation.Access,
    IpAddressToken = ipToken,   // opaque token — not the raw IP
};

// During a security investigation, resolve the original (TokenPseudonymizer only):
var originalIp = pseudonymizer.Reverse(ipToken);
```

---

## Pseudonymization

Pseudonymizers implement `IPseudonymizer`. They replace a value with a token. The original can be recovered given the right store or key, so the data remains personal and privacy obligations still apply.

### TokenPseudonymizer

Stable, reversible tokens backed by a persistent `ITokenStore`.

```csharp
// Production: inject a durable ITokenStore (SQL, Redis, etc.)
// Testing / single-session: use InMemoryTokenStore
var store  = new InMemoryTokenStore();
var pseudo = new TokenPseudonymizer(store);

var token     = pseudo.Pseudonymize("joao@example.com");
var recovered = pseudo.Reverse(token);
// recovered == "joao@example.com"
```

> **Important:** `InMemoryTokenStore` loses all mappings when the process exits. In production, implement `ITokenStore` backed by a durable store (SQL Server, Redis, etc.). Losing the store makes pseudonymized data irrecoverable.

#### Caching token mappings

If the durable token store is remote (SQL, Redis, etc.) and the same values are pseudonymized repeatedly, wrap it with the bounded in-process cache:

```csharp
builder.Services.AddTokenStore<SqlTokenStore>();
builder.Services.AddCachingTokenStore(options =>
{
    options.MaxEntries = 10_000;
});
```

`CachingTokenStore` caches both directions (`value -> token` and `token -> value`) to reduce repeated store roundtrips. It is only a performance layer: the inner `ITokenStore` must still be durable and authoritative.

> **Trade-off:** the cache stores original values in process memory. Size it deliberately, and use a custom distributed or encrypted decorator if your threat model does not allow this.

#### Implementing ITokenStore

```csharp
public class SqlTokenStore : ITokenStore
{
    public Task<string> GetOrCreateTokenAsync(string value, CancellationToken ct = default)
    {
        // INSERT OR IGNORE + SELECT from your tokens table
        throw new NotImplementedException();
    }

    public Task<string> ResolveTokenAsync(string token, CancellationToken ct = default)
    {
        // SELECT original FROM tokens WHERE token = @token
        // Throw KeyNotFoundException if not found
        throw new NotImplementedException();
    }
}
```

### HmacPseudonymizer

Deterministic HMAC-SHA256: same input + same key = same token, without a mapping table. Not reversible through `Reverse()`.

```csharp
// Secret key must be at least 32 characters
var pseudo = new HmacPseudonymizer("my-secret-key-for-hmac-32-bytes!!");

var token = pseudo.Pseudonymize("joao@example.com");
// pseudo.Reverse(token) throws NotSupportedException
```

> Use `HmacPseudonymizer` for consistent join keys across systems that share the secret. Use `TokenPseudonymizer` for true reversibility.

---

## Masking Strategies

Strategies implement `IMaskStrategy` — composable one-way transforms for custom pipelines.

### RedactionStrategy

Replaces the entire value with a fixed marker.

```csharp
new RedactionStrategy().Apply("any value");       // "[REDACTED]"
new RedactionStrategy("***").Apply("any value");  // "***"
```

### HashStrategy

SHA-256 one-way hash, optionally salted. Salt must be at least 16 characters.

```csharp
new HashStrategy().Apply("value");
// deterministic 64-char hex string

new HashStrategy("my-fixed-salt-16ch").Apply("value");
// different hash, same determinism
```

> Always use a salt in production. Without a salt, identical values produce identical hashes — a lookup table can reverse common inputs such as CPF numbers.

---

## Data subject export (portability)

By default, data-subject export returns raw annotated values because portability responses often need to disclose the data back to the subject. To protect specific export fields, use contextual redaction:

```csharp
[PersonalData(Category = DataCategory.Contact)]
[Redaction(Export = OutputRedactionAction.Mask)]
public string Email { get; set; } = string.Empty;

[SensitiveData(Category = SensitiveDataCategory.Other)]
[Redaction(Export = OutputRedactionAction.Omit)]
public string InternalRiskNote { get; set; } = string.Empty;
```

`IDataSubjectExporter` is the read-side counterpart of `IDataSubjectErasureService`. Given an entity, it returns a dictionary keyed by property name with every annotated value (`[PersonalData]`, `[SensitiveData]`, `[RetentionData]`) — useful for satisfying portability requests where a user asks for a copy of the personal data the application holds about them.

```csharp
services.AddDataSubjectExport();

// later
var exporter = sp.GetRequiredService<IDataSubjectExporter>();

var customer = await db.Customers.SingleAsync(c => c.DataSubjectId == subjectId);
IReadOnlyDictionary<string, object?> snapshot = exporter.Export(customer);
// { "Name": "Maria", "Email": "maria@example.com", "TaxId": "12345678900" }
```

The exporter operates on individual entity instances. Building the cross-table view of "everything we know about this subject" is the application's responsibility — typically by querying each table for rows where the subject identifier matches and feeding each row through `Export`.

---

## Deterministic fingerprints (safe diffing)

`DeterministicFingerprint` produces a short HMAC-SHA256 token from a value so you can compare two values for equality without exposing them. Useful for:

- "Did this customer's e-mail change between save A and save B?"
- "Are these two records about the same person?" — same input always yields the same fingerprint
- Diff-style logs that need to show "this field changed" without leaking the value

```csharp
var fp = new DeterministicFingerprint(secretKey: configuration["Fingerprint:Key"]);

var before = fp.Fingerprint(snapshot.Email);
var after  = fp.Fingerprint(updated.Email);

if (before != after)
{
    _logger.LogInformation(
        "Customer {SubjectId} e-mail changed from {Before} to {After}",
        subjectId, before, after);
    // Logs: ... e-mail changed from 7e3a1d... to 9c45ff...
}

// Or use the helper
fp.AreEquivalent(snapshot.Email, updated.Email);
```

> **Not anonymization.** A determined attacker with the secret can still brute-force small input domains (booleans, low-cardinality enums). Apply the same access controls you would to the raw value, and rotate the key with care — rotation invalidates every previously-issued fingerprint.


