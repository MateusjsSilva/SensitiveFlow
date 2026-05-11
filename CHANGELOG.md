# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0-preview.3] - 2026-05-11

### Added

- **Durable audit outbox framework**: `IDurableAuditOutbox` contract with `DequeueBatchAsync`, `MarkProcessedAsync`, `MarkFailedAsync` for production-grade delivery guarantees.
- **Audit outbox entries**: `AuditOutboxEntry` model tracking attempts, timestamps, errors, and dead-letter status for reliable retry logic.
- **Outbox publisher abstraction**: `IAuditOutboxPublisher` for pluggable downstream delivery (SIEM, webhooks, event buses, etc.).
- **Audit outbox dispatcher**: `AuditOutboxDispatcher` background service with configurable polling, exponential backoff, and dead-lettering.
- **Metrics & diagnostics**: New diagnostic codes SF-CONFIG-013 (in-memory outbox outside Development) and SF-CONFIG-014 (durable outbox without publishers).
- **Health checks**: `AuditOutboxHealthCheck` detects in-memory outbox usage in production.
- **EF Core durable outbox** (`SensitiveFlow.Audit.EFCore.Outbox` package): `EfCoreAuditOutbox` with transactional guarantees (audit + outbox in single SaveChanges).
- **Audit outbox diagnostics class**: `SensitiveFlowAuditDiagnostics` tracks enqueued/dispatched/failed/dead-lettered/pending record counts.

### Deprecated

- `InMemoryAuditOutbox` — use `AddEfCoreAuditOutbox()` or custom `IDurableAuditOutbox` for production.
- `AddInMemoryAuditOutbox()` — same guidance as above.

### Changed

- Audit store decorator `OutboxAuditStore` now reports metrics via `SensitiveFlowAuditDiagnostics`.
- `SensitiveFlowConfigurationValidator` now checks for in-memory outbox outside Development (SF-CONFIG-013).
- Docs: `docs/audit.md` updated with at-least-once vs at-most-once delivery matrix and production-ready outbox patterns.

### Security

- Durable outbox prevents audit record loss on process restart or failure — critical for compliance.
- Transactional outbox pattern ensures audit writes and outbox enqueuing happen atomically.

## [1.0.0-preview.2] - 2026-05-10

### Added

- Logo (`assets/logo.png`) added to repository and configured as `PackageIcon` for all NuGet packages.
- `SECURITY.md` with supported versions table, vulnerability reporting process, and security scope.
- `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1).
- `RELEASE.md` with full versioning scheme and publishing process.
- Legal disclaimer in README: "SensitiveFlow helps reduce accidental exposure of sensitive data, but it does not guarantee legal compliance or complete data protection by itself."
- New README badges: Container Tests, NuGet Downloads, .NET 8 | 9 | 10.

### Changed

- CI matrix now includes `net9.0` alongside `net8.0` and `net10.0`.
- All GitHub Actions workflows force Node.js 24 (`FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true`) to resolve deprecation warnings.
- Container tests moved out of `ci.yml` into dedicated `container-tests.yml` (manual + weekly) and `release.yml` (before publish).
- `bug_report.md` issue template translated to English.
- `CONTRIBUTING.md` updated with link to `RELEASE.md`.
- CI optimized: PRs run only `net10.x` for fast feedback; pushes to `main`/`develop` run full matrix (`net8.x`, `net9.x`, `net10.x`).

## [1.0.0-preview.1] - 2026-05-10

### Added

- `SensitiveFlow.Audit` - `RetryingAuditStore` decorator with bounded exponential backoff for transient append failures; opt-in via `AddAuditStoreRetry`.
- `SensitiveFlow.Audit.EFCore` - durable EF Core implementation of `IAuditStore`/`IBatchAuditStore` with maintenance helper for audit log retention.
- `SensitiveFlow.Anonymization` - `IDataSubjectErasureService` and `RedactionErasureStrategy` for right-to-be-forgotten flows; opt-in via `AddDataSubjectErasure`.
- `SensitiveFlow.Analyzers.CodeFixes` - quick-fix providers for SF0001/SF0002 that wrap sensitive expressions with `.MaskEmail()` / `.MaskPhone()` / `.MaskName()` based on the member name.
- `SensitiveFlow.Analyzers` - new SF0003 rule for entities with sensitive members missing `DataSubjectId`/`UserId`.
- `SensitiveFlow.Diagnostics` - OpenTelemetry bridge emitting `ActivitySource` spans and `Meter` metrics for audit/log redaction.
- `SensitiveFlow.Retention` - `RetentionExecutor` to auto-apply `AnonymizeOnExpiration` and report pending delete/notify/block actions.
- `SensitiveFlow.SourceGenerators` - source generator for precomputing sensitive/retention member metadata.
- `SensitiveFlow.TestKit` - xUnit conformance base classes (`AuditStoreContractTests`, `TokenStoreContractTests`) for validating custom store implementations.
- `SensitiveFlow.Core` - `SensitiveMemberCache` shared reflection cache used by the EF Core interceptor and the retention evaluator (eliminates per-call `GetProperties` / `Attribute.IsDefined` cost).
- `SensitiveFlow.Benchmarks` - real BenchmarkDotNet suites for masking, pseudonymization, and reflection caching.
- Sample: Redis-backed `ITokenStore` implementation for distributed pseudonymization.
- `SensitiveFlow.TokenStore.EFCore` - durable EF Core implementation of `ITokenStore` with unique index for concurrency-safe `GetOrCreateTokenAsync`; registers `TokenPseudonymizer` as `IPseudonymizer` automatically.
- `SensitiveFlow.Audit.Snapshots.EFCore` - durable EF Core implementation of `IAuditSnapshotStore` with `SnapshotDbContext` and indexes optimized for aggregate and data-subject queries.
- `BufferedAuditStore` - health checks via `GetHealth()` returning `BufferedAuditStoreHealth` (pending/dropped/flush failures/isFaulted); OpenTelemetry metrics: `sensitiveflow.audit.buffer.pending` (gauge), `.dropped` (counter), `.flush_failures` (counter).
- Container tests: SQL Server (`SqlServerAuditStoreContainerTests`) and Redis (`RedisTokenStoreContainerTests` with atomic Lua scripts) added alongside existing PostgreSQL coverage.

### Changed

- **(Breaking)** `SensitiveDataAuditInterceptor` now requires the entity to expose `DataSubjectId` (or the `UserId` legacy alias). Falling back to a database-generated `Id` was removed: EF providers can assign auto-increment keys before the interceptor runs, which silently grouped unrelated audit rows under whatever `Id` the database happened to allocate. Add a `DataSubjectId` to your entities before upgrading.
- `RetentionDataAttribute.Years` and `Months` now reject negative values with `ArgumentOutOfRangeException`.
- `HmacPseudonymizer` now validates `secretKey` by UTF-8 byte length (>=32 bytes) instead of character count, matching the SHA-256 digest size precisely.
- `HmacPseudonymizer.ReverseAsync` returns a faulted task instead of throwing synchronously, so async callers observe the failure via the task and not via the call site.
- `RedactingLogger` rebuilds the rendered message from `{OriginalFormat}` using the redacted structured values; the previous global string-replace approach corrupted unrelated fields when a sensitive value happened to appear as a substring.
- `HttpAuditContext.ActorId` now also checks `ClaimTypes.NameIdentifier` (the default Microsoft mapping for the `sub` claim) before falling back to `Identity.Name`.
- `HashStrategy` switches from `salt + value` concatenation to `HMAC-SHA256(salt, value)` when salted, eliminating the `("salt","value")` vs `("saltv","alue")` ambiguity.
- `StringAnonymizationExtensions.PseudonymizeHmac` caches `HmacPseudonymizer` instances per secret key.
- Sample `EfCoreTokenStore` adds a unique index on `Value` and recovers from concurrent insert collisions.
- Sample/EF Core: SHA-256 hex now uses `Convert.ToHexStringLower` on .NET 9+ (faster, less allocation) and falls back to `Convert.ToHexString().ToLowerInvariant()` on .NET 8.

### Initial Package Surface

- `SensitiveFlow.Core` package with:
  - Attributes: `PersonalData`, `SensitiveData`, `EraseData`, `RetentionData`, `InternationalTransfer`
  - Enums: `DataCategory`, `ProcessingPurpose`, `LegalBasis`, `SensitiveLegalBasis`, `ProcessingAgentRole`, `ProcessingPrinciple`, `DataSubjectKind`, `AnonymizationType`, `RiskLevel`, `TransferCountry`, `SafeguardMechanism`, `RetentionPolicy`, `DataSubjectRequestType`, `DataSubjectRequestStatus`, `AuditOperation`, `IncidentNature`, `IncidentSeverity`, `IncidentStatus`
  - Interfaces: `IAnonymizer`, `IPseudonymizer`, `IDataSubject`, `IConsentStore`, `IAuditStore`, `IProcessingInventory`, `IIncidentStore`
  - Models: `ConsentRecord`, `AuditRecord`, `DataSubjectRequest`, `ProcessingOperationRecord`, `DataSharingRecord`, `IncidentRecord`
  - Exceptions: `ConsentNotFoundException`, `DataNotFoundException`, `InternationalTransferNotAllowedException`, `RetentionExpiredException`
- CI/CD workflows: `ci.yml`, `release.yml`, `codeql.yml`
- Issue templates and pull request template
- `.editorconfig` with C# coding conventions
- Central Package Management via `Directory.Packages.props`
- Multi-target: `net8.0`, `net9.0`, and `net10.0`
- Documentation: getting-started, attributes, legal-bases, consent, audit, data-subject-rights, retention, data-map, incidents, ripd, international-transfer, efcore, aspnetcore, migration




