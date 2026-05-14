# SensitiveFlow.Core

Core of the library with attributes, enums, interfaces, and models shared across all packages.

## Main Components

### Attributes
- **`[PersonalData]`** — Marks properties as personal data (name, email, phone, etc.)
  - `Category`: Data category (Contact, Identification, Financial, Biometric, etc.)
  
- **`[SensitiveData]`** — Marks properties as sensitive data (tokens, passwords, keys)
  - `Category`: Sensitivity category
  
- **`[Redaction]`** — Controls how data is displayed in different contexts
  - `ForContext(RedactionContext)`: Returns action per context (ApiResponse, Logs, Audit, Export)
  - Possible values: `None`, `Redact`, `Mask`, `Omit`, `Pseudonymize`

### Enums
- **`DataCategory`** — Personal data categories (Contact, Identification, Financial, Biometric, Location, Health, Preference)
- **`SensitiveDataCategory`** — Sensitive data categories (Credential, Authentication, Financial, Encryption, Token)
- **`OutputRedactionAction`** — Redaction actions (None, Redact, Mask, Omit, Pseudonymize)
- **`RedactionContext`** — Contexts where redaction applies (ApiResponse, Logs, Audit, Export)
- **`AuditOperation`** — Types of audited operations (Access, Create, Update, Delete, Export)

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

### Semantics
- `None`: Include full value
- `Redact`: Replace with `[REDACTED]`
- `Mask`: Show partially (e.g., a****@x.com for email)
- `Omit`: Exclude completely (doesn't appear in JSON, logs, exports)
- `Pseudonymize`: Replace with pseudonym token

**IMPORTANT**: In audit records, `Omit` affects only data output (`Details`), never omits the field from the record itself.

## Dependencies
- None (only .NET)

## Possible Improvements

1. **Compile-time DataSubjectId validation** — Currently validated at runtime. Analyzer SF0003 already warns.
2. **Composite DataSubjectId support** — Currently expects single string property
3. **User/role-based redaction** — Currently static per property
