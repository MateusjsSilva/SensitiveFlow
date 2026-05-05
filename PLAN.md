# SensitiveFlow - Product Plan

> Open-source .NET library for sensitive data observability and control.
> Goal: make sensitive data visible and auditable inside the runtime — not compliance paperwork.

## Vision

SensitiveFlow is a privacy-engineering toolkit for .NET developers.
It brings observability and control to sensitive data at the infrastructure level:

- classify personal and sensitive fields with declarative attributes
- automatically audit data access and mutations
- integrate privacy controls into EF Core and ASP.NET Core pipelines
- reduce exposure risk through masking and log redaction

SensitiveFlow focuses on runtime behavior. It does not replace legal processes or business workflows.

## Product Goals

1. Make privacy controls easy to adopt in existing .NET codebases.
2. Keep `Core` framework-agnostic and test-friendly.
3. Provide reliable defaults without hiding critical decisions.
4. Generate audit evidence automatically — no manual instrumentation required.

## Modules

- `SensitiveFlow.Core`
  Core contracts: attributes, enums, interfaces, models, exceptions.

- `SensitiveFlow.Audit`
  Immutable audit records for data access and mutation operations.

- `SensitiveFlow.EFCore`
  EF Core interceptor that auto-generates audit events on SaveChanges.

- `SensitiveFlow.AspNetCore`
  Middleware and filters that capture request context (actor, IP) for audit records.

- `SensitiveFlow.Anonymization`
  Masking, anonymization, and pseudonymization transforms.

- `SensitiveFlow.Logging`
  ILogger integration for automatic PII redaction in structured logs.

- `SensitiveFlow.Retention`
  Retention metadata attributes and expiration hook contracts.

- `SensitiveFlow.Analyzers`
  Roslyn analyzers to detect privacy anti-patterns at compile time.

## Core Capabilities

### 1. Data Classification
- `[PersonalData]` and `[SensitiveData]` attributes on properties and fields.
- Category metadata for classification and tooling.
- `[RetentionData]` attribute with expiration calculation helper.

### 2. Sensitive Data Observability
- Who accessed or mutated which field, when, and from which context.
- Immutable `AuditRecord` with actor, entity, field, operation, and timestamp.
- Pseudonymized IP token — raw IP is never stored in audit records.

### 3. Automatic Auditing
- EF Core interceptor detects sensitive fields on SaveChanges and emits records automatically.
- ASP.NET Core middleware enriches audit context with the current actor and request metadata.

### 4. Data Masking
- Maskers for CPF, email, phone, and name.
- Anonymizers and pseudonymizers for irreversible and reversible transforms.
- Extensible strategy interface for custom masking rules.

### 5. Log Redaction
- `ILogger` integration that scrubs sensitive values before they reach log sinks.

### 6. Lightweight Retention
- `[RetentionData]` attribute declares the retention window and expiration action.
- Hook contracts for expiration handlers — no built-in engine, no hidden background jobs.

## Roadmap

### Phase 1 — MVP
- `Core`: attributes, enums, `AuditRecord`, `IAuditStore`, `ITokenStore`.
- `Audit`: in-memory store, basic query.
- `EFCore`: SaveChanges interceptor with sensitive-field detection.

### Phase 2 — Platform Integrations
- `AspNetCore`: middleware for actor/request context enrichment.
- `Logging`: ILogger redaction integration.
- `Anonymization`: maskers and pseudonymizers finalized.

### Phase 3 — Tooling
- `Analyzers`: rules for unmasked sensitive fields in logs and responses.
- `Retention`: attribute + expiration hook contracts.
- Benchmark baselines for hot-path operations.

### Phase 4 — 1.0 Release
- API freeze and compatibility pass.
- Performance and security hardening.
- Full XML docs on public APIs.
- Publish 1.0 NuGet packages and release notes.

## Quality Bar

- Strong unit and integration coverage.
- Clear XML docs on public APIs.
- No hidden side effects: all behavior must be explicit and testable.
- Stable APIs across preview iterations.

## Design Principles

- Runtime behavior over compliance paperwork.
- Explicit metadata over implicit heuristics.
- Composition over framework lock-in.
- Developer ergonomics with production-safe defaults.
