# SensitiveFlow Documentation Index

Complete reference guide for all SensitiveFlow packages and their documentation.

## Quick Links

- **[PACKAGES_OVERVIEW.md](./PACKAGES_OVERVIEW.md)** — Complete package dependency graph, feature matrix, and common usage scenarios
- **[docs/efcore.md](./docs/efcore.md)** — EF Core integration guide
- **[docs/analyzers.md](./docs/analyzers.md)** — Analyzer rules reference
- **[docs/package-reference.md](./docs/package-reference.md)** — Full API reference

## Package Documentation (Individual README.md files)

Each package has a `README.md` in its source directory with:
- **Main Components** — Key types and interfaces
- **How It Works** — Architecture and flow diagrams
- **Usage Examples** — Code samples
- **Performance Considerations** — Optimization tips
- **Known Limitations** — What's not supported
- **Possible Improvements** — Future enhancements

### Core Packages

| Package | Purpose | Readme |
|---------|---------|--------|
| **SensitiveFlow.Core** | Attributes, enums, interfaces, models | [core/README.md](./src/SensitiveFlow.Core/README.md) |
| **SensitiveFlow.EFCore** | Automatic auditing during SaveChanges | [efcore/README.md](./src/SensitiveFlow.EFCore/README.md) |
| **SensitiveFlow.Audit** | Query and manage audit trails | [audit/README.md](./src/SensitiveFlow.Audit/README.md) |
| **SensitiveFlow.Audit.EFCore** | Persist audit records to database | [audit.efcore/README.md](./src/SensitiveFlow.Audit.EFCore/README.md) |

### Data Protection Packages

| Package | Purpose | Readme |
|---------|---------|--------|
| **SensitiveFlow.Json** | Redact in JSON serialization | [json/README.md](./src/SensitiveFlow.Json/README.md) |
| **SensitiveFlow.Logging** | Redact in log messages | [logging/README.md](./src/SensitiveFlow.Logging/README.md) |
| **SensitiveFlow.AspNetCore** | HTTP context integration | [aspnetcore/README.md](./src/SensitiveFlow.AspNetCore/README.md) |
| **SensitiveFlow.AspNetCore.EFCore** | Response redaction | [aspnetcore.efcore/README.md](./src/SensitiveFlow.AspNetCore.EFCore/README.md) |
| **SensitiveFlow.Diagnostics** | OpenTelemetry metrics and traces | [diagnostics/README.md](./src/SensitiveFlow.Diagnostics/README.md) |

### Compliance & Automation Packages

| Package | Purpose | Readme |
|---------|---------|--------|
| **SensitiveFlow.Audit.Snapshots.EFCore** | Full entity state snapshots | [audit.snapshots.efcore/README.md](./src/SensitiveFlow.Audit.Snapshots.EFCore/README.md) |
| **SensitiveFlow.Audit.EFCore.Outbox** | Outbox pattern for audit publishing | [audit.efcore.outbox/README.md](./src/SensitiveFlow.Audit.EFCore.Outbox/README.md) |
| **SensitiveFlow.TokenStore.EFCore** | Pseudonymization token storage (SQL Server/Postgres/SQLite) | [tokenstore.efcore/README.md](./src/SensitiveFlow.TokenStore.EFCore/README.md) |
| **SensitiveFlow.TokenStore.Redis** | Distributed pseudonymization token storage (Redis) | [tokenstore.redis/README.md](./src/SensitiveFlow.TokenStore.Redis/README.md) |
| **SensitiveFlow.Retention** | Automatic data cleanup and retention policies | [retention/README.md](./src/SensitiveFlow.Retention/README.md) |
| **SensitiveFlow.Anonymization** | Data export, subject erasure, and masking | [anonymization/README.md](./src/SensitiveFlow.Anonymization/README.md) |

### Development & Operations Packages

| Package | Purpose | Readme |
|---------|---------|--------|
| **SensitiveFlow.Analyzers** | Compile-time diagnostics | [analyzers/README.md](./src/SensitiveFlow.Analyzers/README.md) |
| **SensitiveFlow.Analyzers.CodeFixes** | Quick fixes for violations | [analyzers.codefixes/README.md](./src/SensitiveFlow.Analyzers.CodeFixes/README.md) |
| **SensitiveFlow.HealthChecks** | Health checks for compliance | [healthchecks/README.md](./src/SensitiveFlow.HealthChecks/README.md) |
| **SensitiveFlow.SourceGenerators** | Compile-time code generation | [sourcegenerators/README.md](./src/SensitiveFlow.SourceGenerators/README.md) |
| **SensitiveFlow.HealthChecks** | Health checks for audit and token infrastructure | [healthchecks/README.md](./src/SensitiveFlow.HealthChecks/README.md) |
| **SensitiveFlow.TestKit** | Testing utilities and conformance bases | [testkit/README.md](./src/SensitiveFlow.TestKit/README.md) |
| **SensitiveFlow.Tool** | CLI analysis and discovery reporting | [tool/README.md](./src/SensitiveFlow.Tool/README.md) |

## Documentation Hierarchy

```
DOCUMENTATION_INDEX.md (you are here)
├── PACKAGES_OVERVIEW.md
│   ├── Package Dependency Graph
│   ├── Feature Matrix
│   ├── Common Usage Scenarios
│   └── Installation Quick Reference
├── docs/
│   ├── efcore.md — EF Core guide
│   ├── analyzers.md — Analyzer reference
│   └── package-reference.md — Full API reference
└── src/*/README.md (20 files)
    ├── Main Components
    ├── How It Works
    ├── Usage Examples
    ├── Limitations
    └── Possible Improvements
```

## Start Here

### I want to...

**...add sensitive data protection to my REST API**
1. Read: [PACKAGES_OVERVIEW.md](./PACKAGES_OVERVIEW.md) → Scenario 1
2. Read: [Core/README.md](./src/SensitiveFlow.Core/README.md)
3. Read: [EFCore/README.md](./src/SensitiveFlow.EFCore/README.md)
4. Read: [Analyzers/README.md](./src/SensitiveFlow.Analyzers/README.md)
5. Read: [AspNetCore/README.md](./src/SensitiveFlow.AspNetCore/README.md)
6. Read: [Json/README.md](./src/SensitiveFlow.Json/README.md)

**...handle data export and erasure requests**
1. Read: [Anonymization/README.md](./src/SensitiveFlow.Anonymization/README.md)
2. Read: [Audit/README.md](./src/SensitiveFlow.Audit/README.md)
3. Read: [Audit.EFCore/README.md](./src/SensitiveFlow.Audit.EFCore/README.md)

**...automate data retention policies**
1. Read: [Retention/README.md](./src/SensitiveFlow.Retention/README.md)
2. Read: [HealthChecks/README.md](./src/SensitiveFlow.HealthChecks/README.md)

**...audit data changes**
1. Read: [EFCore/README.md](./src/SensitiveFlow.EFCore/README.md)
2. Read: [Audit/README.md](./src/SensitiveFlow.Audit/README.md)
3. Read: [Audit.EFCore/README.md](./src/SensitiveFlow.Audit.EFCore/README.md)

**...redact logs and JSON**
1. Read: [Logging/README.md](./src/SensitiveFlow.Logging/README.md)
2. Read: [Json/README.md](./src/SensitiveFlow.Json/README.md)

**...write tests**
1. Read: [TestKit/README.md](./src/SensitiveFlow.TestKit/README.md)

**...analyze my codebase**
1. Read: [Tool/README.md](./src/SensitiveFlow.Tool/README.md)

## Key Concepts Explained

### Attributes
- **`[PersonalData]`** — Mark personal data (name, email, address, etc.)
- **`[SensitiveData]`** — Mark sensitive data (tokens, passwords, keys)
- **`[Redaction]`** — Control redaction per context (ApiResponse, Logs, Audit, Export)

### Enums
- **`DataCategory`** — Types of personal data (Contact, Identification, Financial, etc.)
- **`SensitiveDataCategory`** — Types of sensitive data (Credential, Token, etc.)
- **`OutputRedactionAction`** — Redaction types (None, Redact, Mask, Omit, Pseudonymize)
- **`RedactionContext`** — Where redaction applies (ApiResponse, Logs, Audit, Export)
- **`AuditOperation`** — Types of changes (Create, Update, Delete, Access, Export)

### Core Flows

**Auditing Flow**:
```
Entity Change → SaveChanges
    ↓
SensitiveDataAuditInterceptor detects mutation
    ↓
Creates AuditRecord
    ↓
IAuditStore persists (EFCoreAuditStore)
    ↓
Queryable via IAuditStore.QueryAsync
```

**Redaction Flow**:
```
Application returns value
    ↓
Redaction layer intercepts (Json/Logging/AspNetCore)
    ↓
Checks [PersonalData] + [Redaction] attributes
    ↓
Applies action per context
    ↓
Client/log sees redacted value
```

**Data Export Flow**:
```
Export request for subject
    ↓
DataSubjectExporter queries all data for subject
    ↓
Applies [Redaction(Export=...)]
    ↓
Returns structured export
    ↓
Deliver to requester
```

## Common Tasks

### Enable Compile-Time Checks
```bash
dotnet add package SensitiveFlow.Analyzers
dotnet add package SensitiveFlow.Analyzers.CodeFixes
```

### Audit EF Core Changes
```bash
dotnet add package SensitiveFlow.EFCore
dotnet add package SensitiveFlow.Audit.EFCore
```

### Redact API Responses
```bash
dotnet add package SensitiveFlow.AspNetCore
dotnet add package SensitiveFlow.AspNetCore.EFCore
```

### Redact Logs
```bash
dotnet add package SensitiveFlow.Logging
```

### Handle Data Export & Erasure
```bash
dotnet add package SensitiveFlow.Anonymization
```

### Cleanup Old Data
```bash
dotnet add package SensitiveFlow.Retention
```

## Inconsistencies & Issues Found

During documentation, these inconsistencies were identified:

1. ✅ **Resolved**: `[Redaction(...Audit=Omit)]` was omitting fields entirely from audit records. **Fixed** to always audit all fields; Omit only affects output, never audit records.

2. **Documented**: Race condition in bulk operations between prefetch and execution. Solution: Use explicit transactions for critical operations.

3. **Documented**: DataSubjectId validation occurs at SaveChanges time, not pre-validation. Risk: Invalid entities partially persisted before validation fails.

## Testing Your Setup

### Minimal Verification
```csharp
[Fact]
public async Task ShouldAuditEmailChange()
{
    var store = new InMemoryAuditStore();
    var interceptor = new SensitiveDataAuditInterceptor(
        store,
        NullAuditContext.Instance
    );

    // ... make change ...

    var records = await store.QueryAsync();
    Assert.NotEmpty(records);
    Assert.Equal("Email", records[0].Field);
}
```

### Full Integration Test
See [TestKit/README.md](./src/SensitiveFlow.TestKit/README.md) for complete testing patterns.

## Contributing to Documentation

When documenting new packages:

1. Create `src/PackageName/README.md`
2. Include sections:
   - Main Components
   - How It Works
   - Usage Examples
   - Performance Considerations
   - Limitations
   - Possible Improvements
3. Link from this index
4. Update [PACKAGES_OVERVIEW.md](./PACKAGES_OVERVIEW.md) dependency graph

## Version Compatibility

Current documentation applies to **v1.0.0-preview.4+**

For previous versions, check git history or release notes.

## Support

For questions:
- Check individual package README.md first
- Review [PACKAGES_OVERVIEW.md](./PACKAGES_OVERVIEW.md) for patterns
- Search issues on GitHub
- Create issue with detailed reproduction steps

---

**Last Updated**: 2026-05-14  
**Documentation Coverage**: 20/20 packages (100%)
