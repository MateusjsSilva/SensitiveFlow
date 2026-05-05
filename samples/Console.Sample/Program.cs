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

// ---------------------------------------------
// SensitiveFlow - Console Sample
// ---------------------------------------------

PrintSection("1. Annotating models with privacy attributes");
DemoAttributes();

PrintSection("2. Reading attribute metadata via reflection");
DemoReflection();

PrintSection("3. Audit trail records");
await DemoAuditAsync();

PrintSection("4. Domain exceptions");
DemoExceptions();

PrintSection("5. Anonymization (data may leave personal-data scope)");
DemoAnonymization();

PrintSection("6. Pseudonymization (data remains personal)");
await DemoPseudonymizationAsync();

PrintSection("7. Masking strategies (one-way transforms)");
DemoStrategies();

// ---------------------------------------------

static void DemoAttributes()
{
    var props = typeof(Customer).GetProperties();

    foreach (var prop in props)
    {
        var personal  = prop.GetCustomAttribute<PersonalDataAttribute>();
        var sensitive = prop.GetCustomAttribute<SensitiveDataAttribute>();
        var retention = prop.GetCustomAttribute<RetentionDataAttribute>();

        if (personal is null && sensitive is null)
        {
            continue;
        }

        Console.Write($"  {prop.Name,-18}");

        if (personal  is not null) { Console.Write($"[PersonalData  category={personal.Category}]"); }
        if (sensitive is not null) { Console.Write($"[SensitiveData category={sensitive.Category}]"); }
        if (retention is not null) { Console.Write($" [Retention years={retention.Years} policy={retention.Policy}]"); }

        Console.WriteLine();
    }
}

static void DemoReflection()
{
    foreach (var prop in typeof(Customer).GetProperties())
    {
        var attr = prop.GetCustomAttribute<PersonalDataAttribute>();
        if (attr is null)
        {
            continue;
        }

        Console.WriteLine($"  Field   : {prop.Name}");
        Console.WriteLine($"  Category: {attr.Category}");
        Console.WriteLine();
    }
}

static async Task DemoAuditAsync()
{
    // IP addresses are personal data and must be pseudonymized before being stored in audit logs.
    var ipStore  = new InMemoryTokenStore();
    var ipPseudo = new TokenPseudonymizer(ipStore);
    var rawIp    = "192.168.1.10";
    var ipToken  = await ipPseudo.PseudonymizeAsync(rawIp);

    var records = new[]
    {
        new AuditRecord
        {
            DataSubjectId  = "user-42",
            Entity         = nameof(Customer),
            Field          = nameof(Customer.Email),
            Operation      = AuditOperation.Access,
            ActorId        = "admin-7",
            IpAddressToken = ipToken,
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
        Console.WriteLine($"  {r.Timestamp:u}  {r.Operation,-12} {r.Entity}.{r.Field,-20} actor={r.ActorId ?? "-"}  ip={r.IpAddressToken ?? "-"}");
    }

    // During a security investigation the original IP can be recovered from the token store:
    var resolvedIp = await ipPseudo.ReverseAsync(ipToken);
    Console.WriteLine($"\n  [investigation] token {ipToken[..8]}... resolved to: {resolvedIp}");
}

static void DemoExceptions()
{
    TryCatch("DataNotFoundException", () =>
        throw new DataNotFoundException("Customer", "user-42"));

    TryCatch("RetentionExpiredException", () =>
        throw new RetentionExpiredException("Customer", "TaxId", DateTimeOffset.UtcNow.AddYears(-1)));
}

static void DemoAnonymization()
{
    var customer = new Customer
    {
        Name  = "Joao da Silva",
        TaxId = "123.456.789-09",
        Email = "joao.silva@example.com",
        Phone = "+55 11 99999-8877",
    };

    Console.WriteLine("  Original:");
    Console.WriteLine($"    Name  : {customer.Name}");
    Console.WriteLine($"    TaxId : {customer.TaxId}");
    Console.WriteLine($"    Email : {customer.Email}");
    Console.WriteLine($"    Phone : {customer.Phone}");

    Console.WriteLine("\n  Anonymized (data may leave personal-data scope):");
    Console.WriteLine($"    TaxId : {customer.TaxId.AnonymizeTaxId()}");

    Console.WriteLine("\n  Masked - risk reduction only (data REMAINS personal):");
    Console.WriteLine($"    Name  : {customer.Name.MaskName()}");
    Console.WriteLine($"    Email : {customer.Email.MaskEmail()}");
    Console.WriteLine($"    Phone : {customer.Phone.MaskPhone()}");

    Console.WriteLine("\n  Direct instance (preferred for bulk):");
    var taxAnon = new BrazilianTaxIdAnonymizer();
    Console.WriteLine($"    TaxId : {taxAnon.Anonymize(customer.TaxId)}");
}

static async Task DemoPseudonymizationAsync()
{
    var store       = new InMemoryTokenStore();
    var tokenPseudo = new TokenPseudonymizer(store);

    var original  = "joao.silva@example.com";
    var token     = await tokenPseudo.PseudonymizeAsync(original);
    var recovered = await tokenPseudo.ReverseAsync(token);

    Console.WriteLine("  TokenPseudonymizer (reversible):");
    Console.WriteLine($"    Original  : {original}");
    Console.WriteLine($"    Token     : {token}");
    Console.WriteLine($"    Recovered : {recovered}");
    Console.WriteLine($"    Same token for same input: {await tokenPseudo.PseudonymizeAsync(original) == token}");

    var hmacPseudo = new HmacPseudonymizer("my-secret-key-for-hmac-32-bytes!!");
    var hmacToken  = hmacPseudo.Pseudonymize(original);
    var hmacToken2 = hmacPseudo.Pseudonymize(original);

    Console.WriteLine("\n  HmacPseudonymizer (deterministic, non-reversible):");
    Console.WriteLine($"    Original       : {original}");
    Console.WriteLine($"    Token          : {hmacToken}");
    Console.WriteLine($"    Deterministic  : {hmacToken == hmacToken2}");

    var viaExtension = original.PseudonymizeHmac("my-secret-key-for-hmac-32-bytes!!");
    Console.WriteLine($"    Via extension  : {viaExtension == hmacToken}");
}

static void DemoStrategies()
{
    var value = "sensitive-value";

    var redacted = new RedactionStrategy().Apply(value);
    var custom   = new RedactionStrategy("***").Apply(value);
    Console.WriteLine($"  Redaction (default) : {redacted}");
    Console.WriteLine($"  Redaction (custom)  : {custom}");

    var hash   = new HashStrategy().Apply(value);
    var salted = new HashStrategy("my-fixed-salt-16ch").Apply(value);
    Console.WriteLine($"  Hash (no salt)      : {hash[..16]}...");
    Console.WriteLine($"  Hash (with salt)    : {salted[..16]}...");
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
    Console.WriteLine($"-- {title} --");
}

// ---------------------------------------------
// Sample model
// ---------------------------------------------

public class Customer
{
    public Guid Id { get; set; }

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Other)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Location)]
    public string Address { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Phone { get; set; } = string.Empty;

    public string? TemporaryNotes { get; set; }
}
