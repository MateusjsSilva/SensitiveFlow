using System.Reflection;

namespace SensitiveFlow.Anonymization.Erasure;

/// <summary>
/// Decides what value to write into an annotated property during an erasure pass.
/// </summary>
public interface IErasureStrategy
{
    /// <summary>
    /// Returns the replacement value for <paramref name="property"/> on <paramref name="entity"/>.
    /// Implementations may return <see langword="null"/> to clear the property (when nullable),
    /// a fixed marker (e.g. <c>"[ERASED]"</c>), or a hashed token.
    /// </summary>
    object? GetErasureValue(object entity, PropertyInfo property);
}
