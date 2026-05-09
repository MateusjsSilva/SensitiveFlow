# SensitiveFlow documentation

Use this page as the documentation index. The files are grouped by task so contributors do not need to scan the whole `docs/` directory to find the right entry point.

## Start here

- [Getting started](getting-started.md): first installation and minimal setup.
- [Package reference](package-reference.md): what each NuGet package contains and when to install it.
- [Attributes](attributes.md): how to classify personal, sensitive, and retention-managed fields.

## Runtime integrations

- [Audit](audit.md): `IAuditStore`, batching, retry, buffering, snapshots, and audit retention concepts.
- [EF Core](efcore.md): `SaveChanges` interceptor and entity requirements.
- [ASP.NET Core](aspnetcore.md): request audit context and IP pseudonymization middleware.
- [JSON redaction](json.md): `System.Text.Json` output protection.
- [Logging](logging.md): `ILogger` redaction provider and limits.
- [Diagnostics](diagnostics.md): OpenTelemetry ActivitySource/Meter integration.

## Data protection workflows

- [Anonymization](anonymization.md): masking, anonymization, pseudonymization, erasure, export, fingerprints, and token caching.
- [Retention](retention.md): retention evaluation and execution.
- [Backend examples](backends-example.md): example persistence strategies for custom stores.

## Developer tooling

- [Analyzers](analyzers.md): Roslyn diagnostics for privacy anti-patterns.
- [TestKit](testkit.md): contract tests for `IAuditStore` and `ITokenStore`, plus leak assertions.
- [Source generators](package-reference.md#sensitiveflowsourcegenerators): generated metadata overview.

## Production and release planning

- [TestKit](testkit.md): reusable conformance tests for custom stores.
- [Backend examples](backends-example.md): production-oriented persistence examples.

## AI usage

- [AI skill guide](ai-skill-sensitiveflow.md): instructions for AI agents that need to apply SensitiveFlow correctly in an application.

## Recommended reading paths

For a web API using EF Core:

1. [Getting started](getting-started.md)
2. [Package reference](package-reference.md)
3. [Attributes](attributes.md)
4. [Audit](audit.md)
5. [EF Core](efcore.md)
6. [ASP.NET Core](aspnetcore.md)
7. [JSON redaction](json.md)
8. [TestKit](testkit.md)

For production hardening:

1. [Package reference](package-reference.md)
2. [Diagnostics](diagnostics.md)
3. [Audit](audit.md)
4. [Retention](retention.md)
5. [TestKit](testkit.md)

For AI-assisted implementation:

1. [AI skill guide](ai-skill-sensitiveflow.md)
2. [Package reference](package-reference.md)
3. The module-specific guide for the feature being changed.
