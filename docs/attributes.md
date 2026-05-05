# Attributes

SensitiveFlow provides attributes to annotate your model classes with LGPD metadata. These attributes are the declarative foundation of the library.

## PersonalDataAttribute

Marks a property or field as personal data under Art. 5, I of the LGPD.

```csharp
[PersonalData(Category = DataCategory.Identification,
              LegalBasis = LegalBasis.Consent,
              Purpose = ProcessingPurpose.ServiceProvision)]
public string Name { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Category` | `DataCategory` | `Other` | Category of personal data |
| `LegalBasis` | `LegalBasis` | `Consent` | Legal basis that authorizes processing |
| `Purpose` | `ProcessingPurpose` | `ServiceProvision` | Purpose for which data is processed |

## SensitiveDataAttribute

Marks a property or field as sensitive personal data under Art. 5, II and Art. 11 of the LGPD. Implies additional obligations and restricted legal bases.

```csharp
[SensitiveData(Category = DataCategory.Financial,
               SensitiveLegalBasis = SensitiveLegalBasis.ExplicitConsent,
               Purpose = ProcessingPurpose.ServiceProvision)]
public string TaxId { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Category` | `DataCategory` | `Other` | Category of sensitive data |
| `SensitiveLegalBasis` | `SensitiveLegalBasis` | `ExplicitConsent` | Legal basis for processing sensitive data |
| `Purpose` | `ProcessingPurpose` | `ServiceProvision` | Purpose for which data is processed |

## EraseDataAttribute

Marks a property for automatic deletion when the data subject exercises the right to erasure (Art. 18, IV).

```csharp
[EraseData(AnonymizeInsteadOfDelete = true)]
public string TemporaryData { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AnonymizeInsteadOfDelete` | `bool` | `false` | When true, anonymizes instead of deleting |

## RetentionDataAttribute

Defines the retention period and the action on expiration under Art. 15 and 16 of the LGPD.

```csharp
[RetentionData(Years = 5, Months = 0, Policy = RetentionPolicy.AnonymizeOnExpiration)]
public string ContractData { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Years` | `int` | `0` | Retention period in years |
| `Months` | `int` | `0` | Retention period in months (added to Years) |
| `Policy` | `RetentionPolicy` | `AnonymizeOnExpiration` | Action executed when the period expires |
| `Period` | `TimeSpan` | *(computed)* | Approximate retention period (Years*365 + Months*30 days) |

## InternationalTransferAttribute

Marks a property whose data can be transferred internationally under Art. 33-36 of the LGPD. Supports multiple transfers per field.

```csharp
[InternationalTransfer(Country = TransferCountry.UnitedStates,
                       Mechanism = SafeguardMechanism.ContractualClauses,
                       Recipient = "Cloud Service Provider")]
public string Email { get; set; }
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Country` | `TransferCountry` | `Other` | Destination country |
| `Mechanism` | `SafeguardMechanism` | `ContractualClauses` | Safeguard mechanism |
| `Recipient` | `string?` | `null` | Recipient name (company or service) |
