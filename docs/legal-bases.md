# Legal Bases (Art. 7 and Art. 11)

The `LGPD.NET.LegalBasis` module manages the legal bases that authorize data processing under the LGPD.

> **Why separate from Consent?** Consent is only one of the 10 legal bases. A system can process data based on legal obligation, contract performance, or legitimate interest — without consent — and still be LGPD compliant. Mixing the two is the most common compliance mistake.

## Legal Bases for Processing (Art. 7)

| Enum Value | Article | Description |
|------------|---------|-------------|
| `Consent` | Art. 7, I | Data subject's free, informed, and unambiguous consent |
| `LegalObligation` | Art. 7, II | Legal or regulatory obligation |
| `PublicPolicy` | Art. 7, III | Public policy by the public administration |
| `Research` | Art. 7, IV | Research studies, anonymized when possible |
| `ContractPerformance` | Art. 7, V | Contract execution or preliminary procedures |
| `ExerciseOfRights` | Art. 7, VI | Exercise of rights in judicial, administrative, or arbitration proceedings |
| `ProtectionOfLife` | Art. 7, VII | Protection of life or physical safety |
| `HealthProtection` | Art. 7, VIII | Health protection, by health professionals |
| `LegitimateInterest` | Art. 7, IX | Legitimate interest of the controller or third party |
| `CreditProtection` | Art. 7, X | Credit protection |

## Legal Bases for Sensitive Data (Art. 11)

| Enum Value | Article | Description |
|------------|---------|-------------|
| `ExplicitConsent` | Art. 11, I | Specific and prominent consent |
| `LegalObligation` | Art. 11, II, a | Legal or regulatory obligation |
| `PublicPolicy` | Art. 11, II, b | Public policy |
| `Research` | Art. 11, II, c | Research studies |
| `ExerciseOfRights` | Art. 11, II, d | Exercise of rights |
| `ProtectionOfLife` | Art. 11, II, e | Protection of life |
| `HealthProtection` | Art. 11, II, f | Health protection |
| `FraudPrevention` | Art. 11, II, g | Fraud prevention |

## Legitimate Interest Balancing Test (Art. 10)

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
