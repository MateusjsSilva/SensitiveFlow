using System.Linq;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Implementations;

/// <summary>
/// Basic anomaly detection with built-in rules for bulk deletes, multiple IPs, and access after deletion.
/// </summary>
public sealed class BasicAuditAlertingPolicy : IAuditAlertingPolicy
{
    private readonly IAuditStore _store;
    private readonly object _lock = new();
    private readonly List<AuditAlert> _alerts = new();
    private readonly Dictionary<string, Func<IAsyncEnumerable<AuditRecord>, IAsyncEnumerable<AuditAlert>>> _customRules = new();

    /// <summary>Initializes a new instance.</summary>
    /// <param name="store">Audit store for querying records.</param>
    public BasicAuditAlertingPolicy(IAuditStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditAlert>> DetectAnomaliesAsync(
        int windowMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var fromTime = DateTimeOffset.UtcNow.AddMinutes(-windowMinutes);

        var query = new AuditQuery()
            .InTimeRange(fromTime, DateTimeOffset.UtcNow)
            .WithPagination(0, 10_000);

        var records = await _store.QueryAsync(query, cancellationToken);

        var detectedAlerts = new List<AuditAlert>();

        // Built-in rules
        detectedAlerts.AddRange(await DetectBulkDeletesAsync(records, cancellationToken));
        detectedAlerts.AddRange(await DetectMultipleIpsPerSubjectAsync(records, cancellationToken));
        detectedAlerts.AddRange(await DetectAccessAfterDeletionAsync(records, cancellationToken));

        // Custom rules
        foreach (var (ruleName, detector) in _customRules)
        {
            async IAsyncEnumerable<AuditRecord> CreateAsyncEnumerable()
            {
                foreach (var record in records)
                {
                    yield return await Task.FromResult(record);
                }
            }

            await foreach (var alert in detector(CreateAsyncEnumerable()).WithCancellation(cancellationToken))
            {
                detectedAlerts.Add(alert);
            }
        }

        lock (_lock)
        {
            _alerts.AddRange(detectedAlerts);
        }

        return detectedAlerts;
    }

    /// <inheritdoc />
    public Task RegisterRuleAsync(
        string ruleName,
        Func<IAsyncEnumerable<AuditRecord>, IAsyncEnumerable<AuditAlert>> detector)
    {
        lock (_lock)
        {
            _customRules[ruleName] = detector;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnregisterRuleAsync(string ruleName)
    {
        lock (_lock)
        {
            _customRules.Remove(ruleName);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetRegisteredRulesAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<string>>(_customRules.Keys.ToList());
        }
    }

    /// <inheritdoc />
    public Task ClearAlertsAsync()
    {
        lock (_lock)
        {
            _alerts.Clear();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditAlert>> GetAlertsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<AuditAlert>>(_alerts.ToList());
        }
    }

    /// <summary>Detects bulk delete patterns.</summary>
    /// <summary>Detects bulk delete patterns.</summary>
    private static Task<IReadOnlyList<AuditAlert>> DetectBulkDeletesAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken)
    {
        var deletes = new Dictionary<string, int>();
        var deletedRecords = new Dictionary<string, List<string>>();

        foreach (var record in records)
        {
            if (record.Operation == AuditOperation.Delete)
            {
                var key = record.Entity;
                if (!deletes.ContainsKey(key))
                {
                    deletes[key] = 0;
                    deletedRecords[key] = new List<string>();
                }

                deletes[key]++;
                deletedRecords[key].Add(record.DataSubjectId);
            }
        }

        var alerts = new List<AuditAlert>();

        foreach (var (entity, count) in deletes)
        {
            if (count > 50)
            {
                alerts.Add(new AuditAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    Severity = count > 500 ? "Critical" : "Warning",
                    Message = $"Bulk delete detected: {count} {entity} records deleted in time window",
                    TriggeredAt = DateTimeOffset.UtcNow,
                    Entities = new[] { entity },
                    DataSubjectIds = deletedRecords[entity].Take(10).ToArray()
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AuditAlert>>(alerts);
    }

    private static Task<IReadOnlyList<AuditAlert>> DetectMultipleIpsPerSubjectAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken)
    {
        var ipsPerSubject = new Dictionary<string, HashSet<string>>();

        foreach (var record in records)
        {
            if (!string.IsNullOrEmpty(record.DataSubjectId) && !string.IsNullOrEmpty(record.IpAddressToken))
            {
                if (!ipsPerSubject.ContainsKey(record.DataSubjectId))
                {
                    ipsPerSubject[record.DataSubjectId] = new HashSet<string>();
                }

                ipsPerSubject[record.DataSubjectId].Add(record.IpAddressToken);
            }
        }

        var alerts = new List<AuditAlert>();

        foreach (var (subjectId, ips) in ipsPerSubject)
        {
            if (ips.Count > 3)
            {
                alerts.Add(new AuditAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    Severity = "Warning",
                    Message = $"Subject {subjectId} accessed from {ips.Count} different IPs",
                    TriggeredAt = DateTimeOffset.UtcNow,
                    DataSubjectIds = new[] { subjectId },
                    Context = new Dictionary<string, object> { { "IpCount", ips.Count } }
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AuditAlert>>(alerts);
    }

    private static Task<IReadOnlyList<AuditAlert>> DetectAccessAfterDeletionAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken)
    {
        var deleteTimestamps = new Dictionary<string, DateTimeOffset>();
        var alerts = new List<AuditAlert>();

        foreach (var record in records.OrderBy(r => r.Timestamp))
        {
            if (record.Operation == AuditOperation.Delete)
            {
                deleteTimestamps[record.Entity + ":" + record.DataSubjectId] = record.Timestamp;
            }
            else if (record.Operation == AuditOperation.Access)
            {
                var key = record.Entity + ":" + record.DataSubjectId;
                if (deleteTimestamps.TryGetValue(key, out var deleteTime) && record.Timestamp > deleteTime)
                {
                    alerts.Add(new AuditAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Severity = "Critical",
                        Message = $"Access detected after deletion of {record.Entity}:{record.DataSubjectId}",
                        TriggeredAt = DateTimeOffset.UtcNow,
                        Entities = new[] { record.Entity },
                        DataSubjectIds = new[] { record.DataSubjectId }
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<AuditAlert>>(alerts);
    }
}
