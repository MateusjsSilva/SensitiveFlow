using SensitiveFlow.Core;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Json.Roles;

/// <summary>
/// Resolves which <see cref="RedactionContext"/> applies for JSON serialization based on current context.
/// </summary>
public interface IRedactionContextResolver
{
    /// <summary>
    /// Resolves the appropriate <see cref="RedactionContext"/> for the current operation.
    /// </summary>
    /// <returns>
    /// The <see cref="RedactionContext"/> to apply, or <see cref="RedactionContext.ApiResponse"/> if no specific context applies.
    /// </returns>
    RedactionContext ResolveContext();
}
