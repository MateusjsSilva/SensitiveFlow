# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `SensitiveFlow.Core` policy engine primitives: `SensitiveFlowOptions`, built-in profiles, category policy registry, output behavior attributes, contextual redaction attributes, data sensitivity levels, discovery reports, export formatters, audit correlation helpers, audit outbox interfaces, data-subject request interfaces, and privacy-safe exception types.
- `SensitiveFlow.Tool` command-line project with `sensitiveflow scan <assembly-or-directory> [output-directory]` for JSON/Markdown discovery reports.
- `SensitiveFlow.HealthChecks` package with audit/token store health checks.
- `SensitiveFlow.Diagnostics` startup validation via `AddSensitiveFlowValidation(...)` and `ValidateSensitiveFlow()`.
- `SensitiveFlow.Retention` dry-run execution via `RetentionExecutor.DryRunAsync(...)`.
- `SensitiveFlow.TestKit` expanded assertions: `ContainsMaskedEmail`, `DoesNotContainRawValues`, `JsonDoesNotExposeAnnotatedProperties`, and `LogsDoNotContainSensitiveValues`.
- `SensitiveFlow.TestKit` contract test bases for `IAuditSnapshotStore`, `IPseudonymizer`, `IMasker`, `IAnonymizer`, and `IRetentionExpirationHandler`.
- `SensitiveFlow.Json` now honors output attributes, contextual API response redaction, and category policies before falling back to the configured default mode.
- `SensitiveDataAssert.DoesNotContainAny` - checks a payload against explicit string values without requiring an annotated entity.
- `SensitiveDataAssert.DoesNotLeakKnownValues` - same as `DoesNotContainAny` but accepts `IEnumerable<string>` for readability.

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




