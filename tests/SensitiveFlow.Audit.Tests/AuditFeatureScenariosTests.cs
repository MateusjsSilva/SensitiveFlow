using System.Linq;
using SensitiveFlow.Audit.Implementations;
using SensitiveFlow.Audit.InMemory;
using SensitiveFlow.Audit.Tests.Stores;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

/// <summary>
/// Integration tests for all 5 Audit improvements:
/// 1. Async query streaming (large datasets)
/// 2. Anonymization workflow (subject deletion)
/// 3. Export formats (CSV, JSON, Parquet)
/// 4. Full-text search index
/// 5. Anomaly detection & alerting
/// </summary>
public class AuditFeatureScenariosTests
{
    private readonly InMemoryAuditStore _store = new();
    private readonly BasicAuditExporter _exporter = new();
    private readonly InMemoryAuditSearchIndex _searchIndex = new();

    #region Streaming Tests

    [Fact]
    public async Task QueryStreamAsync_WithLargeDataset_StreamsWithoutMaterializingAll()
    {
        // Arrange: Create 1000 records
        var records = Enumerable.Range(0, 1000)
            .Select(i => new AuditRecord
            {
                DataSubjectId = $"subject-{i % 10}",
                Entity = "TestEntity",
                Field = $"field-{i % 5}",
                Operation = AuditOperation.Access,
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(-i)
            })
            .ToList();

        foreach (var record in records)
        {
            await _store.AppendAsync(record);
        }

        // Act: Stream all records without pagination limit
        var query = new AuditQuery().ByEntity("TestEntity").WithPagination(0, 10_000);
        var streamedCount = 0;

        await foreach (var record in _store.QueryStreamAsync(query))
        {
            streamedCount++;
            Assert.NotNull(record);
        }

        // Assert: All records streamed
        Assert.Equal(1000, streamedCount);
    }

    [Fact]
    public async Task QueryStreamAsync_ByDataSubject_StreamsOnlyMatchingRecords()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            await _store.AppendAsync(new AuditRecord
            {
                DataSubjectId = i < 50 ? "subject-A" : "subject-B",
                Entity = "User",
                Field = "Email",
                Operation = AuditOperation.Access
            });
        }

        // Act
        var query = new AuditQuery().ByDataSubject("subject-A");
        var count = 0;

        await foreach (var record in _store.QueryStreamAsync(query))
        {
            Assert.Equal("subject-A", record.DataSubjectId);
            count++;
        }

        // Assert
        Assert.Equal(50, count);
    }

    [Fact]
    public async Task QueryStreamAsync_ProcessingCsvExport_HandlesLargeStream()
    {
        // Arrange: 500 records
        for (int i = 0; i < 500; i++)
        {
            await _store.AppendAsync(new AuditRecord
            {
                DataSubjectId = "user-123",
                Entity = "Order",
                Field = $"Column{i % 10}",
                Operation = AuditOperation.Access
            });
        }

        // Act: Export via stream without materializing
        var query = new AuditQuery().ByDataSubject("user-123").WithPagination(0, 10_000);
        var csv = await _exporter.ExportAsCsvAsync(
            _store.QueryStreamAsync(query),
            includeHash: false);

        // Assert: CSV generated with all records
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 500); // Header + 500 records
        Assert.Contains("DataSubjectId", lines[0]); // Header
    }

    #endregion

    #region Anonymization Tests

    [Fact]
    public async Task AnonymizationWorkflow_MarksSubjectAsAnonymized()
    {
        // Arrange
        var workflow = new TestAnonymizationWorkflow();
        const string subjectId = "user-to-delete";

        // Act
        var count = await workflow.AnonymizeByDataSubjectAsync(subjectId, "anon-token-123");

        // Assert
        Assert.True(await workflow.IsAnonymizedAsync(subjectId));
        var token = await workflow.GetAnonymizationTokenAsync(subjectId);
        Assert.Equal("anon-token-123", token);
    }

    [Fact]
    public async Task AnonymizationWorkflow_CountsAnonymizedRecords()
    {
        // Arrange
        var workflow = new TestAnonymizationWorkflow();
        await workflow.AppendRecordsAsync(new[]
        {
            new AuditRecord { DataSubjectId = "user-delete", Entity = "User", Field = "Email", Operation = AuditOperation.Access },
            new AuditRecord { DataSubjectId = "user-delete", Entity = "Order", Field = "Total", Operation = AuditOperation.Access },
            new AuditRecord { DataSubjectId = "user-delete", Entity = "Payment", Field = "Card", Operation = AuditOperation.Access }
        });

        // Act
        var count = await workflow.AnonymizeByDataSubjectAsync("user-delete", "token-xyz");

        // Assert: All 3 records tracked for anonymization
        Assert.Equal(3, count);
    }

    #endregion

    #region Export Format Tests

    [Fact]
    public async Task ExportAsCsv_IncludesAllFields()
    {
        // Arrange
        var records = new[]
        {
            new AuditRecord
            {
                Id = Guid.NewGuid(),
                DataSubjectId = "user-1",
                Entity = "Profile",
                Field = "Name",
                Operation = AuditOperation.Update,
                ActorId = "admin-1",
                Details = "Updated name from John to Jane"
            }
        };

        // Act
        async IAsyncEnumerable<AuditRecord> CreateAsyncStream()
        {
            foreach (var record in records)
            {
                yield return await Task.FromResult(record);
            }
        }

        var csv = await _exporter.ExportAsCsvAsync(
            CreateAsyncStream(),
            includeHash: false);

        // Assert
        Assert.Contains("user-1", csv);
        Assert.Contains("Profile", csv);
        Assert.Contains("admin-1", csv);
        Assert.Contains("Updated name from John to Jane", csv);
    }

    [Fact]
    public async Task ExportAsJson_FormatsValidJson()
    {
        // Arrange
        var records = new[]
        {
            new AuditRecord
            {
                DataSubjectId = "user-1",
                Entity = "Settings",
                Field = "Theme",
                Operation = AuditOperation.Update
            }
        };

        // Act
        async IAsyncEnumerable<AuditRecord> CreateAsyncStream()
        {
            foreach (var record in records)
            {
                yield return await Task.FromResult(record);
            }
        }

        var json = await _exporter.ExportAsJsonAsync(
            CreateAsyncStream(),
            prettyPrint: true,
            includeHash: false);

        // Assert: Valid JSON structure
        Assert.Contains("dataSubjectId", json);
        Assert.Contains("user-1", json);
        Assert.Contains("Settings", json);
    }

    [Fact]
    public void RecordToDictionary_ConvertsToDictionary()
    {
        // Arrange
        var record = new AuditRecord
        {
            DataSubjectId = "subject-1",
            Entity = "Account",
            Field = "Status",
            ActorId = "admin-1"
        };

        // Act
        var dict = _exporter.RecordToDictionary(record);

        // Assert
        Assert.Equal("subject-1", dict["DataSubjectId"]);
        Assert.Equal("Account", dict["Entity"]);
        Assert.Equal("admin-1", dict["ActorId"]);
    }

    #endregion

    #region Search Index Tests

    [Fact]
    public async Task SearchIndex_IndexAndSearchByActor()
    {
        // Arrange: Index 50 records with different actors
        for (int i = 0; i < 50; i++)
        {
            await _searchIndex.IndexAsync(new AuditRecord
            {
                DataSubjectId = $"user-{i}",
                Entity = "Document",
                Field = "Content",
                Operation = AuditOperation.Access,
                ActorId = i < 25 ? "admin@company.com" : "user@company.com"
            });
        }

        // Act: Search for admin records
        var results = await _searchIndex.SearchByActorAsync("admin@company.com");

        // Assert: Only admin records
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Contains("admin", r.ActorId!));
    }

    [Fact]
    public async Task SearchIndex_SearchByEntity()
    {
        // Arrange
        var entities = new[] { "User", "Order", "Invoice", "User", "Order" };
        foreach (var entity in entities)
        {
            await _searchIndex.IndexAsync(new AuditRecord
            {
                DataSubjectId = "user-123",
                Entity = entity,
                Field = "Status",
                Operation = AuditOperation.Access
            });
        }

        // Act
        var userResults = await _searchIndex.SearchByEntityAsync("User");
        var orderResults = await _searchIndex.SearchByEntityAsync("Order");

        // Assert
        Assert.Equal(2, userResults.Count);
        Assert.Equal(2, orderResults.Count);
    }

    [Fact]
    public async Task SearchIndex_RemoveByDataSubject()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _searchIndex.IndexAsync(new AuditRecord
            {
                DataSubjectId = i < 5 ? "user-to-remove" : "user-keep",
                Entity = "Data",
                Field = "Value",
                Operation = AuditOperation.Access
            });
        }

        // Act
        await _searchIndex.RemoveByDataSubjectAsync("user-to-remove");
        var results = await _searchIndex.SearchAsync("user-to-remove");

        // Assert: No records found
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchIndex_FullTextSearch()
    {
        // Arrange
        var records = new[]
        {
            new AuditRecord { DataSubjectId = "user-1", Entity = "Invoice", Field = "Amount", Operation = AuditOperation.Create, ActorId = "john@company.com" },
            new AuditRecord { DataSubjectId = "user-2", Entity = "Invoice", Field = "Status", Operation = AuditOperation.Update, ActorId = "jane@company.com" },
            new AuditRecord { DataSubjectId = "user-3", Entity = "Payment", Field = "Method", Operation = AuditOperation.Create, ActorId = "bob@company.com" }
        };

        foreach (var record in records)
        {
            await _searchIndex.IndexAsync(record);
        }

        // Act: Search for "Invoice"
        var invoiceResults = await _searchIndex.SearchAsync("Invoice");

        // Assert
        Assert.Equal(2, invoiceResults.Count);
    }

    #endregion

    #region Alerting Tests

    [Fact]
    public async Task AlertingPolicy_DetectsBulkDeletes()
    {
        // Arrange
        var policy = new BasicAuditAlertingPolicy(_store);

        // Create 60 delete operations (>50 threshold)
        for (int i = 0; i < 60; i++)
        {
            await _store.AppendAsync(new AuditRecord
            {
                DataSubjectId = $"user-{i}",
                Entity = "User",
                Field = "Email",
                Operation = AuditOperation.Delete
            });
        }

        // Act
        var alerts = await policy.DetectAnomaliesAsync(windowMinutes: 60);

        // Assert: Bulk delete detected
        var bulkDeleteAlert = alerts.FirstOrDefault(a => a.Message.Contains("Bulk delete"));
        Assert.NotNull(bulkDeleteAlert);
        Assert.Contains("Warning", bulkDeleteAlert.Severity);
    }

    [Fact]
    public async Task AlertingPolicy_DetectsMultipleIpsPerSubject()
    {
        // Arrange
        var policy = new BasicAuditAlertingPolicy(_store);

        // Same user from 4 different IPs (>3 threshold)
        var ips = new[] { "ip-token-1", "ip-token-2", "ip-token-3", "ip-token-4" };
        foreach (var ip in ips)
        {
            await _store.AppendAsync(new AuditRecord
            {
                DataSubjectId = "user-suspicious",
                Entity = "Account",
                Field = "Login",
                Operation = AuditOperation.Access,
                IpAddressToken = ip
            });
        }

        // Act
        var alerts = await policy.DetectAnomaliesAsync(windowMinutes: 60);

        // Assert: Multiple IP alert
        var ipAlert = alerts.FirstOrDefault(a => a.Message.Contains("different IPs"));
        Assert.NotNull(ipAlert);
        Assert.Equal("Warning", ipAlert.Severity);
    }

    [Fact]
    public async Task AlertingPolicy_CustomRule()
    {
        // Arrange
        var policy = new BasicAuditAlertingPolicy(_store);

        await _store.AppendAsync(new AuditRecord
        {
            DataSubjectId = "user-1",
            Entity = "Sensitive",
            Field = "SecretKey",
            Operation = AuditOperation.Access
        });

        // Register custom rule: alert on SecretKey access
        async IAsyncEnumerable<AuditAlert> SecretKeyDetector(IAsyncEnumerable<AuditRecord> records)
        {
            await foreach (var record in records)
            {
                if (record.Field == "SecretKey")
                {
                    yield return new AuditAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Severity = "Critical",
                        Message = $"Sensitive field accessed: {record.Field}",
                        TriggeredAt = DateTimeOffset.UtcNow
                    };
                }
            }
        }

        Func<IAsyncEnumerable<AuditRecord>, IAsyncEnumerable<AuditAlert>> detector = SecretKeyDetector;
        await policy.RegisterRuleAsync("SecretKeyDetection", detector);

        // Act
        var alerts = await policy.DetectAnomaliesAsync(windowMinutes: 60);

        // Assert: Custom rule triggered
        var sensitiveAlert = alerts.FirstOrDefault(a => a.Message.Contains("SecretKey"));
        Assert.NotNull(sensitiveAlert);
        Assert.Equal("Critical", sensitiveAlert.Severity);
    }

    [Fact]
    public async Task AlertingPolicy_RegisterAndUnregisterRules()
    {
        // Arrange
        var policy = new BasicAuditAlertingPolicy(_store);

        // Act: Register a rule
        async IAsyncEnumerable<AuditAlert> EmptyRule(IAsyncEnumerable<AuditRecord> records)
        {
            // Empty generator for testing
            await foreach (var _ in records)
            {
                // No alerts
            }
            yield break;
        }

        await policy.RegisterRuleAsync("TestRule", EmptyRule);

        var rules1 = await policy.GetRegisteredRulesAsync();
        Assert.Contains("TestRule", rules1);

        // Unregister
        await policy.UnregisterRuleAsync("TestRule");
        var rules2 = await policy.GetRegisteredRulesAsync();
        Assert.DoesNotContain("TestRule", rules2);
    }

    #endregion

    /// <summary>
    /// Test implementation of anonymization workflow for unit tests.
    /// </summary>
    private class TestAnonymizationWorkflow : IAnonymizationWorkflow
    {
        private readonly Dictionary<string, string> _anonymizationTokens = new();
        private readonly List<AuditRecord> _records = new();

        public async Task AppendRecordsAsync(IEnumerable<AuditRecord> records)
        {
            _records.AddRange(records);
            await Task.CompletedTask;
        }

        public Task<int> AnonymizeByDataSubjectAsync(
            string dataSubjectId,
            string anonymizationToken,
            CancellationToken cancellationToken = default)
        {
            _anonymizationTokens[dataSubjectId] = anonymizationToken;
            var count = _records.Count(r => r.DataSubjectId == dataSubjectId);
            return Task.FromResult(count);
        }

        public Task<int> AnonymizeByQueryAsync(
            AuditQuery query,
            string anonymizationToken,
            CancellationToken cancellationToken = default)
        {
            var count = _records.Count;
            return Task.FromResult(count);
        }

        public Task<bool> IsAnonymizedAsync(string dataSubjectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_anonymizationTokens.ContainsKey(dataSubjectId));
        }

        public Task<string?> GetAnonymizationTokenAsync(string dataSubjectId, CancellationToken cancellationToken = default)
        {
            _anonymizationTokens.TryGetValue(dataSubjectId, out var token);
            return Task.FromResult(token);
        }
    }
}
