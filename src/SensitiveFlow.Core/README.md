# SensitiveFlow.Core

Core of the library with attributes, enums, interfaces, and models shared across all packages.

## Main Components

### Attributes
- **`[PersonalData]`** — Marks properties as personal data (name, email, phone, etc.)
  - `Category`: Data category (Contact, Identification, Financial, Biometric, etc.)
  - `Sensitivity`: Risk level for policy decisions (Low, Medium, High). Default: Medium.
  
- **`[SensitiveData]`** — Marks properties as sensitive data (tokens, passwords, keys)
  - `Category`: Sensitivity category
  - `Sensitivity`: Risk level for policy decisions (Low, Medium, High). Default: High.
  
- **`[Redaction]`** — Controls how data is displayed in different contexts
  - `ForContext(RedactionContext)`: Returns action per context (ApiResponse, Logs, Audit, Export)
  - Possible values: `None`, `Redact`, `Mask`, `Omit`, `Pseudonymize`

### Additional Attributes (Shorthand & Control)

- **`[Mask(...)]`** — Shorthand for masking in API responses and logs
  - `Kind`: Mask type (Email, Phone, Name, Ssn, Generic, etc.)
  - Equivalent to: `[Redaction(ApiResponse = Mask, Logs = Mask)]`
  - Example: `[PersonalData][Mask(MaskKind.Email)] public string Email { get; set; }`

- **`[Redact]`** — Shorthand for redacting in API responses and logs
  - Equivalent to: `[Redaction(ApiResponse = Redact, Logs = Redact)]`
  - Example: `[SensitiveData][Redact] public string ApiKey { get; set; }`

- **`[Omit]`** — Shorthand for omitting from API responses and logs
  - Equivalent to: `[Redaction(ApiResponse = Omit, Logs = Omit)]`
  - Property completely excluded from JSON responses and log messages
  - Example: `[PersonalData][Omit] public string InternalNotes { get; set; }`

- **`[AllowSensitiveLogging]`** — Suppresses SF0001 analyzer warning
  - Use when logging sensitive data is intentional and necessary for diagnostics
  - Requires `Justification` explaining why this exception is needed
  - Example: `[PersonalData][AllowSensitiveLogging(Justification = "Email used as correlation key in error logs")] public string Email { get; set; }`

- **`[AllowSensitiveReturn]`** — Suppresses SF0002 analyzer warning
  - Use when returning sensitive data is intentional and protected by authorization
  - Requires `Justification`
  - Example: `[PersonalData][AllowSensitiveReturn(Justification = "Endpoint is [Authorize]-protected")] public string Email { get; set; }`

- **`[RetentionData]`** — Declares data retention and expiration policy
  - `Years`: How many years to retain before action
  - `Policy`: Action on expiration (AnonymizeOnExpiration, DeleteOnExpiration)
  - Example: `[SensitiveData][RetentionData(Years = 5, Policy = RetentionPolicy.DeleteOnExpiration)] public string TaxId { get; set; }`

- **`[SensitiveFlowIgnore]`** — Excludes property from SensitiveFlow processing
  - Property will not be audited, masked, redacted, or included in exports
  - Use for non-sensitive columns that happen to be in a sensitive entity

- **`[CompositeDataSubjectId(...)]`** — Declares multiple subject identifier properties
  - Use when entity is identified by multiple properties (e.g., CustomerId + OrderId)
  - Properties listed combine into audit trail key: `"customerId:123;orderId:456"`
  - Example: `[CompositeDataSubjectId("CustomerId", "OrderId")]`
  - Alternative to single DataSubjectId when multiple keys are needed

### Enums
- **`DataCategory`** — Personal data categories (Contact, Identification, Financial, Biometric, Location, Health, Preference)
- **`SensitiveDataCategory`** — Sensitive data categories (Credential, Authentication, Financial, Encryption, Token)
- **`OutputRedactionAction`** — Redaction actions (None, Redact, Mask, Omit, Pseudonymize)
- **`RedactionContext`** — Contexts where redaction applies
  - ApiResponse, Log, Audit, Export (standard contexts)
  - AdminView, SupportView, CustomerView (role-based contexts)
  - Used with `[Redaction]` to define per-context behavior
- **`AuditOperation`** — Types of audited operations (Access, Create, Update, Delete, Export)
- **`DataSensitivity`** — Risk levels used by policies, analyzers, and discovery reports
  - Values: Low, Medium, High, Critical
  - Low: Already public or weakly identifying data
  - Medium: Moderate risk if exposed (default for `[PersonalData]`)
  - High: High risk if exposed (default for `[SensitiveData]`)
  - Critical: Strictest handling required (credentials, tokens, encryption keys)
  - Determines strictness of protection and policy decisions
- **`MaskKind`** — Mask types for `[Mask(...)]` attribute
  - Values: Generic, Email, Phone, Name
  - Determines output format (e.g., `a****@example.com` for Email, `555-****` for Phone)
  - Used by logging and API response redaction

### Interfaces
- **`IAuditContext`** — Provides per-request context for auditing (ActorId, IpAddressToken)
- **`IAuditStore`** — Interface for audit record persistence
- **`IBatchAuditStore`** — Extension allowing batch record insertion
- **`IPseudonymizer`** — Interface for data pseudonymization
- **`IDataSubjectExportService`** — Interface for data subject export

### Models
- **`AuditRecord`** — Represents an audit trail entry
  - `DataSubjectId`: Subject identifier (mandatory)
  - `Entity`: Name of modified entity
  - `Field`: Modified field
  - `Operation`: Operation type (Create, Update, Delete, Access)
  - `Timestamp`: When it occurred
  - `ActorId`: Who performed it (optional)
  - `IpAddressToken`: Origin IP token (optional)
  - `Details`: Redaction details applied (optional)

### Reflection Cache
- **`SensitiveMemberCache`** — Thread-safe reflection cache for finding annotated properties
  - `GetSensitiveProperties(Type)`: Returns `[PersonalData]` or `[SensitiveData]` properties
  - Avoids repeated reflection per type

## Redaction Behavior

### By Context
- **ApiResponse**: Masks/omits values returned in HTTP responses
- **Logs**: Masks/omits values in log messages
- **Audit**: Never omits the field name, may mask the value in `Details`
- **Export**: Controls inclusion in exports
- **AdminView**: Administrator view - full access, typically no redaction
- **SupportView**: Support/helpdesk view - partial access, conditional redaction
- **CustomerView**: Customer self-service view - restricted access, heavy redaction

### Role-Based Redaction Example
Different users see different levels of data:

```csharp
[PersonalData(Category = DataCategory.Contact)]
[Redaction(
    ApiResponse = OutputRedactionAction.Mask,      // Default: masked email
    AdminView = OutputRedactionAction.None,        // Admins see full email
    SupportView = OutputRedactionAction.Mask,      // Support sees masked
    CustomerView = OutputRedactionAction.Redact,   // Customers see [REDACTED]
    Logs = OutputRedactionAction.Redact,
    Audit = OutputRedactionAction.Mask,
    Export = OutputRedactionAction.None             // Full value in exports
)]
public string Email { get; set; }
```

When rendering response, application calls:
```csharp
var action = emailProperty.Redaction.ForContext(
    userRole == "Admin" ? RedactionContext.AdminView :
    userRole == "Support" ? RedactionContext.SupportView :
    userRole == "Customer" ? RedactionContext.CustomerView :
    RedactionContext.ApiResponse
);
// Apply action (Mask, Redact, Omit, etc.)
```

### Semantics
- `None`: Include full value
- `Redact`: Replace with `[REDACTED]`
- `Mask`: Show partially (e.g., a****@x.com for email)
- `Omit`: Exclude completely (doesn't appear in JSON, logs, exports)
- `Pseudonymize`: Replace with pseudonym token

**IMPORTANT**: In audit records, `Omit` affects only data output (`Details`), never omits the field from the record itself.

## Dependencies
- None (only .NET)
