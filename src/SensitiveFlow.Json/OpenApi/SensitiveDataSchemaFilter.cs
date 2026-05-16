using System.Reflection;
using SensitiveFlow.Core;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Json.OpenApi;

/// <summary>
/// Provides metadata about sensitive properties for OpenAPI schema inspection.
/// </summary>
/// <remarks>
/// Use this to identify which properties would be redacted in a given context.
/// This is a standalone utility with no Swashbuckle dependency — integrate into your ISchemaFilter as needed.
/// </remarks>
public class SensitiveDataSchemaFilter
{
    /// <summary>
    /// Gets the names of properties that would be redacted in the given context.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="context">The redaction context to check against.</param>
    /// <returns>A collection of property names that have redaction rules for this context.</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
    public static IReadOnlyCollection<string> GetRedactedProperties(Type type, RedactionContext context = RedactionContext.ApiResponse)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var redacted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.IgnoreCase);

        foreach (var prop in properties)
        {
            if (IsPropertyRedacted(prop, context))
            {
                redacted.Add(prop.Name);
            }
        }

        return redacted;
    }

    /// <summary>
    /// Gets the names of properties marked with [PersonalData] or [SensitiveData].
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>A collection of property names marked as sensitive.</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
    public static IReadOnlyCollection<string> GetSensitiveProperties(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.IgnoreCase);

        foreach (var prop in properties)
        {
            var hasPersonal = prop.GetCustomAttribute<PersonalDataAttribute>() != null;
            var hasSensitive = prop.GetCustomAttribute<SensitiveDataAttribute>() != null;

            if (hasPersonal || hasSensitive)
            {
                sensitive.Add(prop.Name);
            }
        }

        return sensitive;
    }

    /// <summary>
    /// Gets properties that should be hidden from the schema entirely (marked with Omit action).
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>A collection of property names marked to be omitted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
    public static IReadOnlyCollection<string> GetOmittedProperties(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var omitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.IgnoreCase);

        foreach (var prop in properties)
        {
            var redaction = prop.GetCustomAttribute<RedactionAttribute>();
            if (redaction != null)
            {
                var mode = GetRedactionMode(prop, RedactionContext.ApiResponse);
                if (mode == OutputRedactionAction.Omit)
                {
                    omitted.Add(prop.Name);
                }
            }
        }

        return omitted;
    }

    /// <summary>
    /// Determines if a property is redacted in the given context.
    /// </summary>
    /// <param name="property">The property to check.</param>
    /// <param name="context">The redaction context.</param>
    /// <returns>True if the property has a redaction rule for this context; false otherwise.</returns>
    private static bool IsPropertyRedacted(PropertyInfo property, RedactionContext context)
    {
        var redaction = property.GetCustomAttribute<RedactionAttribute>();
        if (redaction == null)
        {
            return false;
        }

        var mode = GetRedactionMode(property, context);
        return mode != OutputRedactionAction.None;
    }

    /// <summary>
    /// Gets the redaction mode for a property in the given context.
    /// </summary>
    private static OutputRedactionAction GetRedactionMode(PropertyInfo property, RedactionContext context)
    {
        var redaction = property.GetCustomAttribute<RedactionAttribute>();
        if (redaction == null)
        {
            return OutputRedactionAction.None;
        }

        return context switch
        {
            RedactionContext.AdminView => redaction.AdminView,
            RedactionContext.SupportView => redaction.SupportView,
            RedactionContext.CustomerView => redaction.CustomerView,
            _ => redaction.ApiResponse
        };
    }
}
