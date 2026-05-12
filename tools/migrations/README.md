# SensitiveFlow database schemas

Idempotent SQL scripts for the four SensitiveFlow stores (audit records, outbox,
token mappings, audit snapshots) across SQLite, SQL Server, and PostgreSQL.

The library packages do **not** create production schema on their own. Apply
these scripts (or your own EF Core migrations) before starting the app in
production.

## Layout

```
tools/migrations/
├── sqlite/      # Local development, samples
├── sqlserver/   # SQL Server / Azure SQL
└── postgres/    # PostgreSQL / Aurora / Supabase / Neon
```

Each provider folder contains numbered scripts you can run in order:

| Script              | Required if you use ...                                  |
|---------------------|----------------------------------------------------------|
| `01-audit.sql`      | `SensitiveFlow.Audit.EFCore` (always recommended)        |
| `02-outbox.sql`     | `SensitiveFlow.Audit.EFCore.Outbox` (`EnableOutbox()`)   |
| `03-tokens.sql`     | `SensitiveFlow.TokenStore.EFCore` (pseudonymization)     |
| `04-snapshots.sql`  | `SensitiveFlow.Audit.Snapshots.EFCore` (point-in-time)   |

All scripts are idempotent (`CREATE TABLE IF NOT EXISTS` / `IF OBJECT_ID IS NULL`),
so it is safe to re-run them during deployment.

## Applying the scripts

### SQLite

```powershell
sqlite3 mydb.db ".read tools/migrations/sqlite/01-audit.sql"
sqlite3 mydb.db ".read tools/migrations/sqlite/02-outbox.sql"
sqlite3 mydb.db ".read tools/migrations/sqlite/03-tokens.sql"
```

### SQL Server

```powershell
sqlcmd -S server -d database -i tools/migrations/sqlserver/01-audit.sql
sqlcmd -S server -d database -i tools/migrations/sqlserver/02-outbox.sql
sqlcmd -S server -d database -i tools/migrations/sqlserver/03-tokens.sql
```

### PostgreSQL

```bash
psql -d mydb -f tools/migrations/postgres/01-audit.sql
psql -d mydb -f tools/migrations/postgres/02-outbox.sql
psql -d mydb -f tools/migrations/postgres/03-tokens.sql
```

## EF Core migrations

If you prefer EF Core migrations, generate them against your own
`DbContext` once you have added the SensitiveFlow EF Core packages:

```bash
dotnet ef migrations add SensitiveFlowInitial --context AuditDbContext
dotnet ef database update --context AuditDbContext
```

The included scripts mirror what EF Core would emit for the default table
names and `null` schema (so they remain provider-agnostic by default).
