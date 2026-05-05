# LGPD.NET

[![CI](https://github.com/lgpd-dotnet/lgpd-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/lgpd-dotnet/lgpd-dotnet/actions/workflows/ci.yml)
[![CodeQL](https://github.com/lgpd-dotnet/lgpd-dotnet/actions/workflows/codeql.yml/badge.svg)](https://github.com/lgpd-dotnet/lgpd-dotnet/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/LGPD.NET.Core)](https://www.nuget.org/packages/LGPD.NET.Core)

MIT-licensed .NET library for **LGPD** (Brazilian General Data Protection Law - Law 13.709/2018) compliance with a modular, opt-in design.

## Goals

- Provide framework-agnostic core types and interfaces
- Keep the public API in English for broad adoption
- Support testing and extensibility through interfaces and in-memory stores
- Cover all key LGPD articles: legal bases, consent, audit, data subject rights, retention, data map, incidents, RIPD, international transfer, and more

## Packages

| Package | Description | Status |
|---------|-------------|--------|
| `LGPD.NET.Core` | Public contracts: attributes, enums, interfaces, exceptions | ✅ Preview |
| `LGPD.NET.Anonymization` | Anonymization and pseudonymization | 🔧 Planned |
| `LGPD.NET.LegalBasis` | Legal bases management (Art. 7, 11) | 🔧 Planned |
| `LGPD.NET.Consent` | Consent lifecycle (Art. 7, I and Art. 8) | 🔧 Planned |
| `LGPD.NET.Audit` | Immutable audit trail | 🔧 Planned |
| `LGPD.NET.DataSubject` | Data subject rights (Art. 18) | 🔧 Planned |
| `LGPD.NET.Retention` | Retention policies (Art. 15, 16) | 🔧 Planned |
| `LGPD.NET.DataMap` | Processing operations inventory (Art. 37) | 🔧 Planned |
| `LGPD.NET.Incident` | Security incidents (Art. 46-49) | 🔧 Planned |
| `LGPD.NET.Ripd` | Impact report (Art. 38) | 🔧 Planned |
| `LGPD.NET.Logging` | Personal data redaction in logs | 🔧 Planned |
| `LGPD.NET.AspNetCore` | ASP.NET Core middleware | 🔧 Planned |
| `LGPD.NET.EFCore` | Entity Framework Core interceptors | 🔧 Planned |
| `LGPD.NET.Analyzers` | Roslyn analyzers (LGPD001-LGPD004) | 🔧 Planned |

## Quick Start

```bash
dotnet add package LGPD.NET.Core
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
- [RIPD](docs/ripd.md)
- [International Transfer](docs/international-transfer.md)
- [EF Core](docs/efcore.md)
- [ASP.NET Core](docs/aspnetcore.md)
- [Migration Guide](docs/migration.md)

## Status

Work in progress. See [PLAN.md](PLAN.md) for the full roadmap.

## License

MIT