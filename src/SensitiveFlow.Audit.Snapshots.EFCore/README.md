# SensitiveFlow.Audit.Snapshots.EFCore

Snapshot-based auditing that captures entity state before and after modifications.

## Main Components

### Audit Snapshots
- **`SnapshotAuditInterceptor`** — Captures full entity state
  - Before state (old values)
  - After state (new values)
  - Facilitates "what changed" analysis

## Features

- Full entity serialization (JSON)
- Diff generation between snapshots
- Useful for complex change tracking
- Higher storage overhead than field-level audit

## Usage

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.AddInterceptors(
        sp.GetRequiredService<SnapshotAuditInterceptor>()
    );
});
```

## Possible Improvements

1. **Delta compression** — Only store changed fields
2. **JSON patch format** — RFC 6902 standard format
3. **Version control** — Full history with branching
