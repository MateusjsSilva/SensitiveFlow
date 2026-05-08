namespace SensitiveFlow.Retention.Services;

/// <summary>Aggregated result of a <see cref="RetentionExecutor"/> run.</summary>
public sealed class RetentionExecutionReport
{
    private readonly List<RetentionExecutionEntry> _entries = [];

    /// <summary>Per-field execution entries, in evaluation order.</summary>
    public IReadOnlyList<RetentionExecutionEntry> Entries => _entries;

    /// <summary>Number of fields whose value was anonymized in place.</summary>
    public int AnonymizedFieldCount => _entries.Count(e => e.Action == RetentionAction.Anonymized);

    /// <summary>Number of distinct entities flagged for deletion.</summary>
    public int DeletePendingEntityCount =>
        _entries.Where(e => e.Action == RetentionAction.DeletePending)
                .Select(e => e.Entity)
                .Distinct()
                .Count();

    internal void Add(RetentionExecutionEntry entry) => _entries.Add(entry);
}

/// <summary>Outcome for a single (entity, field) pair processed by the executor.</summary>
public sealed class RetentionExecutionEntry
{
    /// <summary>Initializes a new instance.</summary>
    public RetentionExecutionEntry(object entity, string fieldName, DateTimeOffset expiredAt, RetentionAction action)
    {
        Entity = entity;
        FieldName = fieldName;
        ExpiredAt = expiredAt;
        Action = action;
    }

    /// <summary>The entity instance being evaluated.</summary>
    public object Entity { get; }

    /// <summary>Name of the field whose retention period expired.</summary>
    public string FieldName { get; }

    /// <summary>When the retention period expired.</summary>
    public DateTimeOffset ExpiredAt { get; }

    /// <summary>The action taken — or required from the caller — for this field.</summary>
    public RetentionAction Action { get; }
}
