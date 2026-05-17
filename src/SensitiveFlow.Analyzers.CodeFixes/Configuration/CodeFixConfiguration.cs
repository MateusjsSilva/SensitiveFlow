using System.Collections.Generic;
using System.Linq;

namespace SensitiveFlow.Analyzers.CodeFixes.Configuration;

/// <summary>
/// Configuration for code fix providers.
/// </summary>
public sealed class CodeFixConfiguration
{
    /// <summary>
    /// Gets or sets the list of recognized masking method names.
    /// </summary>
    public List<string> RecognizedMaskingMethods { get; } = new()
    {
        "MaskEmail",
        "MaskPhone",
        "MaskName",
        "MaskSsn",
        "MaskCreditCard",
        "MaskIpAddress",
        "Redact",
        "RedactValue",
        "Anonymize",
        "Pseudonymize",
        "Hash"
    };

    /// <summary>
    /// Gets or sets custom masking patterns (property name → method mapping).
    /// </summary>
    public Dictionary<string, string> CustomMaskingPatterns { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets whether to use semantic analysis for better mask suggestions.
    /// </summary>
    public bool EnableSemanticAnalysis { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable batch fixes (fix-all in file/solution).
    /// </summary>
    public bool EnableBatchFixes { get; set; } = true;

    /// <summary>
    /// Registers a custom masking pattern.
    /// </summary>
    public void AddCustomPattern(string propertyNamePattern, string maskingMethod)
    {
        if (propertyNamePattern == null) throw new ArgumentNullException(nameof(propertyNamePattern));
        if (maskingMethod == null) throw new ArgumentNullException(nameof(maskingMethod));

        CustomMaskingPatterns[propertyNamePattern] = maskingMethod;
    }

    /// <summary>
    /// Gets the masking method for a property name.
    /// </summary>
    public string? GetMaskingMethodForProperty(string propertyName)
    {
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

        if (CustomMaskingPatterns.TryGetValue(propertyName, out var method))
        {
            return method;
        }

        var matchingPattern = CustomMaskingPatterns.Keys.FirstOrDefault(pattern =>
            propertyName.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        if (matchingPattern != null)
        {
            return CustomMaskingPatterns[matchingPattern];
        }

        return null;
    }

    /// <summary>
    /// Checks if a method name is a recognized masking method.
    /// </summary>
    public bool IsRecognizedMaskingMethod(string methodName)
    {
        return RecognizedMaskingMethods.Contains(methodName);
    }

    /// <summary>
    /// Clears all custom patterns.
    /// </summary>
    public void ClearCustomPatterns()
    {
        CustomMaskingPatterns.Clear();
    }

    /// <summary>
    /// Resets to default configuration.
    /// </summary>
    public void ResetToDefaults()
    {
        ClearCustomPatterns();
        EnableSemanticAnalysis = true;
        EnableBatchFixes = true;
    }

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static CodeFixConfiguration CreateDefault() => new();

    /// <summary>
    /// Creates a configuration from a dictionary of custom patterns.
    /// </summary>
    public static CodeFixConfiguration CreateWithPatterns(Dictionary<string, string> patterns)
    {
        if (patterns == null) throw new ArgumentNullException(nameof(patterns));

        var config = new CodeFixConfiguration();
        foreach (var kvp in patterns)
        {
            config.AddCustomPattern(kvp.Key, kvp.Value);
        }

        return config;
    }
}
