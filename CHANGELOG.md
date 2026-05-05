# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
