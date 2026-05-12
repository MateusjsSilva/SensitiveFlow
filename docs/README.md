# SensitiveFlow documentation

Use this page as the documentation index. The files are grouped by task so contributors do not need to scan the whole `docs/` directory to find the right entry point.

## Start here

- [Getting started](getting-started.md): first installation and minimal setup.
- [Package reference](package-reference.md): what each NuGet package contains and when to install it.
- [Attributes](attributes.md): how to classify personal, sensitive, and retention-managed fields.

## Runtime integrations

- [Audit](audit.md): `IAuditStore`, batching, retry, buffering, snapshots, and audit retention concepts.
- [EF Core](efcore.md): `SaveChanges` interceptor and entity requirements.
- [Database providers](database-providers.md): support matrix, schema configuration, and provider-specific notes (SQLite/SQL Server/Postgres).
- [ASP.NET Core](aspnetcore.md): request audit context and IP pseudonymization middleware.
- [JSON redaction](json.md): `System.Text.Json` output protection.
- [Logging](logging.md): `ILogger` redaction provider and limits.
- [Diagnostics](diagnostics.md): OpenTelemetry ActivitySource/Meter integration.
- [Policies, discovery, and health](policies-discovery-health.md): shared profiles, policy rules, discovery reports, startup validation, health checks, and CLI scans.

## Data protection workflows

- [Anonymization](anonymization.md): masking, anonymization, pseudonymization, erasure, export, fingerprints, and token caching.
- [Retention](retention.md): retention evaluation and execution.
- [Backend examples](backends-example.md): example persistence strategies for custom stores.

## Developer tooling

- [Analyzers](analyzers.md): Roslyn diagnostics for privacy anti-patterns.
- [TestKit](testkit.md): contract tests for `IAuditStore` and `ITokenStore`, plus leak assertions.
- [Source generators](package-reference.md#sensitiveflowsourcegenerators): generated metadata overview.
- [SensitiveFlow Tool](policies-discovery-health.md#cli-tool): `sensitiveflow scan` report generation.

## AI usage

- [AI skill guide](ai-skill-sensitiveflow.md): instructions for AI agents that need to apply SensitiveFlow correctly in an application.

## Recommended reading paths

**Quick start (recommended):**

1. [Getting started](getting-started.md) — composition package + EF provider + `AddSensitiveFlowWeb()`.
2. [Package reference / `SensitiveFlow.AspNetCore.EFCore`](package-reference.md#sensitiveflowaspnetcoreefcore) — composition layer reference.
3. [Attributes](attributes.md)

**Full production web API:**

1. [Getting started](getting-started.md)
2. [Package reference](package-reference.md)
3. [Attributes](attributes.md)
4. [Audit](audit.md)
5. [Outbox](outbox-example.md)
6. [EF Core](efcore.md)
7. [ASP.NET Core](aspnetcore.md)
8. [JSON redaction](json.md)
9. [Logging](logging.md)
10. [Diagnostics](diagnostics.md)
11. [Policies, discovery, and health](policies-discovery-health.md)
12. [TestKit](testkit.md)

For data protection workflows:

1. [Anonymization](anonymization.md)
2. [Retention](retention.md)

For advanced composition (per-package control):

1. [Package reference](package-reference.md) — per-package setup matrix with granular `Add*()` calls.

For AI-assisted implementation:

1. [AI skill guide](ai-skill-sensitiveflow.md)
2. [Package reference](package-reference.md)
3. The module-specific guide for the feature being changed.
