# Attributes

SensitiveFlow uses attributes to annotate model properties with privacy metadata. The attributes are defined in `SensitiveFlow.Core` and are the declarative foundation of the library.

## PersonalDataAttribute

Marks a property as personal data. The EF Core interceptor emits an audit record whenever this field is created, updated, or deleted.

```csharp
[PersonalData(Category = DataCategory.Contact)]
public string Email { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Category` | `DataCategory` | `Other` | Category of personal data |

### DataCategory values

| Value | Description |
|-------|-------------|
| `Identification` | Name, CPF, RG, passport |
| `Contact` | Email, phone, address |
| `Financial` | Bank account, income |
| `Behavioral` | Browsing history, preferences |
| `Location` | GPS, IP (raw), address |
| `Professional` | Employer, job title |
| `Other` | Anything not covered above |

## SensitiveDataAttribute

Marks a property as sensitive personal data — a category that typically requires stricter handling than regular personal data (e.g. health, biometric, financial credentials). The distinction from `PersonalData` is semantic: it lets your code, audit trail, and analyzers treat these fields with extra care.

```csharp
[SensitiveData(Category = SensitiveDataCategory.Health)]
public string DiagnosisCode { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Category` | `SensitiveDataCategory` | `Other` | Category of sensitive data |

### SensitiveDataCategory values

| Value | Description |
|-------|-------------|
| `Health` | Medical records, diagnoses |
| `Biometric` | Fingerprints, facial recognition |
| `Genetic` | DNA, genome data |
| `Ethnicity` | Racial or ethnic origin |
| `PoliticalOpinion` | Political views |
| `ReligiousBelief` | Religious or philosophical beliefs |
| `SexualOrientation` | Sexual life or orientation |
| `Financial` | Payment cards, bank credentials |
| `Criminal` | Criminal records or proceedings |
| `Other` | Any other sensitive category |

## RetentionDataAttribute

Declares the retention period for a field. The `RetentionEvaluator` uses this attribute to determine when a field has expired.

```csharp
[RetentionData(Years = 5, Months = 0, Policy = RetentionPolicy.AnonymizeOnExpiration)]
public string ContractData { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Years` | `int` | `0` | Retention period in years |
| `Months` | `int` | `0` | Additional months (added to Years) |
| `Policy` | `RetentionPolicy` | `AnonymizeOnExpiration` | Action on expiration |

### RetentionPolicy values

| Value | Description |
|-------|-------------|
| `AnonymizeOnExpiration` | Replace the value with an anonymized placeholder |
| `DeleteOnExpiration` | Remove the record or nullify the field |
| `BlockOnExpiration` | Block further processing of the field |

### Calendar-accurate arithmetic

`GetExpirationDate(DateTimeOffset from)` uses `AddYears` and `AddMonths`, not `TimeSpan`, to avoid drift on leap years and variable-length months.

```csharp
var attr = new RetentionDataAttribute { Years = 1 };
var expiry = attr.GetExpirationDate(new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero));
// expiry = 2025-02-28 (not 2025-03-01)
```

## Combining attributes

Multiple attributes can be applied to a single property:

```csharp
[PersonalData(Category = DataCategory.Contact)]
[RetentionData(Years = 3, Policy = RetentionPolicy.DeleteOnExpiration)]
public string Email { get; set; }
```

`[SensitiveData]` implies stronger classification; prefer it over `[PersonalData]` for health, biometric, or financial credential data.

## Inheritance and interfaces

Annotations are picked up from:

- The property declaration on the class itself.
- Properties on **base classes** — a derived type inherits the annotations of its base.
- Properties on **implemented interfaces** — annotating an interface property is enough; you do not need to repeat the attribute on the implementation.

```csharp
public interface IHasContact
{
    [PersonalData(Category = DataCategory.Contact)]
    string Email { get; }
}

public sealed class Customer : IHasContact
{
    public string Email { get; set; } = string.Empty;
    // No need to repeat [PersonalData] here — it is inherited from IHasContact.
}
```

Both the source generator and the runtime reflection fallback merge attributes from all of these sources, so behavior is consistent regardless of how a type is declared.
