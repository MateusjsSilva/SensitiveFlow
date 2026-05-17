using System;
using System.Collections.Generic;

namespace SensitiveFlow.TestKit.Fixtures;

/// <summary>
/// Captures and restores audit state between test runs.
/// </summary>
public sealed class AuditFixtureSnapshot
{
    /// <summary>Gets the snapshot identifier.</summary>
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>Gets the audit records in snapshot.</summary>
    public List<object> Records { get; set; } = new();

    /// <summary>Gets the timestamp of snapshot creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets optional metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a snapshot from current records.
    /// </summary>
    public static AuditFixtureSnapshot Create(string id, IEnumerable<object> records)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(records);

        return new AuditFixtureSnapshot
        {
            SnapshotId = id,
            Records = new List<object>(records)
        };
    }

    /// <summary>
    /// Restores records from snapshot.
    /// </summary>
    public IEnumerable<object> Restore()
    {
        return Records;
    }

    /// <summary>
    /// Gets a summary of snapshot contents.
    /// </summary>
    public string GetSummary()
    {
        return $"Snapshot '{SnapshotId}': {Records.Count} records, created {CreatedAt:O}";
    }
}

/// <summary>
/// Manager for audit fixture snapshots.
/// </summary>
public sealed class AuditFixtureSnapshotManager
{
    private readonly Dictionary<string, AuditFixtureSnapshot> _snapshots = new(StringComparer.Ordinal);

    /// <summary>
    /// Saves a snapshot.
    /// </summary>
    public void SaveSnapshot(string id, IEnumerable<object> records, Dictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(records);

        var snapshot = AuditFixtureSnapshot.Create(id, records);
        if (metadata is not null)
        {
            snapshot.Metadata = new Dictionary<string, object>(metadata);
        }

        _snapshots[id] = snapshot;
    }

    /// <summary>
    /// Loads a snapshot.
    /// </summary>
    public AuditFixtureSnapshot? LoadSnapshot(string id)
    {
        _snapshots.TryGetValue(id, out var snapshot);
        return snapshot;
    }

    /// <summary>
    /// Gets all snapshot IDs.
    /// </summary>
    public IEnumerable<string> GetSnapshotIds() => _snapshots.Keys;

    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    public bool DeleteSnapshot(string id)
    {
        return _snapshots.Remove(id);
    }

    /// <summary>
    /// Clears all snapshots.
    /// </summary>
    public void ClearAll()
    {
        _snapshots.Clear();
    }
}
