# SensitiveFlow: Fixes Implementation Map

Mapa detalhado de arquivos que precisam ser modificados para resolver cada issue, com linha de código exata e o tipo de mudança.

---

## P0 Issues (Must Fix Before Release)

### Fix 1.1: DataSubjectId Type Ambiguity ✅ DONE

**Problem:** Accepts any type, converts to string unsafely. Multiple subjects can map to same ID.

**Status:** Complete — Runtime validation added, 100% test coverage achieved

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Core/Attributes/PersonalDataAttribute.cs` | NEW | Add attribute | Create `[DataSubjectIdRequired]` analyzer attribute |
| `src/SensitiveFlow.EFCore/Interceptors/SensitiveDataAuditInterceptor.cs` | 290-318 | Validation logic | Add type check: only string/Guid allowed for DataSubjectId |
| `src/SensitiveFlow.EFCore/BulkOperations/SensitiveBulkOperationsExtensions.cs` | 242-261 | Validation logic | Same type check in BuildSubjectSelector |
| `src/SensitiveFlow.Analyzers/Analyzers/CrossBoundarySensitiveDataAnalyzer.cs` | NEW rule | Diagnostic rule | SF0005: Warn if DataSubjectId is not string/Guid |
| `src/SensitiveFlow.Analyzers/AnalyzerReleases.Unshipped.md` | NEW | Documentation | Document SF0005 rule |

**Test Files:**
- `tests/SensitiveFlow.EFCore.Tests/SensitiveBulkOperationsTests.cs` — Add test for non-string DataSubjectId (should fail)
- `tests/SensitiveFlow.Analyzers.Tests/CrossBoundarySensitiveDataAnalyzerTests.cs` — Add test for SF0005

**Migration:** 
- Analyzer warns, doesn't block
- Existing code can wrap int ID: `public string DataSubjectId => UserId.ToString("X");`

---

### Fix 1.2: AuditRecord.Details Unstructured ✅ DONE

**Problem:** String field prevents typed queries. No structure for before/after values.

**Status:** Complete — AuditRecordDetails sealed record created with 36 comprehensive tests covering all parse paths, edge cases, and round-trip serialization

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Core/Models/AuditRecordDetails.cs` | NEW | New model | Create sealed record with OldValue, NewValue, BulkOperationTag, ReasonCode |
| `src/SensitiveFlow.Core/Models/AuditRecord.cs` | 57-58 | Add helper | Add `ParseDetails() -> AuditRecordDetails?` method |
| `src/SensitiveFlow.Audit.EFCore/Entities/AuditRecordEntity.cs` | 39-40 | No change | Details column stays string (backward compatible) |
| `src/SensitiveFlow.Audit.EFCore/Configuration/AuditRecordEntityTypeConfiguration.cs` | NEW | Migration | Add max length constraint and indexing for Details |
| `src/SensitiveFlow.EFCore/Interceptors/SensitiveDataAuditInterceptor.cs` | 184-202 | Update logic | Change BuildAuditDetails to return JSON-serialized AuditRecordDetails |

**Migration Script:**
```sql
ALTER TABLE AuditRecords ALTER COLUMN Details NVARCHAR(MAX) NULL;
CREATE INDEX IX_AuditRecords_DetailsPrefix ON AuditRecords (Entity, Field, Timestamp DESC);
```

**Test Files:**
- `tests/SensitiveFlow.Core.Tests/Models/AuditRecordDetailsTests.cs` — NEW
- `tests/SensitiveFlow.Audit.EFCore.Tests/` — Add round-trip tests

---

### Fix 1.5: RedactionAttribute Insecure Defaults ✅ DONE

**Problem:** [PersonalData] without [Redaction] leaks full value in API/Logs/Audit.

**Status:** Complete — SF0006 analyzer rule created with 13 comprehensive tests covering happy path, edge cases, multiple properties, and complex scenarios. All tests passing across net8.0, net9.0, net10.0.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Core/Attributes/RedactionAttribute.cs` | 10-21 | Documentation | Add XMLDoc: "None = full value, consider using Mask or Redact" |
| `src/SensitiveFlow.Analyzers/Analyzers/CrossBoundarySensitiveDataAnalyzer.cs` | NEW rule | Diagnostic rule | SF0006: ERROR if [PersonalData] without [Redaction(ApiResponse/Logs/Audit)] |
| `src/SensitiveFlow.Analyzers.CodeFixes/` | NEW | Code fix | Suggest: `[Redaction(ApiResponse=Mask, Logs=Redact, Audit=Mask, Export=None)]` |
| `src/SensitiveFlow.Analyzers/AnalyzerReleases.Unshipped.md` | NEW | Documentation | Document SF0006 rule |

**No database/runtime changes needed** — analyzer is sufficient.

**Test Files:**
- `tests/SensitiveFlow.Analyzers.Tests/CrossBoundarySensitiveDataAnalyzerTests.cs` — Add SF0006 test cases

---

## P1 Issues (High Priority)

### Fix 1.3: IAuditStore.QueryAsync Primitive

**Problem:** No Entity/Operation/Actor filters. Devs fetch all records and filter in memory.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Core/Interfaces/IAuditQuery.cs` | NEW | New interface | Create query builder with ByEntity, ByOperation, ByActor, WithPagination, OrderBy |
| `src/SensitiveFlow.Core/Models/AuditQuery.cs` | NEW | New model | Implement builder as sealed class |
| `src/SensitiveFlow.Core/Interfaces/IAuditStore.cs` | 15-44 | Add overload | Add `QueryAsync(IAuditQuery query)` method (keep old signature) |
| `src/SensitiveFlow.Audit.EFCore/Stores/EfCoreAuditStore.cs` | 82-116 | Implementation | Implement new QueryAsync overload using LINQ |
| `src/SensitiveFlow.Audit/Stores/InMemoryAuditStore.cs` | - | Implementation | Implement new QueryAsync overload for in-memory store |
| `tests/SensitiveFlow.Audit.Tests/` | NEW | Tests | Add comprehensive query builder tests |

**Backward Compatibility:** Keep old `QueryAsync(from, to, skip, take)` signature.

---

### Fix 1.4: RetentionEvaluator Fail-Fast

**Problem:** Throws on first expired field. Doesn't report all expired fields in one pass.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Core/Exceptions/RetentionException.cs` | NEW | New exception | Create with list of expired fields |
| `src/SensitiveFlow.Retention/Services/RetentionEvaluator.cs` | 78-115 | Logic rewrite | Collect all expired fields, then throw if no handlers |
| `src/SensitiveFlow.Retention/Contracts/IRetentionExpirationHandler.cs` | - | No change | Existing interface unchanged |

**Test Files:**
- `tests/SensitiveFlow.Retention.Tests/RetentionEvaluatorTests.cs` — Add test for multiple expired fields

---

### Fix 3.3: AuditRecordEntity No Uniqueness Index

**Problem:** No UNIQUE constraint on RecordId. Duplicate records on retry.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Audit.EFCore/Configuration/AuditRecordEntityTypeConfiguration.cs` | - | Add fluent config | `.HasIndex(a => a.RecordId).IsUnique();` |
| `src/SensitiveFlow.Audit.EFCore/Migrations/` | NEW | New migration | Create migration to add UNIQUE constraint |

**Migration Script:**
```sql
CREATE UNIQUE NONCLUSTERED INDEX UX_AuditRecords_RecordId 
ON AuditRecords(RecordId) 
WHERE RecordId IS NOT NULL;
```

---

## P2 Issues (Medium Priority)

### Fix 2.1: SensitiveMemberCache Doesn't Cache Redaction Attributes

**Problem:** [Redaction] attributes rescanned per operation. Performance degradation in high-throughput scenarios.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Core/Reflection/SensitiveMemberInfo.cs` | NEW | New model | Sealed record pairing PropertyInfo + RedactionAttribute + RetentionDataAttribute |
| `src/SensitiveFlow.Core/Reflection/SensitiveMemberCache.cs` | 40-85 | Refactor cache | Change GetSensitiveProperties to GetSensitiveMembers, return SensitiveMemberInfo[] |
| `src/SensitiveFlow.EFCore/Interceptors/SensitiveDataAuditInterceptor.cs` | 155-221 | Update usage | Use cached SensitiveMemberInfo instead of rescanning |
| `src/SensitiveFlow.Anonymization/Export/DataSubjectExporter.cs` | 54-69 | Update usage | Use cached info |
| `src/SensitiveFlow.Logging/Loggers/RedactingLogger.cs` | - | Update usage | Use cached info (if needed) |
| `src/SensitiveFlow.Json/Newtonsoft/SensitiveDataNewtonsoftConverter.cs` | - | Update usage | Use cached info |

**Backward Compatibility:** Deprecate `GetSensitiveProperties`, add `GetSensitiveMembers` alongside.

---

### Fix 2.2: MaxAuditedRows Arbitrary Default

**Problem:** 10,000 is magic number. No guidance for dev. Surprise failures in production.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.EFCore/BulkOperations/SensitiveBulkOperationsOptions.cs` | 32 | Add method | Static helper: `ComputeLimit(propertyCount, throughputTarget)` |
| `src/SensitiveFlow.EFCore/BulkOperations/SensitiveBulkOperationsExtensions.cs` | 219-223 | Better error | Include suggestion: "For your entity, try X = ..." |

---

### Fix 2.3: JsonRedactionOptions Default Ambiguous

**Problem:** Non-string [PersonalData] fields get masked as "****", no type info in JSON.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Json/Configuration/JsonRedactionOptions.cs` | - | Add mode | Add `IncludeRedactionMetadata` boolean (default: true) |
| `src/SensitiveFlow.Json/Converters/SensitiveJsonModifier.cs` | - | Update logic | Wrap redacted non-strings with `{ "_redacted": true, "type": "int", "value": null }` |
| `src/SensitiveFlow.Json/Newtonsoft/SensitiveDataNewtonsoftConverter.cs` | 100-150 | Update logic | Same wrapping for Newtonsoft |

---

### Fix 3.1: Retention Doesn't Handle Collections

**Problem:** `List<Address>` with [RetentionData] fields never evaluated.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Retention/Services/RetentionEvaluator.cs` | 118-128 | Extend logic | Handle IEnumerable in EvaluateRecursiveAsync |
| `src/SensitiveFlow.Retention/Services/RetentionExecutor.cs` | - | Update | Ensure executor calls evaluator on nested collections |

**Test Files:**
- `tests/SensitiveFlow.Retention.Tests/RetentionEvaluatorTests.cs` — Add test with nested collections

---

### Fix 3.2: RedactingLogger Manual Prefix

**Problem:** No auto-detection of [PersonalData]. Dev must add {[Sensitive]Email} manually.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Logging/Loggers/RedactingLogger.cs` | 89-100 | Add detection | Scan structured properties for [PersonalData], auto-prefix |
| `src/SensitiveFlow.Logging/Configuration/SensitiveLoggingOptions.cs` | - | Add option | `EnableAutoDetectionFromAttributes` (default: true) |

**Backward Compatibility:** Existing [Sensitive] prefix still works.

---

## P3 Issues (Polish)

### Fix 4.1: Masker Type Inference Weak

**Problem:** BillingEmail not recognized by property name inference.

**Files to Change:**

| File | Lines | Change Type | Action |
|------|-------|-------------|--------|
| `src/SensitiveFlow.Core/Attributes/MaskKindAttribute.cs` | NEW | New attribute | Allow explicit `[MaskKind(Email)]` |
| `src/SensitiveFlow.Anonymization/Export/DataSubjectExporter.cs` | 94-100 | Update inference | Check [MaskKind] attribute first, then fallback to name |

---

## Summary: Files by Count

| Package | Count | Priority |
|---------|-------|----------|
| `SensitiveFlow.Core` | 7 | P0-P2 |
| `SensitiveFlow.EFCore` | 4 | P0-P2 |
| `SensitiveFlow.Audit.EFCore` | 3 | P0-P1 |
| `SensitiveFlow.Analyzers` | 4 | P0 |
| `SensitiveFlow.Retention` | 2 | P1-P2 |
| `SensitiveFlow.Logging` | 2 | P2 |
| `SensitiveFlow.Json` | 3 | P2 |
| `SensitiveFlow.Anonymization` | 2 | P2-P3 |
| Test files | 8+ | All |
| Migrations | 2 | P0-P1 |

**Total:** ~40 files touched, ~300-400 lines changed/added.

