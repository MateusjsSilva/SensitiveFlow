using System.Security.Claims;
using SensitiveFlow.Core;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Json.Roles;

/// <summary>
/// Resolves <see cref="RedactionContext"/> based on <see cref="ClaimsPrincipal"/> roles.
/// </summary>
/// <remarks>
/// Maps role claims to role-specific <see cref="RedactionContext"/> values:
/// - "Admin" role → <see cref="RedactionContext.AdminView"/>
/// - "Support" role → <see cref="RedactionContext.SupportView"/>
/// - "Customer" role → <see cref="RedactionContext.CustomerView"/>
/// - No matching role → <see cref="RedactionContext.ApiResponse"/>
/// </remarks>
public class ClaimsPrincipalRedactionContextResolver : IRedactionContextResolver
{
    private readonly ClaimsPrincipal _principal;

    /// <summary>
    /// Creates a new resolver for the given principal.
    /// </summary>
    /// <param name="principal">The claims principal containing role information.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
    public ClaimsPrincipalRedactionContextResolver(ClaimsPrincipal principal)
    {
        _principal = principal ?? throw new ArgumentNullException(nameof(principal));
    }

    /// <summary>
    /// Resolves context based on the principal's roles.
    /// </summary>
    /// <returns>
    /// The first matching role context, or <see cref="RedactionContext.ApiResponse"/> if no matching role.
    /// </returns>
    public RedactionContext ResolveContext()
    {
        if (_principal.IsInRole("Admin"))
        {
            return RedactionContext.AdminView;
        }

        if (_principal.IsInRole("Support"))
        {
            return RedactionContext.SupportView;
        }

        if (_principal.IsInRole("Customer"))
        {
            return RedactionContext.CustomerView;
        }

        return RedactionContext.ApiResponse;
    }
}
