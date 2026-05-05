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

IP truncation (`192.168.1.42` → `192.168.1.0`) is a common practice but **does not constitute anonymization** under many privacy frameworks and GDPR Recital 49. The CNIL ruled that truncated IPs combined with any metadata remain personal data. The EDPB requires anonymization to pass three cumulative tests (singling out, linkability, inference) — truncation fails all three.

**The correct treatment for IP addresses:**

| Context | Correct treatment | Legal basis |
|---------|------------------|-------------|
| Audit / security logs | **Pseudonymize** with `TokenPseudonymizer` | Legitimate interest or equivalent basis |
| Analytics / reporting | Do not store, or collect only with consent | Consent or equivalent basis |

```csharp
// Pseudonymize IP before writing to the audit log
var ipToken = await pseudonymizer.PseudonymizeAsync(request.RemoteIpAddress?.ToString());

var record = new AuditRecord
{
    DataSubjectId  = userId,
    Entity         = "Order",
    Field          = "Payment",
    Operation      = AuditOperation.Access,
    IpAddressToken = ipToken,   // opaque token — not the raw IP
};

// During a security investigation, resolve the original:
var originalIp = await pseudonymizer.ReverseAsync(ipToken);
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

var token     = await pseudo.PseudonymizeAsync("joao@example.com");
var recovered = await pseudo.ReverseAsync(token);
// recovered == "joao@example.com"
```

> **Important:** `InMemoryTokenStore` loses all mappings when the process exits. In production, implement `ITokenStore` backed by a durable store (SQL Server, Redis, etc.). Losing the store makes pseudonymized data irrecoverable.

#### Implementing ITokenStore

```csharp
public class SqlTokenStore : ITokenStore
{
    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken ct = default)
    {
        // INSERT OR IGNORE + SELECT from your tokens table
    }

    public async Task<string> ResolveTokenAsync(string token, CancellationToken ct = default)
    {
        // SELECT original FROM tokens WHERE token = @token
        // Throw KeyNotFoundException if not found
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


