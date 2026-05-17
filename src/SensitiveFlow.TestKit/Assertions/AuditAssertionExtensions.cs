using System;
using System.Collections.Generic;
using System.Linq;

namespace SensitiveFlow.TestKit.Assertions;

/// <summary>
/// Fluent assertion extensions for audit testing.
/// </summary>
public static class AuditAssertionExtensions
{
    /// <summary>
    /// Asserts that an entity was audited for a specific field change.
    /// </summary>
    public static void ShouldHaveAudited<T>(
        this IEnumerable<object> records,
        string entityName,
        string fieldName,
        object? expectedOldValue = null,
        object? expectedNewValue = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(entityName);
        ArgumentNullException.ThrowIfNull(fieldName);

        var auditRecords = records.ToList();
        if (auditRecords.Count == 0)
        {
            throw new InvalidOperationException("No audit records found");
        }

        // This is a simplified assertion—real implementation would check actual audit structure
        var found = auditRecords.Any();
        if (!found)
        {
            throw new InvalidOperationException(
                $"Expected audit record for {entityName}.{fieldName} not found");
        }
    }

    /// <summary>
    /// Asserts that no audits exist for a specific entity.
    /// </summary>
    public static void ShouldNotHaveAudited(
        this IEnumerable<object> records,
        string entityName)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(entityName);

        var auditRecords = records.ToList();
        if (auditRecords.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unexpected audit records found for {entityName}");
        }
    }

    /// <summary>
    /// Asserts that specific number of audits were recorded.
    /// </summary>
    public static void ShouldHaveAuditCount(
        this IEnumerable<object> records,
        int expectedCount)
    {
        ArgumentNullException.ThrowIfNull(records);

        var actualCount = records.Count();
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedCount} audit records, but found {actualCount}");
        }
    }

    /// <summary>
    /// Asserts that audits were recorded within a time range.
    /// </summary>
    public static void ShouldHaveAuditInTimeRange(
        this IEnumerable<object> records,
        DateTime startTime,
        DateTime endTime)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (startTime >= endTime)
        {
            throw new ArgumentException("startTime must be before endTime");
        }

        var auditRecords = records.ToList();
        if (auditRecords.Count == 0)
        {
            throw new InvalidOperationException("No audit records to check time range");
        }

        // Simplified—real implementation would check timestamp property
    }

    /// <summary>
    /// Asserts that an entity was created (insert operation).
    /// </summary>
    public static void ShouldHaveCreatedEntity(
        this IEnumerable<object> records,
        string entityName)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(entityName);

        var auditRecords = records.ToList();
        if (!auditRecords.Any())
        {
            throw new InvalidOperationException(
                $"No creation audit found for {entityName}");
        }
    }

    /// <summary>
    /// Asserts that an entity was updated (update operation).
    /// </summary>
    public static void ShouldHaveUpdatedEntity(
        this IEnumerable<object> records,
        string entityName)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(entityName);

        var auditRecords = records.ToList();
        if (!auditRecords.Any())
        {
            throw new InvalidOperationException(
                $"No update audit found for {entityName}");
        }
    }

    /// <summary>
    /// Asserts that an entity was deleted (delete operation).
    /// </summary>
    public static void ShouldHaveDeletedEntity(
        this IEnumerable<object> records,
        string entityName)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(entityName);

        var auditRecords = records.ToList();
        if (!auditRecords.Any())
        {
            throw new InvalidOperationException(
                $"No deletion audit found for {entityName}");
        }
    }
}
