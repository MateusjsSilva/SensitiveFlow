namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Declares multiple properties that together form the composite data subject identifier.
/// Use when an entity is identified by multiple properties instead of a single DataSubjectId.
/// </summary>
/// <example>
/// <code>
/// [CompositeDataSubjectId("CustomerId", "OrderId")]
/// public class OrderLineItem
/// {
///     public string CustomerId { get; set; }
///     public string OrderId { get; set; }
///     public int LineItemId { get; set; }
///
///     [PersonalData]
///     public string ItemDescription { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CompositeDataSubjectIdAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance with property names that form the composite identifier.
    /// </summary>
    /// <param name="propertyNames">One or more property names that together identify the data subject. Order matters for audit trail consistency.</param>
    /// <exception cref="ArgumentNullException">Thrown when propertyNames is null.</exception>
    /// <exception cref="ArgumentException">Thrown when propertyNames is empty.</exception>
    public CompositeDataSubjectIdAttribute(params string[] propertyNames)
    {
        if (propertyNames == null)
        {
            throw new ArgumentNullException(nameof(propertyNames));
        }

        if (propertyNames.Length == 0)
        {
            throw new ArgumentException("At least one property name must be specified.", nameof(propertyNames));
        }

        PropertyNames = propertyNames;
    }

    /// <summary>
    /// Gets the property names that form the composite data subject identifier, in order.
    /// </summary>
    public string[] PropertyNames { get; }
}
