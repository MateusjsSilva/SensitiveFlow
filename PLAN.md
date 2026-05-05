# SensitiveFlow - Complete Work Plan

> MIT open source library for LGPD compliance in .NET projects.
> Modular, testable, with no required dependencies beyond `Core`.

---

## Table of contents

1. [Overview](#overview)
2. [LGPD legal coverage](#lgpd-legal-coverage)
3. [Folder structure](#folder-structure)
4. [Responsibilities by project](#responsibilities-by-project)
5. [Work plan - 5 phases](#work-plan---5-phases)
6. [Code conventions](#code-conventions)
7. [Test strategy](#test-strategy)
8. [CI/CD pipeline](#cicd-pipeline)
9. [NuGet publishing](#nuget-publishing)
10. [Versioning and changelog](#versioning-and-changelog)
11. [Design decisions](#design-decisions)

---

## Overview

### Goal

Provide a set of NuGet packages that allow .NET developers to implement LGPD requirements (Law 13.709/2018) in a declarative, testable way, without coupling to specific frameworks.

### Principles

- **Modular** - install only what you need
- **Opt-in** - nothing happens without explicit configuration
- **Testable** - all interfaces are mockable
- **Zero-alloc on hot path** - avoid unnecessary allocations in serialization and logging
- **Framework-agnostic in Core** - no dependency on ASP.NET, EF, or any framework

### Packages and dependencies

```
SensitiveFlow.Core                    (no external dependencies)
├── SensitiveFlow.Anonymization        depends on: Core
├── SensitiveFlow.LegalBasis           depends on: Core
├── SensitiveFlow.Consent              depends on: Core, LegalBasis
├── SensitiveFlow.Audit                depends on: Core
├── SensitiveFlow.DataSubject          depends on: Core, Audit
├── SensitiveFlow.Retention            depends on: Core, Audit
├── SensitiveFlow.DataMap              depends on: Core
├── SensitiveFlow.Incident             depends on: Core, Audit
├── SensitiveFlow.Ripd                 depends on: Core, DataMap
├── SensitiveFlow.Logging              depends on: Core, Microsoft.Extensions.Logging.Abstractions
├── SensitiveFlow.AspNetCore           depends on: Core, Consent, LegalBasis, Microsoft.AspNetCore.Http
├── SensitiveFlow.EFCore               depends on: Core, Audit, Retention, Microsoft.EntityFrameworkCore
└── SensitiveFlow.Analyzers            depends on: Microsoft.CodeAnalysis (Roslyn)
```

---

## LGPD legal coverage

Mapping between LGPD articles and library modules:

| Article | Topic | Module | Status |
|---------|-------|--------|--------|
| Art. 5 | Definitions (personal, sensitive, anonymized data) | `Core` - attributes and enums | ✅ Covered |
| Art. 7 | Legal bases for processing | `LegalBasis` | ✅ Covered |
| Art. 10 | Legitimate interest - balancing test | `LegalBasis` | ✅ Covered |
| Art. 11 | Legal bases for sensitive data | `LegalBasis` | ✅ Covered |
| Art. 12 | Anonymized vs pseudonymized data | `Anonymization` | ✅ Covered |
| Art. 15-16 | End of processing and retention | `Retention` | ✅ Covered |
| Art. 18 | Data subject rights (8 rights) | `DataSubject` | ✅ Covered |
| Art. 33-36 | International transfer | `Core` + `AspNetCore` | ✅ Covered |
| Art. 37 | Record of processing operations | `DataMap` | ✅ Covered |
| Art. 38 | Impact report (RIPD) | `Ripd` | ✅ Covered |
| Art. 41 | DPO endpoint | `AspNetCore` - `/.well-known/lgpd` | ✅ Covered |
| Art. 46-49 | Security incidents and breaches | `Incident` | ✅ Covered |
| Art. 50 | Best practices and governance | `Analyzers` + `DataMap` | ✅ Covered |

---

## Folder structure

```
sensitiveflow/
|
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                    # Build, test and coverage on PRs
│   │   ├── release.yml               # Publish to NuGet on tag vX.Y.Z
│   │   └── codeql.yml                # Security static analysis
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   └── feature_request.md
│   └── PULL_REQUEST_TEMPLATE.md
│
├── src/
│   │
│   ├── SensitiveFlow.Core/
│   │   ├── Attributes/
│   │   │   ├── PersonalDataAttribute.cs
│   │   │   ├── SensitiveDataAttribute.cs
│   │   │   ├── EraseDataAttribute.cs
│   │   │   ├── RetentionDataAttribute.cs
│   │   │   └── InternationalTransferAttribute.cs
│   │   ├── Enums/
│   │   │   ├── DataCategory.cs
│   │   │   ├── ProcessingPurpose.cs
│   │   │   ├── LegalBasis.cs
│   │   │   ├── SensitiveLegalBasis.cs
│   │   │   ├── ProcessingAgentRole.cs
│   │   │   ├── ProcessingPrinciple.cs
│   │   │   ├── DataSubjectKind.cs
│   │   │   ├── AnonymizationType.cs
│   │   │   ├── RiskLevel.cs
│   │   │   ├── TransferCountry.cs
│   │   │   ├── SafeguardMechanism.cs
│   │   │   ├── RetentionPolicy.cs
│   │   │   ├── DataSubjectRequestType.cs
│   │   │   ├── DataSubjectRequestStatus.cs
│   │   │   ├── AuditOperation.cs
│   │   │   ├── IncidentNature.cs
│   │   │   ├── IncidentSeverity.cs
│   │   │   └── IncidentStatus.cs
│   │   ├── Interfaces/
│   │   │   ├── IConsentStore.cs
│   │   │   ├── IAuditStore.cs
│   │   │   ├── IDataSubject.cs
│   │   │   ├── IAnonymizer.cs
│   │   │   ├── IPseudonymizer.cs
│   │   │   ├── IProcessingInventory.cs
│   │   │   └── IIncidentStore.cs
│   │   ├── Models/
│   │   │   ├── ConsentRecord.cs
│   │   │   ├── AuditRecord.cs
│   │   │   ├── DataSubjectRequest.cs
│   │   │   ├── ProcessingOperationRecord.cs
│   │   │   ├── DataSharingRecord.cs
│   │   │   └── IncidentRecord.cs
│   │   ├── Exceptions/
│   │   │   ├── ConsentNotFoundException.cs
│   │   │   ├── DataNotFoundException.cs
│   │   │   ├── InternationalTransferNotAllowedException.cs
│   │   │   └── RetentionExpiredException.cs
│   │   └── SensitiveFlow.Core.csproj
│   │
│   ├── SensitiveFlow.Anonymization/
│   │   ├── Anonymizers/
│   │   │   ├── BrazilianTaxIdAnonymizer.cs
│   │   │   ├── EmailAnonymizer.cs
│   │   │   ├── PhoneAnonymizer.cs
│   │   │   ├── NameAnonymizer.cs
│   │   │   └── IpAnonymizer.cs
│   │   ├── Pseudonymizers/
│   │   │   ├── TokenPseudonymizer.cs
│   │   │   └── HmacPseudonymizer.cs
│   │   ├── Extensions/
│   │   │   └── StringAnonymizationExtensions.cs
│   │   ├── Strategies/
│   │   │   ├── IMaskStrategy.cs
│   │   │   ├── HashStrategy.cs
│   │   │   └── RedactionStrategy.cs
│   │   └── SensitiveFlow.Anonymization.csproj
│   │
│   ├── SensitiveFlow.LegalBasis/
│   │   ├── Services/
│   │   │   ├── ILegalBasisService.cs
│   │   │   └── LegalBasisService.cs
│   │   ├── Models/
│   │   │   ├── LegalBasisRecord.cs
│   │   │   ├── LegitimateInterest.cs
│   │   │   └── LegalObligation.cs
│   │   ├── Validators/
│   │   │   ├── ILegalBasisValidator.cs
│   │   │   └── LegalBasisValidator.cs
│   │   ├── Stores/
│   │   │   └── InMemoryLegalBasisStore.cs
│   │   └── SensitiveFlow.LegalBasis.csproj
│   │
│   ├── SensitiveFlow.Consent/
│   │   ├── Services/
│   │   │   ├── IConsentService.cs
│   │   │   └── ConsentService.cs
│   │   ├── Models/
│   │   │   ├── ConsentRegistration.cs
│   │   │   ├── Purpose.cs
│   │   │   └── PrivacyPolicy.cs
│   │   ├── Stores/
│   │   │   └── InMemoryConsentStore.cs
│   │   ├── Options/
│   │   │   └── ConsentOptions.cs
│   │   └── SensitiveFlow.Consent.csproj
│   │
│   ├── SensitiveFlow.Audit/
│   │   ├── Services/
│   │   │   ├── IAuditService.cs
│   │   │   └── AuditService.cs
│   │   ├── Models/
│   │   │   ├── AuditEvent.cs
│   │   │   └── AccessContext.cs
│   │   ├── Stores/
│   │   │   └── InMemoryAuditStore.cs
│   │   ├── Options/
│   │   │   └── AuditOptions.cs
│   │   └── SensitiveFlow.Audit.csproj
│   │
│   ├── SensitiveFlow.DataSubject/
│   │   ├── Services/
│   │   │   ├── IDataSubjectService.cs
│   │   │   └── DataSubjectService.cs
│   │   ├── Handlers/
│   │   │   ├── PortabilityHandler.cs
│   │   │   ├── DeletionHandler.cs
│   │   │   ├── CorrectionHandler.cs
│   │   │   └── ObjectionHandler.cs
│   │   ├── Models/
│   │   │   ├── PortabilityReport.cs
│   │   │   ├── DeletionRequest.cs
│   │   │   └── InformationResponse.cs
│   │   └── SensitiveFlow.DataSubject.csproj
│   │
│   ├── SensitiveFlow.Retention/
│   │   ├── Services/
│   │   │   ├── IRetentionService.cs
│   │   │   └── RetentionService.cs
│   │   ├── Policies/
│   │   │   ├── IRetentionPolicy.cs
│   │   │   ├── FixedTermPolicy.cs
│   │   │   └── PurposeAchievedPolicy.cs
│   │   ├── Workers/
│   │   │   └── RetentionBackgroundService.cs
│   │   ├── Options/
│   │   │   └── RetentionOptions.cs
│   │   └── SensitiveFlow.Retention.csproj
│   │
│   ├── SensitiveFlow.DataMap/
│   │   ├── Services/
│   │   │   ├── IDataMapService.cs
│   │   │   └── DataMapService.cs
│   │   ├── Models/
│   │   │   ├── ProcessingOperation.cs
│   │   │   ├── DataSharing.cs
│   │   │   └── InventoryReport.cs
│   │   ├── Builders/
│   │   │   └── ProcessingOperationBuilder.cs
│   │   ├── Stores/
│   │   │   └── InMemoryDataMapStore.cs
│   │   └── SensitiveFlow.DataMap.csproj
│   │
│   ├── SensitiveFlow.Incident/
│   │   ├── Services/
│   │   │   ├── IIncidentService.cs
│   │   │   └── IncidentService.cs
│   │   ├── Models/
│   │   │   ├── SecurityIncident.cs
│   │   │   ├── AffectedDataSubject.cs
│   │   │   └── AnpdNotification.cs
│   │   ├── Notifications/
│   │   │   ├── IIncidentNotificationHandler.cs
│   │   │   └── EmailNotificationHandler.cs
│   │   ├── Stores/
│   │   │   └── InMemoryIncidentStore.cs
│   │   ├── Options/
│   │   │   └── IncidentOptions.cs
│   │   └── SensitiveFlow.Incident.csproj
│   │
│   ├── SensitiveFlow.Ripd/
│   │   ├── Services/
│   │   │   ├── IRipdService.cs
│   │   │   └── RipdService.cs
│   │   ├── Models/
│   │   │   ├── ImpactReport.cs
│   │   │   ├── IdentifiedRisk.cs
│   │   │   └── MitigationMeasure.cs
│   │   ├── Builders/
│   │   │   └── RipdBuilder.cs
│   │   ├── Export/
│   │   │   ├── IRipdExporter.cs
│   │   │   └── JsonRipdExporter.cs
│   │   └── SensitiveFlow.Ripd.csproj
│   │
│   ├── SensitiveFlow.Logging/
│   │   ├── Redactors/
│   │   │   ├── IRedactor.cs
│   │   │   ├── BrazilianTaxIdRedactor.cs
│   │   │   ├── EmailRedactor.cs
│   │   │   ├── CreditCardRedactor.cs
│   │   │   └── CompositeRedactor.cs
│   │   ├── Enrichers/
│   │   │   └── LgpdLogEnricher.cs
│   │   ├── Extensions/
│   │   │   └── LoggingBuilderExtensions.cs
│   │   └── SensitiveFlow.Logging.csproj
│   │
│   ├── SensitiveFlow.AspNetCore/
│   │   ├── Middleware/
│   │   │   ├── ConsentMiddleware.cs
│   │   │   ├── AuditMiddleware.cs
│   │   │   └── InternationalTransferMiddleware.cs
│   │   ├── Filters/
│   │   │   └── PersonalDataActionFilter.cs
│   │   ├── Endpoints/
│   │   │   └── LgpdWellKnownEndpoints.cs
│   │   ├── Extensions/
│   │   │   └── ApplicationBuilderExtensions.cs
│   │   ├── Options/
│   │   │   └── LgpdAspNetOptions.cs
│   │   └── SensitiveFlow.AspNetCore.csproj
│   │
│   ├── SensitiveFlow.EFCore/
│   │   ├── Interceptors/
│   │   │   ├── AuditInterceptor.cs
│   │   │   ├── AnonymizationSaveInterceptor.cs
│   │   │   └── RetentionInterceptor.cs
│   │   ├── Extensions/
│   │   │   └── DbContextOptionsBuilderExtensions.cs
│   │   ├── Conventions/
│   │   │   └── PersonalDataModelConvention.cs
│   │   └── SensitiveFlow.EFCore.csproj
│   │
│   └── SensitiveFlow.Analyzers/
│       ├── Analyzers/
│       │   ├── PersonalDataWithoutAnonymizationAnalyzer.cs  # LGPD001
│       │   ├── ConsentNotVerifiedAnalyzer.cs                # LGPD002
│       │   ├── LegalBasisNotDeclaredAnalyzer.cs             # LGPD003
│       │   └── InternationalTransferNotMarkedAnalyzer.cs    # LGPD004
│       ├── CodeFixes/
│       │   └── AddAttributeCodeFix.cs
│       └── SensitiveFlow.Analyzers.csproj
|
├── tests/
│   ├── SensitiveFlow.Core.Tests/
│   ├── SensitiveFlow.Anonymization.Tests/
│   ├── SensitiveFlow.LegalBasis.Tests/
│   ├── SensitiveFlow.Consent.Tests/
│   ├── SensitiveFlow.Audit.Tests/
│   ├── SensitiveFlow.DataSubject.Tests/
│   ├── SensitiveFlow.Retention.Tests/
│   ├── SensitiveFlow.DataMap.Tests/
│   ├── SensitiveFlow.Incident.Tests/
│   ├── SensitiveFlow.Ripd.Tests/
│   ├── SensitiveFlow.Logging.Tests/
│   ├── SensitiveFlow.AspNetCore.Tests/
│   ├── SensitiveFlow.EFCore.Tests/
│   |
│   ├── SensitiveFlow.Integration.Tests/
│   │   ├── AspNetCore/
│   │   │   ├── ConsentMiddlewareTests.cs
│   │   │   └── WellKnownEndpointTests.cs
│   │   ├── EFCore/
│   │   │   ├── AuditInterceptorTests.cs
│   │   │   └── RetentionInterceptorTests.cs
│   │   └── SensitiveFlow.Integration.Tests.csproj
│   |
│   └── SensitiveFlow.Benchmarks/
│       ├── AnonymizationBenchmarks.cs
│       ├── LoggingBenchmarks.cs
│       └── SensitiveFlow.Benchmarks.csproj
|
├── samples/
│   ├── WebApi.Sample/
│   ├── MinimalApi.Sample/
│   └── Console.Sample/
|
├── docs/
│   ├── getting-started.md
│   ├── attributes.md
│   ├── legal-bases.md
│   ├── consent.md
│   ├── audit.md
│   ├── data-subject-rights.md
│   ├── retention.md
│   ├── data-map.md
│   ├── incidents.md
│   ├── ripd.md
│   ├── international-transfer.md
│   ├── efcore.md
│   ├── aspnetcore.md
│   └── migration.md
|
├── Directory.Build.props
├── Directory.Packages.props
├── NuGet.config
├── sensitiveflow.slnx
├── LICENSE
├── README.md
├── CHANGELOG.md
└── CONTRIBUTING.md
```

---

## Responsibilities by project

### `SensitiveFlow.Core`

**What it is:** Public contract of the library. No business implementation - only types, interfaces, and attributes.

**Responsibilities:**
- Attributes `[PersonalData]`, `[SensitiveData]`, `[EraseData]`, `[RetentionData]`, `[InternationalTransfer]`
- Enums with the 10 legal bases of Art. 7 and bases for sensitive data in Art. 11
- Enum `AnonymizationType` distinguishing anonymization (irreversible, outside LGPD scope) from pseudonymization (reversible, still personal data)
- Cross-cutting LGPD vocabulary: processing agent roles, data subject kinds, Art. 6 processing principles, and shared risk levels
- Typed lifecycle enums for audit, data subject requests, retention, transfers, and incidents
- Store interfaces for consent, audit, processing inventory, and incidents
- Base records for consent, audit, data subject requests, processing operations, data sharing, and incidents
- Domain exceptions with contextual properties
- Zero external dependencies

**Example:**
```csharp
public class Customer
{
    public Guid Id { get; set; }

    [PersonalData(Category = DataCategory.Identification,
                 LegalBasis = LegalBasis.ContractPerformance)]
    public string Name { get; set; } = string.Empty;

    [SensitiveData(Category = DataCategory.Financial,
                  SensitiveLegalBasis = SensitiveLegalBasis.ExplicitConsent)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string BrazilianTaxId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    [InternationalTransfer(Country = TransferCountry.UnitedStates,
                                Mechanism = SafeguardMechanism.ContractualClauses)]
    public string Email { get; set; } = string.Empty;
}
```

---

### `SensitiveFlow.LegalBasis` - Art. 7 and Art. 11 (NEW)

**What it is:** Management of legal bases that authorize data processing.

**Why separate from Consent?** Consent is only one of the 10 legal bases. A system can process data based on legal obligation, contract performance, or legitimate interest - without consent - and still be LGPD compliant. Mixing the two is the most common compliance mistake.

**Responsibilities:**
- Model and document the 10 legal bases in Art. 7
- Model the bases for sensitive data in Art. 11
- Document legitimate interest justification with the balancing test (Art. 10)
- `InMemoryLegalBasisStore` for tests

**Example:**
```csharp
await legalBasisService.RegisterAsync(new LegalBasisRecord
{
    Entity = nameof(Customer),
    Field = nameof(Customer.Email),
    Base = LegalBasis.LegitimateInterest,
    Justification = "Sending communications about the active contract",
    BalancingTest = "Legitimate interest does not override data subject rights because..."
});
```

---

### `SensitiveFlow.Anonymization`

**What it is:** Masking, anonymization, and pseudonymization of personal data.

**Critical distinction (Art. 12):**
- **Anonymization** (`IAnonymizer`): irreversible. The data is no longer personal and **falls outside the LGPD scope**
- **Pseudonymization** (`IPseudonymizer`): reversible. The data remains personal and **all LGPD obligations apply**

**Responsibilities:**
- Anonymizers for Brazilian CPF/CNPJ tax IDs, email, phone, name, IP
- Pseudonymizers: `TokenPseudonymizer` (UUID mapping) and `HmacPseudonymizer` (HMAC-SHA256)
- `string` extensions for direct usage
- Benchmarks: < 500ns per operation

**Example:**
```csharp
// Anonymization - irreversible, data leaves LGPD scope
var anonymizedTaxId = anonymizer.Anonymize("123.456.789-09");
// "***.***.***-**"

// Pseudonymization - reversible, data remains personal
var token = pseudonymizer.Pseudonymize("123.456.789-09");
// "a3f8c2d1-..." - can be reversed with the right key
var original = pseudonymizer.Reverse(token);
// "123.456.789-09"
```

---

### `SensitiveFlow.Consent`

**What it is:** Consent lifecycle management (Art. 7, I and Art. 8).

**Responsibilities:**
- Register, revoke, and query consents by data subject and purpose
- Validate status (active + not expired)
- Capture collection evidence (date, channel, policy version)
- Automatic expiration with configurable TTL
- Integration with `LegalBasis`

**Example:**
```csharp
await consentService.RegisterAsync(new ConsentRegistration
{
    DataSubjectId = "user-123",
    Purpose = ProcessingPurpose.Marketing,
    ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
    CollectionEvidence = "Checkbox accepted at /signup on 05/01/2026 at 14:32 UTC"
});
```

---

### `SensitiveFlow.Audit`

**What it is:** Immutable record of who accessed or modified personal data.

**Responsibilities:**
- Register access, modification, and deletion events
- Capture context: who, when, which data, which operation, IP
- `InMemoryAuditStore` for tests
- Audit reports by data subject and period

---

### `SensitiveFlow.DataSubject` - Art. 18

**What it is:** Implementation of the 8 data subject rights.

**Responsibilities:**
- **Confirmation** (Art. 18, I): confirm existence of processing
- **Access** (Art. 18, II): provide full report of data and purposes
- **Correction** (Art. 18, III): correct data with audit trail
- **Anonymization/blocking/deletion** (Art. 18, IV): remove unnecessary data
- **Portability** (Art. 18, V): export data in JSON/CSV
- **Deletion of consented data** (Art. 18, VI)
- **Information about sharing** (Art. 18, VII)
- **Objection** (Art. 18, IX): contest processing based on legitimate interest

---

### `SensitiveFlow.Retention` - Art. 15 and 16 (NEW)

**What it is:** Automatic lifecycle and processing termination policies.

**Responsibilities:**
- `[RetentionData]` attribute defines period and action on expiration
- `RetentionBackgroundService`: `IHostedService` that runs periodic purges
- Pluggable policies: fixed term, purpose achieved
- Automatic audit for each purge executed
- Integration with `EFCore` via `RetentionInterceptor`

**Example:**
```csharp
builder.Services.AddLgpdRetention(options =>
{
    options.CheckInterval = TimeSpan.FromHours(24);
    options.DefaultAction = RetentionAction.Anonymize;
});

// Model
public class Order
{
    [RetentionData(Years = 5, Policy = RetentionPolicy.DeleteOnExpiration)]
    public string CardData { get; set; } = string.Empty;
}
```

---

### `SensitiveFlow.DataMap` - Art. 37 (NEW)

**What it is:** Inventory of the organization's data processing operations.

**Difference from Audit:** `Audit` records *access* at runtime (who read or modified specific data). `DataMap` records *operations* (which data the organization collects, for what purpose, under which legal basis, and who it shares with). These are complementary and both required by law.

**Responsibilities:**
- `ProcessingOperationBuilder` with a fluent API
- Inventory report generation in JSON
- Base for RIPD generation via `Ripd`

**Example:**
```csharp
await dataMapService.RegisterOperationAsync(
    new ProcessingOperationBuilder()
        .ForEntity<Customer>()
        .Fields(c => c.Email, c => c.Phone)
        .WithPurpose(ProcessingPurpose.ContractCommunication)
        .WithLegalBasis(LegalBasis.ContractPerformance)
        .RetainedFor(years: 5)
        .SharedWith("Email provider", "Transactional-only delivery")
        .Build()
);
```

---

### `SensitiveFlow.Incident` - Art. 46-49 (NEW)

**What it is:** Security incident management and breach notification.

**Responsibilities:**
- Register incidents with date, nature, affected data, and impacted subjects
- Generate structure for ANPD notification (Art. 48) within legal deadline
- Notify affected data subjects via `IIncidentNotificationHandler`
- Track status (detected -> assessed -> notified -> closed)

**Example:**
```csharp
var incident = await incidentService.RegisterAsync(new SecurityIncident
{
    DetectedAt = DateTimeOffset.UtcNow,
    Nature = IncidentNature.UnauthorizedAccess,
    AffectedData = [DataCategory.Identification, DataCategory.Financial],
    EstimatedAffectedDataSubjects = 1500,
    RemediationAction = "Access revoked, passwords reset, CISO notified"
});

var notification = incidentService.GenerateAnpdNotification(incident.Id);
```

---

### `SensitiveFlow.Ripd` - Art. 38 (NEW)

**What it is:** Generation and management of the Personal Data Protection Impact Report.

**Responsibilities:**
- `RipdBuilder` with a fluent API based on `DataMap`
- Identify and document risks and mitigation measures
- Export to JSON (extendable to PDF via community packages)

**Example:**
```csharp
var ripd = await ripdService.GenerateAsync(
    new RipdBuilder()
        .ForOperation("Processing of customer data")
        .ImportFromInventory(inventoryId)
        .AddRisk(new IdentifiedRisk
        {
            Description = "Unauthorized access to the database",
            Likelihood = RiskLevel.Medium,
            Impact = RiskLevel.High,
            Mitigation = "Encryption at rest, MFA for DBAs, query auditing"
        })
        .Build()
);
```

---

### `SensitiveFlow.AspNetCore`

**What it is:** Integration with the ASP.NET Core pipeline.

**Responsibilities:**
- `ConsentMiddleware` and `AuditMiddleware`
- `InternationalTransferMiddleware`: validates and blocks unauthorized transfers
- `[RequireConsent]` as action/controller attribute
- `GET /.well-known/lgpd` endpoint with DPO contact info (Art. 41)

**DPO endpoint example:**
```json
GET /.well-known/lgpd
{
  "dataController": "Example Company Ltd.",
  "cnpj": "00.000.000/0001-00",
  "dpo": {
    "name": "Joao Silva",
    "email": "dpo@example.com"
  },
  "privacyPolicyUrl": "https://example.com/privacy",
  "policyVersion": "2.1"
}
```

---

### `SensitiveFlow.EFCore`

**What it is:** Integration with Entity Framework Core via interceptors and conventions.

**Responsibilities:**
- `AuditInterceptor`: intercepts `SaveChanges` and records changes on `[PersonalData]` fields
- `AnonymizationSaveInterceptor`: anonymizes fields before persistence when configured
- `RetentionInterceptor`: checks and applies retention policies on persistence
- `PersonalDataModelConvention`: applies model configuration automatically

---

### `SensitiveFlow.Analyzers`

**What it is:** Roslyn analyzers that detect violations at compile time.

**Responsibilities:**
- `LGPD001`: property with PII without `[PersonalData]` in public types
- `LGPD002`: access to `[SensitiveData]` without consent check
- `LGPD003`: personal data field without declared legal basis
- `LGPD004`: data transfer to external service without `[InternationalTransfer]`
- Code fixes for all four analyzers

---

## Work plan - 5 phases

### Phase 1 - Foundation (Weeks 1-3)

**Goal:** Repository ready, CI running, and `Core` package published.

#### Week 1 - Repository setup
- [ ] Create GitHub repo with `README.md`, `LICENSE` (MIT), `.gitignore`
- [ ] Configure `Directory.Build.props` with nullable, warnings as errors, `net8.0;net10.0`
- [ ] Configure `Directory.Packages.props` (Central Package Management)
- [ ] Create solution and all projects via `dotnet new`
- [ ] Configure `.editorconfig`

#### Week 2 - CI/CD
- [ ] `ci.yml` workflow: build + tests + coverage (coverlet + codecov)
- [ ] `release.yml` workflow: publish to NuGet on `v*.*.*` tag
- [ ] `codeql.yml` workflow: security analysis
- [ ] Branch protection: PR required + green CI
- [ ] Badges in README

#### Week 3 - Core package
- [x] Attributes: `[PersonalData]`, `[SensitiveData]`, `[EraseData]`, `[RetentionData]`, `[InternationalTransfer]`
- [x] Enums with the 10 legal bases (Art. 7), bases for sensitive data (Art. 11), `AnonymizationType`, processing agent roles, data subject kinds, Art. 6 processing principles, risk levels, audit operations, data subject request status, retention, transfer safeguards, and incident lifecycle
- [x] All base interfaces and models for consent, audit, data subject requests, processing inventory, data sharing, and incidents
- [x] Typed exceptions with context properties
- [x] 100% line coverage in Core tests for `net8.0` and `net10.0`
- [ ] Publish `SensitiveFlow.Core` `1.0.0-preview.1`

---

### Phase 2 - Main modules (Weeks 4-10)

**Goal:** All functional modules delivered with tests.

#### Week 4 - Anonymization
- [ ] Anonymizers: CPF, email, phone, name, IP
- [ ] Pseudonymizers: `TokenPseudonymizer`, `HmacPseudonymizer`
- [ ] Document and test the distinction between anonymization and pseudonymization (Art. 12)
- [ ] Benchmarks: < 500ns per operation

#### Week 5 - LegalBasis
- [ ] Model the 10 legal bases of Art. 7 with documentation for each
- [ ] Bases for sensitive data in Art. 11
- [ ] Balancing test for legitimate interest (Art. 10)
- [ ] Tests covering each legal basis individually

#### Week 6 - Consent + Audit
- [ ] `ConsentService` with full lifecycle and integration with `LegalBasis`
- [ ] `AuditService` with immutable records and access context
- [ ] Concurrency tests for both

#### Week 7 - DataSubject
- [ ] All 8 rights in Art. 18 including objection (Art. 18, IX)
- [ ] `PortabilityHandler` with JSON and CSV export
- [ ] Integration tests: `DataSubject` + `Audit` + `Consent`

#### Week 8 - Retention
- [ ] `RetentionService` with pluggable policies
- [ ] `RetentionBackgroundService` as `IHostedService`
- [ ] Lifecycle tests: data created -> period expired -> purge executed -> audit logged

#### Week 9 - DataMap
- [ ] `DataMapService` with fluent `ProcessingOperationBuilder`
- [ ] JSON inventory report generation
- [ ] Tests validating inventory reflects model attributes

#### Week 10 - Incident + Logging
- [ ] `IncidentService` with status tracking and ANPD notification generation
- [ ] Logging redactors including credit card
- [ ] Publish preview of all modules

---

### Phase 3 - RIPD and integrations (Weeks 11-14)

**Goal:** RIPD functional and ASP.NET Core + EF Core integrations complete.

#### Week 11 - Ripd
- [ ] `RipdBuilder` with fluent API
- [ ] Integration with `DataMap` to import processing operations
- [ ] `JsonRipdExporter` compatible with ANPD requirements
- [ ] Tests with real scenarios

#### Week 12 - AspNetCore
- [ ] `ConsentMiddleware`, `AuditMiddleware`, `InternationalTransferMiddleware`
- [ ] `/.well-known/lgpd` endpoint with DPO data (Art. 41)
- [ ] Integration tests with `WebApplicationFactory`

#### Week 13 - EFCore
- [ ] `AuditInterceptor`, `RetentionInterceptor`, `PersonalDataModelConvention`
- [ ] Tests with `Testcontainers` (real PostgreSQL container)
- [ ] Validate compatibility with EF Core 8 and 10

#### Week 14 - Analyzers
- [ ] `LGPD001`, `LGPD002`, `LGPD003`, `LGPD004` as `DiagnosticAnalyzer`
- [ ] Code fixes for all analyzers
- [ ] Evaluate source generator to eliminate runtime reflection
- [ ] Tests with `Microsoft.CodeAnalysis.Testing`

---

### Phase 4 - Quality and documentation (Weeks 15-17)

#### Week 15 - Quality and security
- [ ] Security audit with Snyk and CodeQL
- [ ] Review all public APIs
- [ ] Coverage >= 90% in all packages
- [ ] Final benchmarks

#### Week 16 - Samples
- [ ] `WebApi.Sample`: full API with all modules
- [ ] `MinimalApi.Sample`: focused on consent and audit
- [ ] `Console.Sample`: automatic retention and DataMap

#### Week 17 - Documentation
- [ ] 5-minute quickstart guide
- [ ] Module docs with real examples
- [ ] LGPD article mapping table
- [ ] Legal and technical FAQ
- [ ] `CONTRIBUTING.md` and GitHub Discussions

---

### Phase 5 - Release 1.0.0 (Week 18)

- [ ] Create tag `v1.0.0` - CI publishes automatically to NuGet
- [ ] Verify all packages on NuGet.org
- [ ] Launch post on dev.to / LinkedIn
- [ ] Submit to awesome-dotnet and similar lists
- [ ] Open roadmap issues post-1.0.0 (stores for SQL Server, MongoDB, Redis)

---

## Code conventions

### Naming
- Public API in English (classes, methods, properties, namespaces, enums)
- Interfaces prefixed with `I`
- Attributes suffixed with `Attribute` (omitted in usage)
- Tests follow `Method_Scenario_ExpectedResult`

```csharp
[Fact]
public async Task VerifyAsync_ConsentRevoked_ReturnsFalse()
```

### Style
- Records for immutable models
- `sealed` on concrete implementations by default
- `CancellationToken` on all async methods
- Nullable annotations enabled in all projects
- C# 14 features allowed

### Commits (Conventional Commits)
```
feat(legal-basis): implement legal bases from Art. 7
feat(retention): add automatic purge via IHostedService
fix(anonymization): fix anonymization vs pseudonymization distinction
feat(incident): add ANPD notification generation
docs(ripd): add RIPD generation guide
test(datamap): add processing inventory tests
```

---

## Test strategy

### Test pyramid

```
         /\
        /  \  E2E (samples)
       /----\
      / Intg  \  Testcontainers (EF, ASP.NET)
     /----------\
    /   Unit      \  xUnit + FluentAssertions + NSubstitute
   /--------------\
  (goal: > 90% coverage)
```

### Tools
- `xUnit` - test framework
- `FluentAssertions` - readable assertions
- `NSubstitute` - mocks/stubs for interfaces
- `Testcontainers` - integration with real DB container
- `coverlet` - code coverage
- `BenchmarkDotNet` - performance benchmarks
- `Microsoft.AspNetCore.Mvc.Testing` - ASP.NET integration tests
- `Microsoft.CodeAnalysis.Testing` - Roslyn analyzer tests

### Rules
- Each package has its own test project
- Unit tests with no I/O (disk, network, database)
- Every bug fix gets a regression test
- Minimum 90% coverage enforced in CI

---

## CI/CD pipeline

### `ci.yml`
```yaml
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - run: dotnet build --configuration Release
      - run: dotnet test --collect:"XPlat Code Coverage" --no-build
      - uses: codecov/codecov-action@v4
```

### `release.yml`
```yaml
on:
  push:
    tags: ['v*.*.*']
jobs:
  publish:
    steps:
      - run: dotnet pack --configuration Release
      - run: dotnet nuget push **/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }}
```

---

## NuGet publishing

```xml
<PropertyGroup>
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  <Authors>Your Name</Authors>
  <PackageProjectUrl>https://github.com/your-user/sensitiveflow</PackageProjectUrl>
  <RepositoryUrl>https://github.com/your-user/sensitiveflow</RepositoryUrl>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageIcon>icon.png</PackageIcon>
  <PackageTags>lgpd;anpd;privacy;personal-data;dotnet;csharp;compliance</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

### Pre-publish checklist
- [ ] `PackageReleaseNotes` updated
- [ ] Version updated in all `.csproj`
- [ ] `CHANGELOG.md` updated
- [ ] All tests passing with coverage >= 90%
- [ ] Package-specific README for each package

---

## Versioning and changelog

### SemVer
- `MAJOR`: breaking changes in public API
- `MINOR`: backwards-compatible features
- `PATCH`: bug fixes

### Pre-1.0 preview
- `1.0.0-preview.1`, `1.0.0-preview.2`, etc.
- Breaking changes are allowed in preview versions

---

## Design decisions

### Why is `LegalBasis` separate from `Consent`?
Consent is only one of the 10 legal bases in Art. 7. A system can process data based on legal obligation or contract performance without consent and still be compliant. Mixing the two is a common compliance mistake.

### Why separate `IAnonymizer` from `IPseudonymizer`?
LGPD treats them very differently. Anonymized data falls outside the law (Art. 12). Pseudonymized data remains personal and all obligations apply. Separate interfaces make that distinction explicit and prevent misuse.

### Why separate `DataMap` from `Audit`?
`Audit` records *access* at runtime (who read data X at 14:32). `DataMap` records *operations* at design time (the company collects email for contractual communication, retains for 5 years, shares with an email provider). These are distinct obligations in Art. 37.

### Why interfaces for everything?
Users can implement their own stores (SQL Server, Redis, MongoDB). The `InMemoryStore` is for tests only. After 1.0.0, community packages can provide specific implementations.

### Why avoid runtime reflection?
Reflection is slow and does not work well with .NET 10 Native AOT. Phase 3 evaluates source generators to emit code at compile time based on attributes.

### Why English for the public API?
The library is intended for broad .NET adoption. English identifiers reduce friction for global teams while keeping LGPD terminology and legal references intact.

