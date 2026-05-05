# SensitiveFlow

[![CI](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/ci.yml)
[![CodeQL](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/codeql.yml/badge.svg)](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/SensitiveFlow.Core)](https://www.nuget.org/packages/SensitiveFlow.Core)

MIT-licensed .NET library to support privacy-by-design and regulatory compliance with a modular, opt-in architecture.

## Goals

- Provide framework-agnostic core types and interfaces
- Keep the public API in English for broad adoption
- Support testing and extensibility through interfaces and in-memory stores
- Cover core privacy operations: legal bases, consent, audit, data subject rights, retention, data map, incidents, impact assessment, international transfer, and more

## Packages

| Package | Description | Status |
|---------|-------------|--------|
| `SensitiveFlow.Core` | Public contracts: attributes, enums, interfaces, exceptions | ✅ Preview |
| `SensitiveFlow.Anonymization` | Anonymization and pseudonymization | 🔧 Planned |
| `SensitiveFlow.LegalBasis` | Legal bases management | Planned |
| `SensitiveFlow.Consent` | Consent lifecycle | Planned |
| `SensitiveFlow.Audit` | Immutable audit trail | 🔧 Planned |
| `SensitiveFlow.DataSubject` | Data subject rights workflows | Planned |
| `SensitiveFlow.Retention` | Retention policies | Planned |
| `SensitiveFlow.DataMap` | Processing operations inventory | Planned |
| `SensitiveFlow.Incident` | Security incident handling | Planned |
| `SensitiveFlow.Ripd` | Privacy impact report | Planned |
| `SensitiveFlow.Logging` | Personal data redaction in logs | 🔧 Planned |
| `SensitiveFlow.AspNetCore` | ASP.NET Core middleware | 🔧 Planned |
| `SensitiveFlow.EFCore` | Entity Framework Core interceptors | 🔧 Planned |
| `SensitiveFlow.Analyzers` | Roslyn analyzers for privacy guardrails | Planned |

## Quick Start

```bash
dotnet add package SensitiveFlow.Core
```

```csharp
[PersonalData(Category = DataCategory.Identification,
              LegalBasis = LegalBasis.Consent)]
public string Name { get; set; }
```

See [Getting Started](docs/getting-started.md) for more details.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Attributes](docs/attributes.md)
- [Legal Bases](docs/legal-bases.md)
- [Consent](docs/consent.md)
- [Audit](docs/audit.md)
- [Data Subject Rights](docs/data-subject-rights.md)
- [Retention](docs/retention.md)
- [Data Map](docs/data-map.md)
- [Incidents](docs/incidents.md)
- [Privacy Impact Assessment (PIA)](docs/ripd.md)
- [International Transfer](docs/international-transfer.md)
- [EF Core](docs/efcore.md)
- [ASP.NET Core](docs/aspnetcore.md)
- [Migration Guide](docs/migration.md)

## Status

Work in progress. See [PLAN.md](PLAN.md) for the full roadmap.

## License

MIT

