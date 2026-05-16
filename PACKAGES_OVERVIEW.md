# SensitiveFlow Packages Overview

Complete reference for all packages in the SensitiveFlow library, their purposes, and how they integrate.

## Package Dependency Graph

```
SensitiveFlow.Core (Base)
├── SensitiveFlow.EFCore (Audit via SaveChanges)
│   ├── SensitiveFlow.Audit.EFCore (Persist audit records)
│   │   └── SensitiveFlow.Audit (Query audit trail)
│   └── SensitiveFlow.Retention (Auto-cleanup)
├── SensitiveFlow.Analyzers (Compile-time checks)
│   └── SensitiveFlow.Analyzers.CodeFixes (Quick fixes)
├── SensitiveFlow.Json (Redact in serialization)
│   ├── Newtonsoft.Json support
│   └── System.Text.Json support
├── SensitiveFlow.Logging (Redact in logs)
├── SensitiveFlow.AspNetCore (HTTP context)
│   └── SensitiveFlow.AspNetCore.EFCore (Response redaction)
├── SensitiveFlow.Anonymization (Data export & erasure)
├── SensitiveFlow.HealthChecks (Monitoring)
└── SensitiveFlow.TestKit (Testing utilities)
```

## Core Packages

### SensitiveFlow.Core
**Purpose**: Attributes, enums, interfaces, and models

**Key Types**:
- Attributes: `[PersonalData]`, `[SensitiveData]`, `[Redaction]`
- Enums: `DataCategory`, `SensitiveDataCategory`, `OutputRedactionAction`, `AuditOperation`
- Interfaces: `IAuditContext`, `IAuditStore`, `IPseudonymizer`, `IDataSubjectExportService`
- Models: `AuditRecord`
- Cache: `SensitiveMemberCache`

**Dependencies**: None (only .NET)

**Use when**: Defining domain models with sensitive data annotations

## Audit & Storage

### SensitiveFlow.EFCore
**Purpose**: Automatic auditing during SaveChanges and bulk operations

**Key Types**:
- `SensitiveDataAuditInterceptor` — Captures mutations
- `SensitiveBulkOperationsGuardInterceptor` — Blocks unaudited bulk ops
- `ExecuteUpdateAuditedAsync<T>`, `ExecuteDeleteAuditedAsync<T>` — Audited bulk helpers

**Dependencies**: Core, Microsoft.EntityFrameworkCore

**Use when**: You need to audit EF Core operations automatically

### SensitiveFlow.Audit
**Purpose**: Query and manage audit trails

**Key Types**:
- `IAuditStore`, `IBatchAuditStore` (interfaces)
- `AuditQuery` (builder)
- `InMemoryAuditStore` (test implementation)

**Dependencies**: Core

**Use when**: You need to query audit records or implement custom storage

### SensitiveFlow.Audit.EFCore
**Purpose**: Persist audit records to database

**Key Types**:
- `EfCoreAuditStore<TDbContext>` — Database-backed storage

**Dependencies**: Core, Audit, EFCore, Microsoft.EntityFrameworkCore

**Use when**: You want database persistence for audit trail

## Data Protection

### SensitiveFlow.Json
**Purpose**: Redact sensitive data during JSON serialization

**Key Types**:
- `SensitiveDataNewtonsoftConverter` (Newtonsoft.Json)
- `SensitiveJsonModifier` (System.Text.Json)

**Dependencies**: Core

**Use when**: You need automatic redaction in JSON output (APIs, exports)

### SensitiveFlow.Logging
**Purpose**: Redact sensitive data in log messages

**Key Types**:
- `RedactingLogger<T>` — Wrapper for ILogger

**Dependencies**: Core, Microsoft.Extensions.Logging

**Use when**: You need automatic redaction in application logs

### SensitiveFlow.AspNetCore
**Purpose**: HTTP context integration for audit context

**Key Types**:
- `HttpAuditContext` — Extracts ActorId and IP from HttpContext

**Dependencies**: Core, Microsoft.AspNetCore

**Use when**: Running in ASP.NET Core and need user/IP in audit records

### SensitiveFlow.AspNetCore.EFCore
**Purpose**: Automatic response envelope redaction

**Key Types**:
- Response filter for redacting JSON responses

**Dependencies**: AspNetCore, EFCore

**Use when**: You need automatic API response redaction

## Compliance & Automation

### SensitiveFlow.Anonymization
**Purpose**: Export and manage subject data, including erasure workflows

**Key Types**:
- `DataSubjectExporter<T>` — Export all data for a subject
- `IDataSubjectExportService` — Data export interface

**Dependencies**: Core, EFCore

**Use when**: You need to export subject data or support erasure requests

### SensitiveFlow.Retention
**Purpose**: Automatic data deletion/anonymization

**Key Types**:
- `RetentionScheduler` — Background job
- `IRetentionPolicy` — Define what expires when

**Dependencies**: Core, EFCore

**Use when**: You need compliance-based data cleanup

## Development & Operations

### SensitiveFlow.Analyzers
**Purpose**: Compile-time diagnostics for privacy violations

**Rules**:
- SF0001: Sensitive data logged without masking
- SF0002: Sensitive data returned in HTTP response without masking
- SF0003: Entity missing DataSubjectId
- SF0004: Property name suggests personal data but not annotated
- SF0005: Sensitive data endpoint without [Authorize]

**Dependencies**: Roslyn

**Use when**: You want compile-time safety checks

### SensitiveFlow.Analyzers.CodeFixes
**Purpose**: Quick fixes for analyzer violations

**Fixes**:
- SF0001/SF0002: Suggest masking methods

**Dependencies**: Analyzers

**Use when**: You want IDE quick fixes

### SensitiveFlow.HealthChecks
**Purpose**: Health checks for compliance verification

**Checks**:
- Audit store connectivity
- Data export functionality
- Retention policy health

**Dependencies**: Core, Microsoft.Extensions.Diagnostics.HealthChecks

**Use when**: Running in production and need monitoring

### SensitiveFlow.TestKit
**Purpose**: Testing utilities

**Components**:
- `InMemoryAuditStore` — Simple test store
- Builders and assertions

**Dependencies**: Core, Audit

**Use when**: Writing unit tests for sensitive data handling

## Complete Feature Matrix

| Feature | Package | Sync | Async |
|---------|---------|------|-------|
| Mark sensitive data | Core | ✅ | - |
| Audit SaveChanges | EFCore | ✅ | ✅ |
| Audit bulk operations | EFCore | - | ✅ |
| Persist audit records | Audit.EFCore | - | ✅ |
| Query audit trail | Audit | ✅ | ✅ |
| Stream large audit sets | Audit | - | ✅ |
| Search audit records | Audit | - | ✅ |
| Export audit data (CSV/JSON) | Audit | - | ✅ |
| Anomaly detection & alerting | Audit | - | ✅ |
| Anonymization workflow | Audit | - | ✅ |
| Redact in JSON | Json | ✅ | ✅ |
| Redact in logs | Logging | ✅ | ✅ |
| Extract HTTP context | AspNetCore | ✅ | - |
| Redact HTTP responses | AspNetCore.EFCore | ✅ | ✅ |
| Export subject data | Anonymization | - | ✅ |
| Delete/anonymize data | Anonymization | - | ✅ |
| Schedule retention | Retention | - | ✅ |
| Compile-time checks | Analyzers | ✅ | - |
| Health checks | HealthChecks | - | ✅ |
| Test utilities | TestKit | ✅ | ✅ |

## Common Usage Scenarios

### Scenario 1: REST API with Data Protection
```
User annotates entities → [PersonalData]
Analyzer checks for leaks → SF0001, SF0002, SF0003
EFCore audits SaveChanges
Audit.EFCore persists to database
AspNetCore extracts user/IP
Json redacts responses
Logging redacts messages
Retention auto-cleans old data
Anonymization handles data export/erasure
```

**Packages needed**:
- Core, Analyzers, EFCore, Audit.EFCore, AspNetCore, Json, Logging, Retention, Anonymization

### Scenario 2: Microservice with Audit Trail
```
Service receives event
Audits changes via EFCore
Stores in dedicated audit database (Audit.EFCore)
Data export requests query via Audit
Team can review and analyze records
```

**Packages needed**:
- Core, EFCore, Audit, Audit.EFCore

### Scenario 3: Background Job with Data Cleanup
```
Daily job runs
Retention scheduler checks policies
Identifies expired data
Deletes or anonymizes
Audit trail recorded
Health checks monitor
```

**Packages needed**:
- Core, EFCore, Audit, Retention, HealthChecks

## Package Size & Performance

| Package | Size | Runtime Overhead |
|---------|------|------------------|
| Core | ~100 KB | Minimal (reflection cache) |
| EFCore | ~50 KB | Low (SaveChanges hook) |
| Audit | ~30 KB | Low (LINQ query) |
| Audit.EFCore | ~20 KB | Medium (DB query) |
| Analyzers | ~200 KB | Build-time only |
| Json | ~40 KB | Low (converter interception) |
| Logging | ~30 KB | Low (logger wrapper) |
| AspNetCore | ~20 KB | Minimal (context extraction) |
| Anonymization | ~60 KB | Medium (DB query) |
| Retention | ~50 KB | Background job |
| HealthChecks | ~20 KB | On-demand checks |
| TestKit | ~10 KB | Test-only |

## Installation Quick Reference

### Minimal Setup (Audit Only)
```bash
dotnet add package SensitiveFlow.Core
dotnet add package SensitiveFlow.Analyzers
dotnet add package SensitiveFlow.EFCore
dotnet add package SensitiveFlow.Audit.EFCore
```

### Full Production Setup
```bash
dotnet add package SensitiveFlow.Core
dotnet add package SensitiveFlow.Analyzers
dotnet add package SensitiveFlow.Analyzers.CodeFixes
dotnet add package SensitiveFlow.EFCore
dotnet add package SensitiveFlow.Audit.EFCore
dotnet add package SensitiveFlow.AspNetCore
dotnet add package SensitiveFlow.AspNetCore.EFCore
dotnet add package SensitiveFlow.Json
dotnet add package SensitiveFlow.Logging
dotnet add package SensitiveFlow.Anonymization
dotnet add package SensitiveFlow.Retention
dotnet add package SensitiveFlow.HealthChecks
```

### Testing Setup
```bash
dotnet add package SensitiveFlow.TestKit --scope test
dotnet add package SensitiveFlow.Analyzers --scope development PrivateAssets=all
```

## Documentation Index

Each package has a `README.md` in its source directory:

- [Core](../src/SensitiveFlow.Core/README.md)
- [EFCore](../src/SensitiveFlow.EFCore/README.md)
- [Audit](../src/SensitiveFlow.Audit/README.md)
- [Audit.EFCore](../src/SensitiveFlow.Audit.EFCore/README.md)
- [Analyzers](../src/SensitiveFlow.Analyzers/README.md)
- [Json](../src/SensitiveFlow.Json/README.md)
- [Logging](../src/SensitiveFlow.Logging/README.md)
- [AspNetCore](../src/SensitiveFlow.AspNetCore/README.md)
- [Anonymization](../src/SensitiveFlow.Anonymization/README.md)
- [Retention](../src/SensitiveFlow.Retention/README.md)
- [HealthChecks](../src/SensitiveFlow.HealthChecks/README.md)
- [TestKit](../src/SensitiveFlow.TestKit/README.md)
