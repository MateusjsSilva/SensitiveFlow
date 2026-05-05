# Getting Started with SensitiveFlow

SensitiveFlow is a modular, MIT-licensed library that helps .NET developers implement LGPD (Brazilian General Data Protection Law - Law 13.709/2018) compliance in a declarative, testable way.

## Installation

```bash
dotnet add package SensitiveFlow.Core
```

## Quick Start

### 1. Annotate your model

```csharp
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public class Customer
{
    public Guid Id { get; set; }

    [PersonalData(Category = DataCategory.Identification,
                  LegalBasis = LegalBasis.ContractPerformance)]
    public string Name { get; set; } = string.Empty;

    [SensitiveData(Category = DataCategory.Financial,
                   SensitiveLegalBasis = SensitiveLegalBasis.ExplicitConsent)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    [InternationalTransfer(Country = TransferCountry.UnitedStates,
                           Mechanism = SafeguardMechanism.ContractualClauses)]
    public string Email { get; set; } = string.Empty;
}
```

## Next Steps

- Read the [Attributes](attributes.md) documentation
- Learn about [Legal Bases](legal-bases.md)
- Anonymize and pseudonymize data with [SensitiveFlow.Anonymization](anonymization.md)
