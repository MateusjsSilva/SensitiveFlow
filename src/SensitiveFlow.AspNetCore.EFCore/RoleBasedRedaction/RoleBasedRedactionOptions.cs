using SensitiveFlow.Json.Enums;

namespace SensitiveFlow.AspNetCore.EFCore.RoleBasedRedaction;

/// <summary>
/// Configures redaction behavior per user role.
/// </summary>
public sealed class RoleBasedRedactionOptions
{
    /// <summary>
    /// Gets or sets the default redaction mode for unauthenticated users.
    /// </summary>
    public JsonRedactionMode DefaultMode { get; set; } = JsonRedactionMode.Mask;

    /// <summary>
    /// Gets or sets role-specific redaction overrides.
    /// </summary>
    public Dictionary<string, JsonRedactionMode> RoleOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a role with a specific redaction mode.
    /// </summary>
    public void ConfigureRole(string roleName, JsonRedactionMode mode)
    {
        ArgumentNullException.ThrowIfNull(roleName);
        RoleOverrides[roleName] = mode;
    }

    /// <summary>
    /// Gets the redaction mode for a user with the given roles.
    /// First matching role takes precedence; if no role matches, returns <see cref="DefaultMode"/>.
    /// </summary>
    public JsonRedactionMode GetModeForRoles(IEnumerable<string>? userRoles)
    {
        if (userRoles is null)
        {
            return DefaultMode;
        }

        foreach (var role in userRoles)
        {
            if (!string.IsNullOrEmpty(role) && RoleOverrides.TryGetValue(role, out var mode))
            {
                return mode;
            }
        }

        return DefaultMode;
    }
}
