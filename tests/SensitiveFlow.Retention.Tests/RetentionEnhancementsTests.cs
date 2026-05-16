using System.Reflection;
using SensitiveFlow.Retention.Analytics;
using SensitiveFlow.Retention.Archive;
using SensitiveFlow.Retention.Notifications;
using SensitiveFlow.Retention.Scheduling;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Tests;

public class RetentionEnhancementsTests
{
    #region Incremental Scheduling Tests

    [Fact]
    public void RetentionRunTracker_GetLastRunAt_ReturnsNull()
    {
        var tracker = new RetentionRunTracker();
        var result = tracker.GetLastRunAt("policy1");
        Assert.Null(result);
    }

    [Fact]
    public void RetentionRunTracker_MarkRanAt_StoresValue()
    {
        var tracker = new RetentionRunTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.MarkRanAt("policy1", now);
        var result = tracker.GetLastRunAt("policy1");
        Assert.NotNull(result);
        Assert.Equal(now, result);
    }

    [Fact]
    public void RetentionRunTracker_MultipleKeys()
    {
        var tracker = new RetentionRunTracker();
        var time1 = DateTimeOffset.UtcNow;
        var time2 = time1.AddHours(1);
        tracker.MarkRanAt("policy1", time1);
        tracker.MarkRanAt("policy2", time2);
        Assert.Equal(time1, tracker.GetLastRunAt("policy1"));
        Assert.Equal(time2, tracker.GetLastRunAt("policy2"));
    }

    [Fact]
    public void RetentionRunTracker_IgnoresNullKey()
    {
        var tracker = new RetentionRunTracker();
        var result = tracker.GetLastRunAt(null!);
        Assert.Null(result);
    }

    [Fact]
    public void RetentionRunTracker_IgnoresEmptyKey()
    {
        var tracker = new RetentionRunTracker();
        tracker.MarkRanAt("", DateTimeOffset.UtcNow);
        var result = tracker.GetLastRunAt("");
        Assert.Null(result);
    }

    #endregion

    #region Parallel Execution Tests

    [Fact]
    public async Task ParallelRetentionExecutor_NullBatches_Throws()
    {
        var executor = new ParallelRetentionExecutor();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            executor.ExecuteParallelAsync(null!));
    }

    [Fact]
    public async Task ParallelRetentionExecutor_EmptyBatches_ReturnsEmptyReport()
    {
        var executor = new ParallelRetentionExecutor();
        var result = await executor.ExecuteParallelAsync(new List<RetentionBatch>());
        Assert.NotNull(result);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task ParallelRetentionExecutor_SingleBatch()
    {
        var executor = new ParallelRetentionExecutor();
        var entity = new TestEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var batch = new RetentionBatch(
            new[] { (object)entity },
            e => ((TestEntity)e).CreatedAt
        );
        var result = await executor.ExecuteParallelAsync(new[] { batch });
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParallelRetentionExecutor_MultipleBatches()
    {
        var executor = new ParallelRetentionExecutor();
        var entity1 = new TestEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var entity2 = new TestEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-3) };
        var batches = new[]
        {
            new RetentionBatch(new[] { (object)entity1 }, e => ((TestEntity)e).CreatedAt),
            new RetentionBatch(new[] { (object)entity2 }, e => ((TestEntity)e).CreatedAt)
        };
        var result = await executor.ExecuteParallelAsync(batches);
        Assert.NotNull(result);
    }

    #endregion

    #region Analytics Tests

    [Fact]
    public void RetentionAnalyticsCollector_RecordRun_NullReport_Throws()
    {
        var collector = new RetentionAnalyticsCollector();
        Assert.Throws<ArgumentNullException>(() =>
            collector.RecordRun(null!, DateTimeOffset.UtcNow, 100));
    }

    [Fact]
    public void RetentionAnalyticsCollector_RecordRun_StoresRecord()
    {
        var collector = new RetentionAnalyticsCollector();
        var report = new RetentionExecutionReport();
        var now = DateTimeOffset.UtcNow;
        collector.RecordRun(report, now, 50);
        var history = collector.GetRunHistory();
        Assert.Single(history);
        Assert.Equal(now, history[0].RunAt);
        Assert.Equal(50, history[0].DurationMs);
    }

    [Fact]
    public void RetentionAnalyticsCollector_MultipleRecords()
    {
        var collector = new RetentionAnalyticsCollector();
        var report1 = new RetentionExecutionReport();
        var report2 = new RetentionExecutionReport();
        var now = DateTimeOffset.UtcNow;
        collector.RecordRun(report1, now, 50);
        collector.RecordRun(report2, now.AddMinutes(1), 75);
        var history = collector.GetRunHistory();
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void RetentionAnalyticsCollector_TrendSummary_EmptyHistory()
    {
        var collector = new RetentionAnalyticsCollector();
        var summary = collector.GetTrendSummary();
        Assert.Equal(0, summary.TotalRuns);
        Assert.Equal(0, summary.TotalAnonymized);
        Assert.Null(summary.LastRunAt);
        Assert.Null(summary.PeakAnonymizedRun);
    }

    [Fact]
    public void RetentionAnalyticsCollector_TrendSummary_WithData()
    {
        var collector = new RetentionAnalyticsCollector();
        var report = CreateTestReport(anonymizedCount: 10, deletePendingCount: 5);
        var now = DateTimeOffset.UtcNow;
        collector.RecordRun(report, now, 100);
        var summary = collector.GetTrendSummary();
        Assert.Equal(1, summary.TotalRuns);
        Assert.Equal(10, summary.TotalAnonymized);
        Assert.Equal(5, summary.TotalDeletePending);
        Assert.Equal(10, summary.AverageAnonymizedPerRun);
        Assert.Equal(now, summary.LastRunAt);
    }

    #endregion

    #region Re-anonymization Tests

    [Fact]
    public async Task RetentionReAnonymizer_NullEntities_Throws()
    {
        var reAnon = new RetentionReAnonymizer();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            reAnon.ReAnonymizeAsync<TestEntity>(null!, e => true));
    }

    [Fact]
    public async Task RetentionReAnonymizer_NullPredicate_Throws()
    {
        var reAnon = new RetentionReAnonymizer();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            reAnon.ReAnonymizeAsync(new List<TestEntity>(), null!));
    }

    [Fact]
    public async Task RetentionReAnonymizer_EmptyEntities()
    {
        var reAnon = new RetentionReAnonymizer();
        var result = await reAnon.ReAnonymizeAsync(new List<TestEntity>(), e => true);
        Assert.NotNull(result);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task RetentionReAnonymizer_FiltersWithPredicate()
    {
        var reAnon = new RetentionReAnonymizer();
        var entities = new[]
        {
            new TestEntity { Id = 1, CreatedAt = DateTimeOffset.UtcNow },
            new TestEntity { Id = 2, CreatedAt = DateTimeOffset.UtcNow }
        };
        var result = await reAnon.ReAnonymizeAsync(entities, e => e.Id == 1);
        Assert.NotNull(result);
    }

    #endregion

    #region Archive Tests

    [Fact]
    public async Task InMemoryArchive_ArchiveAsync_NullEntities_Throws()
    {
        var archive = new InMemoryRetentionArchiveProvider();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            archive.ArchiveAsync(null!, "key"));
    }

    [Fact]
    public async Task InMemoryArchive_ArchiveAsync_NullKey_Throws()
    {
        var archive = new InMemoryRetentionArchiveProvider();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            archive.ArchiveAsync(new List<object>(), null!));
    }

    [Fact]
    public async Task InMemoryArchive_ArchiveAsync_StoresEntities()
    {
        var archive = new InMemoryRetentionArchiveProvider();
        var entities = new[] { new object(), new object() };
        await archive.ArchiveAsync(entities, "key1");
        var retrieved = await archive.RetrieveAsync("key1");
        Assert.Equal(2, retrieved.Count);
    }

    [Fact]
    public async Task InMemoryArchive_RetrieveAsync_NonexistentKey()
    {
        var archive = new InMemoryRetentionArchiveProvider();
        var result = await archive.RetrieveAsync("nonexistent");
        Assert.Empty(result);
    }

    [Fact]
    public async Task InMemoryArchive_ListArchiveKeysAsync()
    {
        var archive = new InMemoryRetentionArchiveProvider();
        await archive.ArchiveAsync(new[] { new object() }, "key1");
        await archive.ArchiveAsync(new[] { new object() }, "key2");
        var keys = await archive.ListArchiveKeysAsync();
        Assert.Equal(2, keys.Count);
        Assert.Contains("key1", keys);
        Assert.Contains("key2", keys);
    }

    #endregion

    #region Notification Tests

    [Fact]
    public void RetentionNotificationTemplate_Format_SubstitutesAnonymizedCount()
    {
        var template = new RetentionNotificationTemplate
        {
            Subject = "Retention Report",
            Body = "Anonymized: {AnonymizedCount}",
            Channel = RetentionNotificationChannel.Email
        };
        var report = CreateTestReport(anonymizedCount: 42, deletePendingCount: 0);
        var result = template.Format(report);
        Assert.Contains("42", result);
    }

    [Fact]
    public void RetentionNotificationTemplate_Format_SubstitutesDeletePendingCount()
    {
        var template = new RetentionNotificationTemplate
        {
            Body = "Delete Pending: {DeletePendingCount}"
        };
        var report = CreateTestReport(anonymizedCount: 0, deletePendingCount: 15);
        var result = template.Format(report);
        Assert.Contains("15", result);
    }

    [Fact]
    public void RetentionNotificationTemplate_Format_NullReport_Throws()
    {
        var template = new RetentionNotificationTemplate();
        Assert.Throws<ArgumentNullException>(() => template.Format(null!));
    }

    [Fact]
    public void RetentionNotificationChannel_Values()
    {
        Assert.Equal(0, (int)RetentionNotificationChannel.Email);
        Assert.Equal(1, (int)RetentionNotificationChannel.Slack);
        Assert.Equal(2, (int)RetentionNotificationChannel.Webhook);
    }

    #endregion

    #region Report Generation Tests

    [Fact]
    public void RetentionReportGenerator_GenerateTextReport_NullSummary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RetentionReportGenerator.GenerateTextReport(null!));
    }

    [Fact]
    public void RetentionReportGenerator_GenerateTextReport_ContainsMetrics()
    {
        var summary = new RetentionTrendSummary(
            TotalRuns: 5,
            TotalAnonymized: 100,
            TotalDeletePending: 20,
            AverageAnonymizedPerRun: 20,
            LastRunAt: DateTimeOffset.UtcNow,
            PeakAnonymizedRun: null
        );
        var report = RetentionReportGenerator.GenerateTextReport(summary);
        Assert.Contains("Total Runs: 5", report);
        Assert.Contains("Total Fields Anonymized: 100", report);
        Assert.Contains("Total Entities Marked for Deletion: 20", report);
    }

    [Fact]
    public void RetentionReportGenerator_GenerateCsvReport_NullRecords_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RetentionReportGenerator.GenerateCsvReport(null!));
    }

    [Fact]
    public void RetentionReportGenerator_GenerateCsvReport_HasHeader()
    {
        var records = new List<RetentionRunRecord>();
        var report = RetentionReportGenerator.GenerateCsvReport(records);
        Assert.Contains("RunAt", report);
        Assert.Contains("AnonymizedCount", report);
        Assert.Contains("DeletePendingCount", report);
        Assert.Contains("DurationMs", report);
    }

    [Fact]
    public void RetentionReportGenerator_GenerateCsvReport_WithData()
    {
        var now = DateTimeOffset.UtcNow;
        var records = new List<RetentionRunRecord>
        {
            new(now, 10, 5, 100)
        };
        var report = RetentionReportGenerator.GenerateCsvReport(records);
        Assert.Contains("10", report);
        Assert.Contains("5", report);
    }

    [Fact]
    public void RetentionReportGenerator_GenerateJsonReport_NullSummary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RetentionReportGenerator.GenerateJsonReport(null!));
    }

    [Fact]
    public void RetentionReportGenerator_GenerateJsonReport_IsValidJson()
    {
        var summary = new RetentionTrendSummary(
            TotalRuns: 1,
            TotalAnonymized: 10,
            TotalDeletePending: 5,
            AverageAnonymizedPerRun: 10,
            LastRunAt: null,
            PeakAnonymizedRun: null
        );
        var report = RetentionReportGenerator.GenerateJsonReport(summary);
        Assert.Contains("{", report);
        Assert.Contains("}", report);
        Assert.Contains("TotalRuns", report);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void RetentionEnhancements_AllComponentsTogether()
    {
        var tracker = new RetentionRunTracker();
        var collector = new RetentionAnalyticsCollector();
        var archive = new InMemoryRetentionArchiveProvider();
        var template = new RetentionNotificationTemplate();

        Assert.NotNull(tracker);
        Assert.NotNull(collector);
        Assert.NotNull(archive);
        Assert.NotNull(template);
    }

    #endregion

    #region Test Helpers

    private class TestEntity
    {
        public int Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    private static RetentionExecutionReport CreateTestReport(int anonymizedCount = 0, int deletePendingCount = 0)
    {
        var report = new RetentionExecutionReport();
        var addMethod = typeof(RetentionExecutionReport).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);

        var entity = new TestEntity { Id = 1 };
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < anonymizedCount; i++)
        {
            var entry = new RetentionExecutionEntry(entity, $"Field{i}", now, RetentionAction.Anonymized);
            addMethod?.Invoke(report, new object[] { entry });
        }

        for (int i = 0; i < deletePendingCount; i++)
        {
            var entry = new RetentionExecutionEntry(new TestEntity { Id = 100 + i }, $"Field{i}", now, RetentionAction.DeletePending);
            addMethod?.Invoke(report, new object[] { entry });
        }

        return report;
    }

    #endregion
}
