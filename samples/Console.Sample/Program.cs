using System.Reflection;
using LGPD.NET.Core.Attributes;
using LGPD.NET.Core.Enums;
using LGPD.NET.Core.Exceptions;
using LGPD.NET.Core.Models;

// ─────────────────────────────────────────────
// LGPD.NET.Core — Console Sample
// ─────────────────────────────────────────────

PrintSection("1. Annotating models with LGPD attributes");
DemoAttributes();

PrintSection("2. Reading attribute metadata via reflection");
DemoReflection();

PrintSection("3. Working with consent records");
DemoConsent();

PrintSection("4. Audit trail records");
DemoAudit();

PrintSection("5. Data subject rights requests (Art. 18)");
DemoDataSubjectRequests();

PrintSection("6. Incident records (Art. 48)");
DemoIncidents();

PrintSection("7. Domain exceptions");
DemoExceptions();

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

        if (personal is null && sensitive is null) continue;

        Console.Write($"  {prop.Name,-18}");

        if (personal  is not null) Console.Write($"[PersonalData  category={personal.Category,-16} basis={personal.LegalBasis}]");
        if (sensitive  is not null) Console.Write($"[SensitiveData category={sensitive.Category,-16} basis={sensitive.SensitiveLegalBasis}]");
        if (retention  is not null) Console.Write($" [Retention years={retention.Years} policy={retention.Policy}]");
        if (transfer   is not null) Console.Write($" [Transfer country={transfer.Country} mechanism={transfer.Mechanism}]");
        if (erase      is not null) Console.Write($" [Erase anonymize={erase.AnonymizeInsteadOfDelete}]");

        Console.WriteLine();
    }
}

static void DemoReflection()
{
    // Modules like LGPD.NET.DataMap scan types at startup to build a processing inventory.
    // This shows the pattern they use.
    foreach (var prop in typeof(Customer).GetProperties())
    {
        var attr = prop.GetCustomAttribute<PersonalDataAttribute>();
        if (attr is null) continue;

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

static void DemoAudit()
{
    var records = new[]
    {
        new AuditRecord
        {
            DataSubjectId = "user-42",
            Entity        = nameof(Customer),
            Field         = nameof(Customer.Email),
            Operation     = AuditOperation.Access,
            ActorId       = "admin-7",
            IpAddress     = "192.168.1.10",
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
        Console.WriteLine($"  {r.Timestamp:u}  {r.Operation,-12} {r.Entity}.{r.Field,-20} actor={r.ActorId ?? "—"}");
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
        Console.WriteLine($"  {r.Id}  {r.Type,-14} {r.Status,-12} subject={r.DataSubjectId} kind={r.DataSubjectKind}");
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
        Category             = DataCategory.Financial,
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

    [EraseData(AnonymizeInsteadOfDelete = true)]
    public string? TemporaryNotes { get; set; }
}
