# SensitiveFlow.Anonymization

Data subject export and anonymization support for managing sensitive data lifecycles.

## Main Components

### Data Subject Export
- **`DataSubjectExporter<T>`** — Exports all data for a subject
  - Queries EF Core DbContext for all entities matching subject ID
  - Includes related entities (navigation properties)
  - Applies `[Redaction(Export=...)]` per field
  - Returns structured export (JSON or CSV)

- **`IDataSubjectExportService`** — Interface for DI
  - `ExportAsync(string dataSubjectId)`: Export all data
  - `AnonymizeAsync(string dataSubjectId)`: Anonymize/delete all data

### Models
- **`DataSubjectExport`** — Contains exported data
  - `DataSubjectId`: Subject identifier
  - `Entities`: Dictionary of entity type → records
  - `ExportedAt`: Timestamp
  - `AuditTrail`: Related audit records

## How It Works

### Export Flow
```
Export request for subject-123
    ↓
DataSubjectExporter queries DbContext
    ↓
Finds all entities with DataSubjectId == "subject-123"
    ↓
Includes related navigation properties
    ↓
Applies [Redaction(Export=...)] to each field
    ↓
Returns structured export (JSON or CSV)
    ↓
Deliver to subject (encrypted channel)
```

### Anonymization Flow
```
Erasure request
    ↓
DataSubjectExporter identifies all entities
    ↓
Options:
  1. Hard delete (complete removal)
  2. Logical delete (set is_deleted = true)
  3. Anonymize (remove identifiers, keep for stats)
    ↓
Execute deletion/anonymization
    ↓
Log to audit trail (who deleted, when)
```

## Redaction Contexts

### Export Context
Use `[Redaction(Export=...)]` to control data export:

```csharp
[PersonalData]
[Redaction(
    Export = OutputRedactionAction.None,  // Full value in export
    ApiResponse = OutputRedactionAction.Mask,  // Masked in API
    Logs = OutputRedactionAction.Redact,  // Redacted in logs
    Audit = OutputRedactionAction.Mask  // Masked in audit
)]
public string Email { get; set; }
```

### Actions
- `None` — Include full value (export includes all data)
- `Redact` — Replace with `[REDACTED]`
- `Mask` — Partial masking
- `Omit` — Exclude from export entirely (only if truly not subject's data)
- `Pseudonymize` — Replace with token

## Usage

### Registration
```csharp
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAnonymization();
```

### Export All Data
```csharp
public sealed class DsarController : ControllerBase
{
    private readonly IDataSubjectExportService _exporter;

    [Authorize]
    [HttpPost("dsar")]
    public async Task<IActionResult> RequestExport()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var export = await _exporter.ExportAsync(userId!);

        // Encrypt and deliver via secure channel
        var json = JsonSerializer.Serialize(export);
        return Ok(json);
    }
}
```

### Anonymize Subject
```csharp
[Authorize]
[HttpPost("erasure")]
public async Task<IActionResult> RequestErasure()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    // Audit trail logs who initiated erasure
    await _exporter.AnonymizeAsync(userId!);
    
    return Ok("Right to erasure processed");
}
```

### Query Audit Trail
```csharp
var auditRecords = await auditStore.QueryAsync(
    new AuditQuery()
        .ByDataSubjectId("subject-123")
        .OrderByNewest()
        .WithPagination(0, 1000)
);
```

## Navigation Property Handling

Exporter automatically includes related data:

```csharp
public class Customer
{
    public string DataSubjectId { get; set; }
    [PersonalData]
    public string Email { get; set; }

    // Navigation: automatically exported
    public List<Order> Orders { get; set; }
}

public class Order
{
    public string DataSubjectId { get; set; }  // Foreign key reference
    [PersonalData]
    public string ShippingAddress { get; set; }
}
```

Export includes all related Orders with matching DataSubjectId.

## Export Formats

### JSON (Default)
```csharp
var json = JsonSerializer.Serialize(export);
```

### CSV (with helper)
```csharp
var csv = export.ToCsv();  // Flattens structure
```

## Anonymization Strategies

### Hard Delete (Recommended)
```csharp
// Removes all traces
await _exporter.AnonymizeAsync(userId, strategy: AnonymizationStrategy.Delete);
```

### Logical Delete
```csharp
// Sets is_deleted = true, keeps for audit
await _exporter.AnonymizeAsync(userId, strategy: AnonymizationStrategy.LogicalDelete);
```

### Pseudonymization
```csharp
// Replaces PII with tokens
await _exporter.AnonymizeAsync(userId, strategy: AnonymizationStrategy.Pseudonymize);
```

## Audit Trail

Erasure operations are logged:

```json
{
  "dataSubjectId": "subject-123",
  "entity": "Customer",
  "field": "Email",
  "operation": "Delete",
  "timestamp": "2024-01-15T10:30:00Z",
  "actorId": "admin-456",
  "details": "Right to erasure (hard delete)"
}
```

## Data Export & Erasure

- **Structured export**: Export in structured format (JSON/CSV)
- **Erasure support**: Delete or anonymize all data
- **Audit trail**: Complete record of deletions for accountability

## Possible Improvements

1. **Incremental exports** — Export only changes since last request
2. **Streaming large exports** — For subjects with gigabytes of data
3. **Automated export workflow** — Email/secure portal delivery
4. **Linked data discovery** — Find related subjects via foreign keys
5. **Anonymization validation** — Detect if truly anonymized (k-anonymity, l-diversity)
6. **Audit trail export** — Data export includes audit trail records
