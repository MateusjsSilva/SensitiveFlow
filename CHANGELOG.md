# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `SensitiveFlow.Audit` — `RetryingAuditStore` decorator with bounded exponential backoff for transient append failures; opt-in via `AddAuditStoreRetry`.
- `SensitiveFlow.Anonymization` — `IDataSubjectErasureService` and `RedactionErasureStrategy` for right-to-be-forgotten flows; opt-in via `AddDataSubjectErasure`.
- `SensitiveFlow.Analyzers.CodeFixes` — quick-fix providers for SF0001/SF0002 that wrap sensitive expressions with `.MaskEmail()` / `.MaskPhone()` / `.MaskName()` based on the member name.
- `SensitiveFlow.TestKit` — xUnit conformance base classes (`AuditStoreContractTests`, `TokenStoreContractTests`) for validating custom store implementations.
- `SensitiveFlow.Core` — `SensitiveMemberCache` shared reflection cache used by the EF Core interceptor and the retention evaluator (eliminates per-call `GetProperties` / `Attribute.IsDefined` cost).
- `SensitiveFlow.Benchmarks` — real BenchmarkDotNet suites for masking, pseudonymization, and reflection caching.

### Changed

- **(Breaking)** `SensitiveDataAuditInterceptor` now requires the entity to expose `DataSubjectId` (or the `UserId` legacy alias). Falling back to a database-generated `Id` was removed: EF providers can assign auto-increment keys before the interceptor runs, which silently grouped unrelated audit rows under whatever `Id` the database happened to allocate. Add a `DataSubjectId` to your entities before upgrading.
- `RetentionDataAttribute.Years` and `Months` now reject negative values with `ArgumentOutOfRangeException`.
- `HmacPseudonymizer` now validates `secretKey` by UTF-8 byte length (≥32 bytes) instead of character count, matching the SHA-256 digest size precisely.
- `HmacPseudonymizer.ReverseAsync` returns a faulted task instead of throwing synchronously, so async callers observe the failure via the task and not via the call site.
- `RedactingLogger` rebuilds the rendered message from `{OriginalFormat}` using the redacted structured values; the previous global string-replace approach corrupted unrelated fields when a sensitive value happened to appear as a substring.
- `HttpAuditContext.ActorId` now also checks `ClaimTypes.NameIdentifier` (the default Microsoft mapping for the `sub` claim) before falling back to `Identity.Name`.
- `HashStrategy` switches from `salt + value` concatenation to `HMAC-SHA256(salt, value)` when salted, eliminating the `("salt","value")` vs `("saltv","alue")` ambiguity.
- `StringAnonymizationExtensions.PseudonymizeHmac` caches `HmacPseudonymizer` instances per secret key.
- Sample `EfCoreTokenStore` adds a unique index on `Value` and recovers from concurrent insert collisions.
- Sample/EF Core: SHA-256 hex now uses `Convert.ToHexStringLower` on .NET 9+ (faster, less allocation) and falls back to `Convert.ToHexString().ToLowerInvariant()` on .NET 8.

### Added

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
- Multi-target: `net8.0` and `net10.0`
- Documentation: getting-started, attributes, legal-bases, consent, audit, data-subject-rights, retention, data-map, incidents, ripd, international-transfer, efcore, aspnetcore, migration
