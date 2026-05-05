using System.Reflection;
using SensitiveFlow.Anonymization.Anonymizers;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Masking;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Anonymization.Stores;
using SensitiveFlow.Anonymization.Strategies;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Models;

// ─────────────────────────────────────────────
// SensitiveFlow — Console Sample
// ─────────────────────────────────────────────

PrintSection("1. Annotating models with LGPD attributes");
DemoAttributes();

PrintSection("2. Reading attribute metadata via reflection");
DemoReflection();

PrintSection("3. Working with consent records");
DemoConsent();

PrintSection("4. Audit trail records");
await DemoAuditAsync();

PrintSection("5. Data subject rights requests (Art. 18)");
DemoDataSubjectRequests();

PrintSection("6. Incident records (Art. 48)");
DemoIncidents();

PrintSection("7. Domain exceptions");
DemoExceptions();

PrintSection("8. Anonymization (Art. 12 — data leaves LGPD scope)");
DemoAnonymization();

PrintSection("9. Pseudonymization (Art. 12, §3 — data remains personal)");
await DemoPseudonymizationAsync();

PrintSection("10. Masking strategies (one-way transforms)");
DemoStrategies();

// ─────────────────────────────────────────────

static void DemoAttributes()
{
    // Models are annotated with LGPD attributes — no runtime overhead.
    // The attributes carry the legal metadata that other modules read.
    var props = typeof(Customer).GetProperties();

    foreach (var prop in props)
    {
        var personal = prop.GetCustomAttribute<PersonalDataAttribute>();
        var sensitive = prop.GetCustomAttribute<SensitiveDataAttribute>();
        var retention = prop.GetCustomAttribute<RetentionDataAttribute>();
        var transfer  = prop.GetCustomAttribute<InternationalTransferAttribute>();
        var erase     = prop.GetCustomAttribute<EraseDataAttribute>();

        if (personal is null && sensitive is null) { continue; }

        Console.Write($"  {prop.Name,-18}");

        if (personal  is not null) { Console.Write($"[PersonalData  category={personal.Category,-16} basis={personal.LegalBasis}]"); }
        if (sensitive  is not null) { Console.Write($"[SensitiveData category={sensitive.Category,-16} basis={sensitive.SensitiveLegalBasis}]"); }
        if (retention  is not null) { Console.Write($" [Retention years={retention.Years} policy={retention.Policy}]"); }
        if (transfer   is not null) { Console.Write($" [Transfer country={transfer.Country} mechanism={transfer.Mechanism}]"); }
        if (erase      is not null) { Console.Write($" [Erase anonymize={erase.AnonymizeInsteadOfDelete}]"); }

        Console.WriteLine();
    }
}

static void DemoReflection()
{
    // Modules like SensitiveFlow.DataMap scan types at startup to build a processing inventory.
    // This shows the pattern they use.
    foreach (var prop in typeof(Customer).GetProperties())
    {
        var attr = prop.GetCustomAttribute<PersonalDataAttribute>();
        if (attr is null) { continue; }

        Console.WriteLine($"  Field   : {prop.Name}");
        Console.WriteLine($"  Category: {attr.Category}");
        Console.WriteLine($"  Basis   : {attr.LegalBasis}");
        Console.WriteLine($"  Purpose : {attr.Purpose}");
        Console.WriteLine();
    }
}

static void DemoConsent()
{
    var consent = new ConsentRecord
    {
        Id                   = Guid.NewGuid().ToString(),
        DataSubjectId        = "user-42",
        Purpose              = ProcessingPurpose.Marketing,
        LegalBasis           = LegalBasis.Consent,
        CollectedAt          = DateTimeOffset.UtcNow,
        ExpiresAt            = DateTimeOffset.UtcNow.AddYears(1),
        Evidence             = "checkbox checked on signup form",
        CollectionChannel    = "web",
        PrivacyPolicyVersion = "2.1",
    };

    Console.WriteLine($"  Subject  : {consent.DataSubjectId}");
    Console.WriteLine($"  Purpose  : {consent.Purpose}");
    Console.WriteLine($"  Basis    : {consent.LegalBasis}");
    Console.WriteLine($"  Collected: {consent.CollectedAt:u}");
    Console.WriteLine($"  Expires  : {consent.ExpiresAt:u}");
    Console.WriteLine($"  Evidence : {consent.Evidence}");

    // Revoke — records are immutable; create a new one with Revoked = true
    var revoked = consent with
    {
        Revoked   = true,
        RevokedAt = DateTimeOffset.UtcNow,
    };

    Console.WriteLine($"\n  [revoked] Revoked={revoked.Revoked}  RevokedAt={revoked.RevokedAt:u}");
}

static async Task DemoAuditAsync()
{
    // IP addresses are personal data (LGPD Art. 5, I).
    // They must be PSEUDONYMIZED before being stored in the audit log — never stored raw.
    // This allows a security team to resolve the original IP during an investigation,
    // while keeping the audit log itself opaque to anyone without access to the token store.
    var ipStore      = new InMemoryTokenStore();
    var ipPseudo     = new TokenPseudonymizer(ipStore);
    var rawIp        = "192.168.1.10";
    var ipToken      = await ipPseudo.PseudonymizeAsync(rawIp);

    var records = new[]
    {
        new AuditRecord
        {
            DataSubjectId = "user-42",
            Entity        = nameof(Customer),
            Field         = nameof(Customer.Email),
            Operation     = AuditOperation.Access,
            ActorId       = "admin-7",
            IpAddressToken = ipToken,          // pseudonymized — not raw IP
        },
        new AuditRecord
        {
            DataSubjectId = "user-42",
            Entity        = nameof(Customer),
            Field         = nameof(Customer.TaxId),
            Operation     = AuditOperation.Update,
            ActorId       = "user-42",
        },
        new AuditRecord
        {
            DataSubjectId = "user-42",
            Entity        = nameof(Customer),
            Field         = nameof(Customer.Name),
            Operation     = AuditOperation.Anonymize,
            Details       = "erasure request #req-99",
        },
    };

    foreach (var r in records)
    {
        Console.WriteLine($"  {r.Timestamp:u}  {r.Operation,-12} {r.Entity}.{r.Field,-20} actor={r.ActorId ?? "—"}  ip={r.IpAddressToken ?? "—"}");
    }

    // During a security investigation the original IP can be recovered from the token store:
    var resolvedIp = await ipPseudo.ReverseAsync(ipToken);
    Console.WriteLine($"\n  [investigation] token {ipToken[..8]}… resolved to: {resolvedIp}");
}

static void DemoDataSubjectRequests()
{
    // Art. 18 lists the rights: confirmation, access, correction, deletion, portability, etc.
    var requests = new[]
    {
        new DataSubjectRequest
        {
            Id              = "req-1",
            DataSubjectId   = "user-42",
            Type            = DataSubjectRequestType.Access,
            Status          = DataSubjectRequestStatus.Open,
            ResponseDueAt   = DateTimeOffset.UtcNow.AddDays(15),
        },
        new DataSubjectRequest
        {
            Id              = "req-2",
            DataSubjectId   = "user-42",
            DataSubjectKind = DataSubjectKind.Adult,
            Type            = DataSubjectRequestType.Deletion,
            Status          = DataSubjectRequestStatus.InProgress,
            Notes           = "erasure requested after account cancellation",
        },
        new DataSubjectRequest
        {
            Id              = "req-3",
            DataSubjectId   = "user-99",
            DataSubjectKind = DataSubjectKind.Child,
            Type            = DataSubjectRequestType.Portability,
            Status          = DataSubjectRequestStatus.Completed,
            CompletedAt     = DateTimeOffset.UtcNow,
        },
    };

    foreach (var r in requests)
    {
        Console.WriteLine($"  {r.Id}  {r.Type,-14} {r.Status,-12} subject={r.DataSubjectId} kind={r.DataSubjectKind}");
    }
}

static void DemoIncidents()
{
    var incident = new IncidentRecord
    {
        Id                              = "inc-2024-001",
        Nature                          = IncidentNature.UnauthorizedAccess,
        Severity                        = IncidentSeverity.High,
        RiskLevel                       = RiskLevel.High,
        Status                          = IncidentStatus.Notified,
        Summary                         = "Credential stuffing attack exposed customer emails",
        AffectedData                    = [DataCategory.Contact, DataCategory.Identification],
        EstimatedAffectedDataSubjects   = 1_500,
        RemediationAction               = "Force password reset, enable MFA, patch auth endpoint",
        AnpdNotificationGeneratedAt     = DateTimeOffset.UtcNow,
    };

    Console.WriteLine($"  Id       : {incident.Id}");
    Console.WriteLine($"  Nature   : {incident.Nature}");
    Console.WriteLine($"  Severity : {incident.Severity}  Risk={incident.RiskLevel}");
    Console.WriteLine($"  Status   : {incident.Status}");
    Console.WriteLine($"  Affected : ~{incident.EstimatedAffectedDataSubjects:N0} data subjects");
    Console.WriteLine($"  Data     : {string.Join(", ", incident.AffectedData)}");
    Console.WriteLine($"  ANPD     : {incident.AnpdNotificationGeneratedAt:u}");
    Console.WriteLine($"  Action   : {incident.RemediationAction}");
}

static void DemoExceptions()
{
    TryCatch("ConsentNotFoundException", () =>
        throw new ConsentNotFoundException("user-42", ProcessingPurpose.Marketing));

    TryCatch("DataNotFoundException", () =>
        throw new DataNotFoundException("Customer", "user-42"));

    TryCatch("RetentionExpiredException", () =>
        throw new RetentionExpiredException("Customer", "TaxId", DateTimeOffset.UtcNow.AddYears(-1)));

    TryCatch("InternationalTransferNotAllowedException", () =>
        throw new InternationalTransferNotAllowedException(
            TransferCountry.UnitedStates,
            SafeguardMechanism.ContractualClauses,
            "destination country not approved by ANPD"));
}

static void DemoAnonymization()
{
    // ANONYMIZATION (Art. 12) — irreversible, result is no longer personal data.
    // Only BrazilianTaxIdAnonymizer qualifies: replaces all digits, no structure remains.
    //
    // MASKING — reduces accidental exposure in UIs/logs, but the result REMAINS personal data.
    // Email, phone, and name masking keep enough structure to allow re-identification.
    //
    // IP addresses cannot be anonymized by truncation — see DemoAuditAsync for correct treatment.

    var customer = new Customer
    {
        Name  = "João da Silva",
        TaxId = "123.456.789-09",
        Email = "joao.silva@example.com",
        Phone = "+55 11 99999-8877",
    };

    Console.WriteLine("  Original:");
    Console.WriteLine($"    Name  : {customer.Name}");
    Console.WriteLine($"    TaxId : {customer.TaxId}");
    Console.WriteLine($"    Email : {customer.Email}");
    Console.WriteLine($"    Phone : {customer.Phone}");

    Console.WriteLine("\n  Anonymized — Art. 12 compliant (data leaves LGPD scope):");
    Console.WriteLine($"    TaxId : {customer.TaxId.AnonymizeTaxId()}");

    Console.WriteLine("\n  Masked — risk reduction only (data REMAINS personal):");
    Console.WriteLine($"    Name  : {customer.Name.MaskName()}");
    Console.WriteLine($"    Email : {customer.Email.MaskEmail()}");
    Console.WriteLine($"    Phone : {customer.Phone.MaskPhone()}");

    // For bulk processing, instantiate and reuse directly — classes are stateless and thread-safe.
    Console.WriteLine("\n  Direct instance (preferred for bulk):");
    var taxAnon = new BrazilianTaxIdAnonymizer();
    Console.WriteLine($"    TaxId : {taxAnon.Anonymize(customer.TaxId)}");
}

static async Task DemoPseudonymizationAsync()
{
    // Pseudonymization replaces data with a reversible token.
    // Art. 12, §3: pseudonymized data REMAINS personal — LGPD obligations still apply.
    // The token can be reversed only by whoever holds the store with the mapping.

    // TokenPseudonymizer — reversible, requires a durable ITokenStore in production.
    // InMemoryTokenStore is for tests and single-session batch only.
    var store         = new InMemoryTokenStore();
    var tokenPseudo   = new TokenPseudonymizer(store);

    var original      = "joao.silva@example.com";
    var token         = await tokenPseudo.PseudonymizeAsync(original);
    var recovered     = await tokenPseudo.ReverseAsync(token);

    Console.WriteLine("  TokenPseudonymizer (reversible):");
    Console.WriteLine($"    Original  : {original}");
    Console.WriteLine($"    Token     : {token}");
    Console.WriteLine($"    Recovered : {recovered}");
    Console.WriteLine($"    Same token for same input: {await tokenPseudo.PseudonymizeAsync(original) == token}");

    // HmacPseudonymizer — deterministic (same input + key = same token), NOT reversible.
    // Useful as a join key across systems that share the secret — no mapping table needed.
    var hmacPseudo    = new HmacPseudonymizer("my-secret-key-for-hmac-32-bytes!!");
    var hmacToken     = hmacPseudo.Pseudonymize(original);
    var hmacToken2    = hmacPseudo.Pseudonymize(original);

    Console.WriteLine("\n  HmacPseudonymizer (deterministic, non-reversible):");
    Console.WriteLine($"    Original       : {original}");
    Console.WriteLine($"    Token          : {hmacToken}");
    Console.WriteLine($"    Deterministic  : {hmacToken == hmacToken2}");

    // Extension method shortcut
    var viaExtension  = original.PseudonymizeHmac("my-secret-key-for-hmac-32-bytes!!");
    Console.WriteLine($"    Via extension  : {viaExtension == hmacToken}");
}

static void DemoStrategies()
{
    // Strategies are composable one-way transforms that can be applied to any string.
    // Use them directly when you need a custom pipeline instead of a named anonymizer.

    var value = "sensitive-value";

    // Redaction — replaces the value entirely
    var redacted  = new RedactionStrategy().Apply(value);
    var custom    = new RedactionStrategy("***").Apply(value);
    Console.WriteLine($"  Redaction (default) : {redacted}");
    Console.WriteLine($"  Redaction (custom)  : {custom}");

    // Hash — SHA-256, one-way, deterministic
    var hash      = new HashStrategy().Apply(value);
    var salted    = new HashStrategy("my-fixed-salt-16ch").Apply(value);
    Console.WriteLine($"  Hash (no salt)      : {hash[..16]}…");
    Console.WriteLine($"  Hash (with salt)    : {salted[..16]}…");
    Console.WriteLine($"  Same input = same hash: {new HashStrategy().Apply(value) == hash}");
}

static void TryCatch(string label, Action action)
{
    try { action(); }
    catch (Exception ex) { Console.WriteLine($"  [{label}] {ex.Message}"); }
}

static void PrintSection(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ──");
}

// ─────────────────────────────────────────────
// Sample model
// ─────────────────────────────────────────────

public class Customer
{
    public Guid Id { get; set; }

    [PersonalData(
        Category   = DataCategory.Identification,
        LegalBasis = LegalBasis.ContractPerformance,
        Purpose    = ProcessingPurpose.ServiceProvision)]
    public string Name { get; set; } = string.Empty;

    [SensitiveData(
        Category             = SensitiveDataCategory.Other,
        SensitiveLegalBasis  = SensitiveLegalBasis.ExplicitConsent,
        Purpose              = ProcessingPurpose.ServiceProvision)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;

    [PersonalData(
        Category   = DataCategory.Contact,
        LegalBasis = LegalBasis.ContractPerformance,
        Purpose    = ProcessingPurpose.ServiceProvision)]
    [InternationalTransfer(
        Country    = TransferCountry.UnitedStates,
        Mechanism  = SafeguardMechanism.ContractualClauses,
        Recipient  = "Email delivery provider")]
    public string Email { get; set; } = string.Empty;

    [PersonalData(
        Category   = DataCategory.Location,
        LegalBasis = LegalBasis.ContractPerformance,
        Purpose    = ProcessingPurpose.ServiceProvision)]
    public string Address { get; set; } = string.Empty;

    [PersonalData(
        Category   = DataCategory.Contact,
        LegalBasis = LegalBasis.ContractPerformance,
        Purpose    = ProcessingPurpose.ServiceProvision)]
    public string Phone { get; set; } = string.Empty;

    [EraseData(AnonymizeInsteadOfDelete = true)]
    public string? TemporaryNotes { get; set; }
}

