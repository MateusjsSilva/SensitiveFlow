using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Policies;

namespace SensitiveFlow.Core.Profiles;

/// <summary>
/// Shared SensitiveFlow options used by modules that want central policy/profile decisions.
/// </summary>
public sealed class SensitiveFlowOptions
{
    /// <summary>
    /// Gets the active profile. The default is <see cref="SensitiveFlowDefaults.Profile"/>
    /// (<see cref="SensitiveFlowProfile.Balanced"/>).
    /// </summary>
    public SensitiveFlowProfile Profile { get; private set; } = SensitiveFlowDefaults.Profile;

    /// <summary>Gets the policy registry.</summary>
    public SensitiveFlowPolicyRegistry Policies { get; } = new();

    /// <summary>Applies one of the built-in profiles.</summary>
    public SensitiveFlowOptions UseProfile(SensitiveFlowProfile profile)
    {
        Profile = profile;

        switch (profile)
        {
            case SensitiveFlowProfile.Development:
                Policies.ForCategory(DataCategory.Contact).MaskInLogs().RedactInJson();
                Policies.ForCategory(DataCategory.Identification).MaskInLogs();
                break;
            case SensitiveFlowProfile.Balanced:
                Policies.ForCategory(DataCategory.Contact).MaskInLogs().RedactInJson().AuditOnChange();
                Policies.ForCategory(DataCategory.Financial).MaskInLogs().OmitInJson().RequireAudit();
                Policies.ForSensitiveCategory(SensitiveDataCategory.Other).OmitInJson().RequireAudit();
                break;
            case SensitiveFlowProfile.Strict:
                Policies.ForCategory(DataCategory.Contact).MaskInLogs().OmitInJson().RequireAudit();
                Policies.ForCategory(DataCategory.Identification).MaskInLogs().RedactInJson().RequireAudit();
                Policies.ForCategory(DataCategory.Financial).MaskInLogs().OmitInJson().RequireAudit();
                foreach (var category in Enum.GetValues<SensitiveDataCategory>())
                {
                    Policies.ForSensitiveCategory(category).OmitInJson().RequireAudit();
                }
                break;
            case SensitiveFlowProfile.AuditOnly:
                Policies.ForCategory(DataCategory.Other).AuditOnChange();
                Policies.ForSensitiveCategory(SensitiveDataCategory.Other).RequireAudit();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown SensitiveFlow profile.");
        }

        return this;
    }
}
