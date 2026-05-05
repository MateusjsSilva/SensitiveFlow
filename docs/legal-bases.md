# Legal Bases

The `SensitiveFlow.LegalBasis` module manages the legal bases that authorize data processing under your applicable privacy framework.

> **Why separate from Consent?** Consent is only one legal basis among several. A system can process data based on legal obligation, contract performance, or legitimate interest without consent, depending on context and jurisdiction.

## Legal Bases for Processing

| Enum Value | Typical Use | Description |
|------------|---------|-------------|
| `Consent` | Permission-based processing | Data subject's free, informed, and unambiguous consent |
| `LegalObligation` | Compliance duties | Legal or regulatory obligation |
| `PublicPolicy` | Public interest programs | Public policy by the public administration |
| `Research` | Scientific/statistical studies | Research studies, anonymized when possible |
| `ContractPerformance` | Service delivery | Contract execution or preliminary procedures |
| `ExerciseOfRights` | Legal defense and claims | Exercise of rights in judicial, administrative, or arbitration proceedings |
| `ProtectionOfLife` | Emergency scenarios | Protection of life or physical safety |
| `HealthProtection` | Care and safety contexts | Health protection, by health professionals |
| `LegitimateInterest` | Business continuity | Legitimate interest of the controller or third party |
| `CreditProtection` | Fraud and credit risk | Credit protection |

## Legal Bases for Sensitive Data

| Enum Value | Typical Use | Description |
|------------|---------|-------------|
| `ExplicitConsent` | High-sensitivity processing | Specific and prominent consent |
| `LegalObligation` | Compliance duties | Legal or regulatory obligation |
| `PublicPolicy` | Public interest programs | Public policy |
| `Research` | Scientific/statistical studies | Research studies |
| `ExerciseOfRights` | Legal defense and claims | Exercise of rights |
| `ProtectionOfLife` | Emergency scenarios | Protection of life |
| `HealthProtection` | Care and safety contexts | Health protection |
| `FraudPrevention` | Security controls | Fraud prevention |

## Legitimate Interest Balancing Test

When using `LegitimateInterest` as a legal basis, a balancing test must be documented:

```csharp
await legalBasisService.RegisterAsync(new LegalBasisRecord
{
    Entity = nameof(Customer),
    Field = nameof(Customer.Email),
    Basis = LegalBasis.LegitimateInterest,
    Justification = "Sending communications about the active contract",
    BalancingTest = "Legitimate interest does not override data subject rights because..."
});
```

