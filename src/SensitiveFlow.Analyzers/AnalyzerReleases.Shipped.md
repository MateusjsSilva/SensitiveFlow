## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
SF0001 | Privacy | Warning | Sensitive member logged directly without sanitization.
SF0002 | Privacy | Warning | Sensitive member returned directly in HTTP response.
SF0003 | Privacy | Error | Entity with sensitive data missing DataSubjectId/UserId. Compile-time validation enforces DataSubjectId requirement.
SF0004 | Privacy | Info    | Property name suggests PII but is not annotated.
