using System;
using System.Collections.Generic;

namespace SensitiveFlow.SourceGenerators.Incremental;

/// <summary>
/// Tracks generated types and their metadata for incremental generation.
/// </summary>
public sealed class IncrementalGenerationTracker
{
    private readonly Dictionary<string, GeneratedTypeInfo> _generatedTypes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _modifiedTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets all tracked generated types.
    /// </summary>
    public IReadOnlyDictionary<string, GeneratedTypeInfo> GeneratedTypes => _generatedTypes;

    /// <summary>
    /// Gets types that have been modified since last generation.
    /// </summary>
    public IEnumerable<string> ModifiedTypes => _modifiedTypes;

    /// <summary>
    /// Registers a generated type.
    /// </summary>
    public void RegisterGeneratedType(string fullyQualifiedName, GeneratedTypeInfo info)
    {
        if (fullyQualifiedName == null) throw new ArgumentNullException(nameof(fullyQualifiedName));
        if (info == null) throw new ArgumentNullException(nameof(info));

        _generatedTypes[fullyQualifiedName] = info;
        _modifiedTypes.Remove(fullyQualifiedName);
    }

    /// <summary>
    /// Marks a type as modified.
    /// </summary>
    public void MarkAsModified(string fullyQualifiedName)
    {
        if (fullyQualifiedName == null) throw new ArgumentNullException(nameof(fullyQualifiedName));
        _modifiedTypes.Add(fullyQualifiedName);
    }

    /// <summary>
    /// Checks if a type needs regeneration.
    /// </summary>
    public bool NeedsRegeneration(string fullyQualifiedName)
    {
        return _modifiedTypes.Contains(fullyQualifiedName) ||
               !_generatedTypes.ContainsKey(fullyQualifiedName);
    }

    /// <summary>
    /// Clears modification tracking for next build cycle.
    /// </summary>
    public void ClearModifications()
    {
        _modifiedTypes.Clear();
    }

    /// <summary>
    /// Resets all tracking data.
    /// </summary>
    public void Reset()
    {
        _generatedTypes.Clear();
        _modifiedTypes.Clear();
    }
}

/// <summary>
/// Information about a generated type.
/// </summary>
public sealed class GeneratedTypeInfo
{
    /// <summary>Gets the fully qualified type name.</summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>Gets the number of sensitive properties.</summary>
    public int SensitivePropertyCount { get; set; }

    /// <summary>Gets the timestamp of generation.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
