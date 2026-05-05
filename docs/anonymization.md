# Anonymization and Pseudonymization

`LGPD.NET.Anonymization` provides anonymizers, pseudonymizers, and masking strategies that can be applied to personal data fields.

## Installation

```bash
dotnet add package LGPD.NET.Anonymization
```

## Art. 12 — The distinction that matters

| Technique | Reversible | Data remains personal? | LGPD scope |
|-----------|-----------|------------------------|------------|
| Anonymization | No | No | **Removed** from scope (Art. 12) |
| Pseudonymization | Yes (with the store/key) | **Yes** | Still in scope (Art. 12, §3) |

> Use anonymization when you no longer need to identify the data subject.
> Use pseudonymization when you need the mapping for operational reasons (e.g. joins, audit trails).

---

## Anonymizers

Anonymizers transform a value irreversibly. The result carries no information that can re-identify the person.

### BrazilianTaxIdAnonymizer

Masks CPF and CNPJ digits, preserving punctuation.

```csharp
var anon = new BrazilianTaxIdAnonymizer();

anon.Anonymize("123.456.789-09");          // "***.***.***-**"
anon.Anonymize("12.345.678/0001-95");      // "**.***.***/****-**"
```

### EmailAnonymizer

Keeps the first character of the local part and the full domain.

```csharp
var anon = new EmailAnonymizer();

anon.Anonymize("joao.silva@example.com");  // "j*********@example.com"
anon.Anonymize("a@example.com");           // "*@example.com"
```

> Inputs with multiple `@` symbols are not valid e-mails — `CanAnonymize` returns `false` and the value is returned unchanged.

### PhoneAnonymizer

Preserves the last two digits and masks the rest.

```csharp
var anon = new PhoneAnonymizer();

anon.Anonymize("(11) 99999-8877");         // "(**) *****-**77"
anon.Anonymize("+55 11 99999-8877");       // "+** ** *****-**77"
```

### NameAnonymizer

Keeps the first letter of each word and masks the rest.

```csharp
var anon = new NameAnonymizer();

anon.Anonymize("João da Silva");           // "J*** d* S****"
```

### Extension methods

For one-off operations, use the `StringAnonymizationExtensions` convenience methods. They reuse shared static instances, so regex compilation cost is paid only once.

```csharp
using LGPD.NET.Anonymization.Extensions;

string name  = "João da Silva".AnonymizeName();
string taxId = "123.456.789-09".AnonymizeTaxId();
string email = "joao@example.com".AnonymizeEmail();
string phone = "(11) 99999-8877".AnonymizePhone();
```

> For bulk processing, instantiate the anonymizer classes directly and reuse them — they are stateless and thread-safe.

---

## IP addresses — why truncation is not anonymization

IP truncation (`192.168.1.42` → `192.168.1.0`) is a common practice but **does not constitute anonymization** under Art. 12 of the LGPD or GDPR Recital 49. The CNIL ruled that truncated IPs combined with any metadata remain personal data. The EDPB requires that anonymization pass three cumulative tests (singling out, linkability, inference) — truncation fails all three given access to ISP allocation tables or session metadata.

**The correct treatment for IP addresses:**

| Context | Correct treatment | Legal basis |
|---------|------------------|-------------|
| Audit / security logs | **Pseudonymize** with `TokenPseudonymizer` | Legitimate interest (Art. 7, IX) |
| Analytics / reporting | Do not store, or collect only with consent | Consent (Art. 7, I) |
| Shown to end users | Not applicable — do not expose to other users | — |

```csharp
// Pseudonymize IP before writing to the audit log
var ipToken = await pseudonymizer.PseudonymizeAsync(request.RemoteIpAddress?.ToString());

var record = new AuditRecord
{
    DataSubjectId  = userId,
    Entity         = "Order",
    Field          = "Payment",
    Operation      = AuditOperation.Access,
    IpAddressToken = ipToken,   // opaque in the log
};

// During a security investigation, resolve the token:
var originalIp = await pseudonymizer.ReverseAsync(ipToken);
```

---

## Pseudonymizers

Pseudonymizers replace a value with a token. The original can be recovered given the right store or key.

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

> **Important:** `InMemoryTokenStore` loses all mappings when the process exits. In production, implement `ITokenStore` backed by a durable store such as SQL Server or Redis. Losing the store makes pseudonymized data irrecoverable.

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

Deterministic HMAC-SHA256: same input + same key = same token, without storing a mapping table. Not reversible through `Reverse()`.

```csharp
// Secret key must be at least 32 characters
var pseudo = new HmacPseudonymizer("my-secret-key-for-hmac-32-bytes!!");

var token = pseudo.Pseudonymize("joao@example.com");
// Same token on every call for the same input + key
// pseudo.Reverse(token) throws NotSupportedException
```

> Use `HmacPseudonymizer` when you need a consistent join key across systems that share the secret (e.g. analytics pipelines). Use `TokenPseudonymizer` when you need true reversibility.

#### Extension method

```csharp
using LGPD.NET.Anonymization.Extensions;

var token = "joao@example.com".PseudonymizeHmac("my-secret-key-for-hmac-32-bytes!!");
```

---

## Masking Strategies

Strategies are composable one-way transforms. Use them when the built-in anonymizers do not cover your field type, or when you need to build a custom pipeline.

### RedactionStrategy

Replaces the entire value with a fixed marker.

```csharp
new RedactionStrategy().Apply("any value");        // "[REDACTED]"
new RedactionStrategy("***").Apply("any value");   // "***"
```

### HashStrategy

SHA-256 one-way hash, optionally salted.

```csharp
new HashStrategy().Apply("value");
// "cd42404d52ad55ccfa9aca4adc828aa5..."  (64 hex chars)

// Salt must be at least 16 characters
new HashStrategy("my-fixed-salt-16ch").Apply("value");
// Different hash, same determinism
```

> Always use a salt in production. Without a salt, identical values produce identical hashes — a simple lookup table can reverse common inputs such as CPF numbers or phone numbers.
