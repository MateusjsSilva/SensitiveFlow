# SensitiveFlow.Tool

Command-line tool for analyzing projects and validating sensitive data protection setup.

## Commands

### Analyze Project
```bash
sensitiveflow analyze --project ./MyProject.csproj
```

**Checks**:
- All entities with `[PersonalData]` have `DataSubjectId`
- All sensitive properties have redaction rules
- Analyzer rules enabled in `.editorconfig`
- Packages properly installed

### Validate Configuration
```bash
sensitiveflow validate
```

**Validates**:
- DI registration is complete
- Interceptors registered
- Audit store configured
- Retention policies present

### List Sensitive Types
```bash
sensitiveflow list-types --project ./MyProject.csproj
```

**Output**:
```
Customer
  - DataSubjectId (key)
  - Email [PersonalData] → Mask (ApiResponse)
  - Phone [PersonalData] → Omit (ApiResponse)
  - InternalNotes [SensitiveData] → Redact (Logs)

Order
  - DataSubjectId (key)
  - ShippingAddress [PersonalData] → Mask (ApiResponse)
```

### Generate Report
```bash
sensitiveflow report --output compliance.html
```

**Generates**:
- Coverage report (what's protected)
- Risk assessment (unprotected fields)
- Configuration summary
- Audit trail status

## Installation

```bash
dotnet tool install --global SensitiveFlow.Tool

# Or local tool
dotnet tool install SensitiveFlow.Tool
```

## Usage Examples

### Check Project Health
```bash
sensitiveflow analyze --project src/MyApp.csproj --verbose
```

### Generate Compliance Report
```bash
sensitiveflow report \
  --project src/MyApp.csproj \
  --output compliance-$(date +%Y-%m-%d).html \
  --include-recommendations
```

### List All Sensitive Data
```bash
sensitiveflow list-types --all
```

## Possible Improvements

1. **Interactive wizard** — Setup new projects
2. **CI/CD integration** — Exit codes for pipeline gates
3. **Configuration scaffolding** — Generate boilerplate
4. **Diff reports** — Compare before/after
5. **Performance analysis** — Benchmark redaction overhead
