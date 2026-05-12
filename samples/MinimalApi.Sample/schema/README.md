# MinimalApi.Sample SQLite schema

The MinimalApi.Sample creates its local SQLite tables automatically on startup so
the routes work immediately.

These scripts are kept as an explicit reference for the schema created by the
sample. You can also run them manually if you want to inspect or reset the
databases yourself:

```powershell
sqlite3 sensitiveflow-minimalapi.db ".read schema/sqlite-app.sql"
sqlite3 sensitiveflow-minimalapi-audit.db ".read schema/sqlite-audit.sql"
sqlite3 sensitiveflow-minimalapi-tokens.db ".read schema/sqlite-tokens.sql"
```

The app database stores the sample `Customers` table. The audit database stores
`SensitiveFlow_AuditRecords` and `AuditOutboxEntries`. The token database stores
`SensitiveFlow_TokenMappings`.

SensitiveFlow library packages do not create production schema automatically. In
real applications, prefer EF Core migrations or deployment-owned SQL scripts.
