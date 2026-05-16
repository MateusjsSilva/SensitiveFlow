using System.Security.Claims;

namespace SensitiveFlow.AspNetCore.Claims;

/// <summary>
/// Options for customizing which claims are checked to extract the actor ID.
/// </summary>
public sealed class ActorIdClaimOptions
{
    /// <summary>
    /// Gets or sets an ordered list of claim type names to check for the actor ID.
    /// The first matching claim value is used as the actor ID.
    /// Default includes <c>"sub"</c> (JWT subject) and <see cref="ClaimTypes.NameIdentifier"/>.
    /// </summary>
    public IList<string> ClaimNames { get; set; } = new List<string>
    {
        "sub",
        ClaimTypes.NameIdentifier,
    };
}
