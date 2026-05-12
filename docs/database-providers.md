# Database providers

SensitiveFlow's EF Core packages are **provider-agnostic**: the library never calls
`UseSqlServer`, `UseSqlite`, or `UseNpgsql` on your behalf. You pick the provider in
your own `optionsBuilder` and SensitiveFlow uses whatever you wire in.

This page documents what works out of the box, and the small caveats per provider.

## Support matrix

| Feature                                | SQLite | SQL Server | PostgreSQL | MySQL / MariaDB | Oracle |
|----------------------------------------|--------|------------|------------|-----------------|--------|
| `SensitiveFlow.Audit.EFCore`           | ✅      | ✅          | ✅          | ✅ (1)           | ✅ (1)  |
| `SensitiveFlow.TokenStore.EFCore`      | ✅      | ✅          | ✅          | ✅ (1)           | ✅ (1)  |
| `SensitiveFlow.Audit.EFCore.Outbox`    | ✅      | ✅          | ✅          | ✅ (1)           | ✅ (1)  |
| `SensitiveFlow.Audit.Snapshots.EFCore` | ✅      | ✅          | ✅          | ✅ (1)           | ✅ (1)  |
| Schemas (`ToTable(name, "schema")`)    | ❌ (2)  | ✅          | ✅          | ❌               | ✅      |
| `EnsureCreatedAsync` for samples       | ✅      | ✅          | ✅          | ✅               | ✅      |
| `ExecuteDeleteAsync` (retention)       | ✅      | ✅          | ✅          | ✅               | ✅      |

1. Pass an EF Core provider package (e.g. `Pomelo.EntityFrameworkCore.MySql`,
   `Oracle.EntityFrameworkCore`) and call `UseMySql(...)` / `UseOracle(...)` in
   your `optionsBuilder`. Nothing in SensitiveFlow is SQL Server-specific.
2. SQLite does not implement schemas. The default `ToTable(...)` calls in
   SensitiveFlow's configurations omit the schema for this reason; if you opt
   into a schema for SQL Server / Postgres, **do not pass one for SQLite**.

## Ordering and timestamps

The outbox stores both `EnqueuedAt` (`DateTimeOffset`) and `EnqueuedAtTicks`
(`long`). `EnqueuedAtTicks` exists because some providers (notably SQLite) cannot
ORDER BY `DateTimeOffset` server-side. Sorting the outbox queue uses
`EnqueuedAtTicks`, which is a plain integer and works identically on every
backend.

If you maintain your own EF Core migrations, make sure both columns and the
matching index (`IX_AuditOutboxEntries_EnqueuedAtTicks`) are part of them.

## Configuring custom table names and schemas

Every entity configuration accepts both a table name and an optional schema:

```csharp
modelBuilder.ApplyConfiguration(new AuditRecordEntityTypeConfiguration(
    tableName: "MyAuditTrail",
    schema:    "compliance"));   // null on SQLite

modelBuilder.ApplyConfiguration(new TokenMappingEntityTypeConfiguration(
    tableName: "MyTokenStore"));  // schema defaults to null

modelBuilder.ApplyConfiguration(new AuditOutboxEntryConfiguration(
    tableName: "AuditOutbox",
    schema:    "sensitiveflow"));
```

If you stick with the defaults, you can omit the call entirely — SensitiveFlow's
`AuditDbContext` and `TokenDbContext` register the right configurations for you.

## SQL Server

- Install `Microsoft.EntityFrameworkCore.SqlServer` in the project that owns the
  `DbContext`.
- The provided `tools/migrations/sqlserver/*.sql` scripts deploy into the
  default `dbo` schema. Create a dedicated schema first (e.g.
  `CREATE SCHEMA sensitiveflow;`) and update the configurations if you prefer
  isolation.
- `NVARCHAR(MAX)` is used for unbounded JSON payloads. Index pages have a 1700-byte
  limit; the SensitiveFlow indexes only target bounded columns.

## PostgreSQL

- Install `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Default deployment uses the `public` schema. Use the `schema:` parameter or
  `npgsql.SetPostgresExtension("...")` for custom schemas.
- The provided scripts use `TIMESTAMPTZ` for outbox timestamps so they round-trip
  cleanly through `DateTimeOffset`.

## SQLite

- Install `Microsoft.EntityFrameworkCore.Sqlite`.
- **Never pass a non-null schema** — SQLite ignores schemas and EF Core logs a
  warning. The SensitiveFlow defaults already omit the schema.
- Use SQLite only for samples, tests, or single-process apps. The library's
  outbox polling is safe on SQLite, but SQLite's writer lock will bottleneck
  high-throughput workloads.

## MySQL / MariaDB / Oracle

These providers are supported transitively (nothing in the library targets a
specific dialect), but they are **not part of the test matrix**. If you adopt
SensitiveFlow on one of them, please:

1. Run `samples/WebApi.Sample` or `samples/MinimalApi.Sample` against your
   provider with `EnsureCreatedAsync()` to verify model compilation.
2. Open an issue if any DDL fails — we will accept a `tools/migrations/<provider>/`
   contribution.

## Schema-not-initialized errors

When the runtime cannot find a SensitiveFlow table, the EF Core stores throw
`SensitiveFlowSchemaNotInitializedException` (code `SF-SCHEMA-001`). The message
includes the missing table name (when detectable) and points back to this page.

To reproduce the friendly error: delete the database file and run the sample
without `EnsureCreatedAsync()`.

## Picking the right entry point

| You want to ...                              | Use ...                                                  |
|----------------------------------------------|----------------------------------------------------------|
| Production deployment                        | EF Core migrations **or** `tools/migrations/<provider>/` |
| Try the library locally in 30 seconds        | `EnsureCreatedAsync()` in `samples/*`                    |
| Isolate audit/token state per environment    | Separate `DbContext` + connection string per store       |
| Run the outbox on Postgres + dedicated schema| Pass `schema: "sensitiveflow"` to the configuration      |
