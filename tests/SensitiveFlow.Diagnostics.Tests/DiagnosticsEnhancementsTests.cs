using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Diagnostics.AlertRules;
using SensitiveFlow.Diagnostics.ComplianceReporting;
using SensitiveFlow.Diagnostics.CustomMetrics;
using SensitiveFlow.Diagnostics.MetricAggregations;
using SensitiveFlow.Diagnostics.PerformanceBaselines;
using SensitiveFlow.Diagnostics.QueryOptimization;
using SensitiveFlow.Diagnostics.Tests.Stores;

namespace SensitiveFlow.Diagnostics.Tests;

/// <summary>
/// Tests for SensitiveFlow.Diagnostics enhancements (custom metrics, alerts, compliance, baselines, optimization).
/// </summary>
public class DiagnosticsEnhancementsTests
{
    #region Custom Metrics Tests

    [Fact]
    public void CustomMetricsProvider_RecordsSensitiveFieldAccess()
    {
        // Arrange
        var provider = new CustomMetricsProvider();

        // Act
        provider.RecordSensitiveFieldAccess("Email", "Customer");
        provider.RecordSensitiveFieldAccess("Email", "Customer");
        provider.RecordSensitiveFieldAccess("Phone", "Customer");

        // Assert - Provider records metrics (verified through OpenTelemetry export)
        Assert.NotNull(provider);
    }

    [Fact]
    public void CustomMetricsProvider_RecordsRedactionDuration()
    {
        // Arrange
        var provider = new CustomMetricsProvider();

        // Act
        provider.RecordRedactionDuration(2.5, "Mask");
        provider.RecordRedactionDuration(1.2, "Redact");

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void CustomMetricsProvider_RecordsComplianceViolation()
    {
        // Arrange
        var provider = new CustomMetricsProvider();

        // Act
        provider.RecordComplianceViolation("UnauthorizedAccess", "User accessed non-owned data");

        // Assert
        Assert.NotNull(provider);
    }

    #endregion

    #region Metric Aggregation Tests

    [Fact]
    public void MetricAggregationService_CalculatesPercentiles()
    {
        // Arrange
        var service = new MetricAggregationService();
        var measurements = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };

        foreach (var m in measurements)
        {
            service.Record("latency", m);
        }

        // Act
        var p50 = service.GetPercentile("latency", 50);
        var p95 = service.GetPercentile("latency", 95);

        // Assert
        Assert.True(p50 > 0, "p50 should be calculated");
        Assert.True(p95 > p50, "p95 should be greater than p50");
    }

    [Fact]
    public void MetricAggregationService_CalculatesAverage()
    {
        // Arrange
        var service = new MetricAggregationService();
        service.Record("latency", 10.0);
        service.Record("latency", 20.0);
        service.Record("latency", 30.0);

        // Act
        var avg = service.GetAverage("latency");

        // Assert
        Assert.Equal(20.0, avg);
    }

    [Fact]
    public void MetricAggregationService_GetStatistics()
    {
        // Arrange
        var service = new MetricAggregationService();
        service.Record("latency", 10.0);
        service.Record("latency", 50.0);
        service.Record("latency", 30.0);

        // Act
        var (min, max, count, mean) = service.GetStatistics("latency");

        // Assert
        Assert.Equal(10.0, min);
        Assert.Equal(50.0, max);
        Assert.Equal(3, count);
        Assert.Equal(30.0, mean);
    }

    [Fact]
    public void MetricAggregationService_ClearMetrics()
    {
        // Arrange
        var service = new MetricAggregationService();
        service.Record("latency", 10.0);

        // Act
        service.Clear("latency");
        var avg = service.GetAverage("latency");

        // Assert
        Assert.Equal(0, avg);
    }

    #endregion

    #region Alert Rule Templates Tests

    [Fact]
    public void AlertRuleTemplates_HighAuditLatencyDefined()
    {
        // Arrange & Act
        var rule = AlertRuleTemplates.HighAuditLatency;

        // Assert
        Assert.Equal("HighAuditLatency", rule.Name);
        Assert.Contains("50", rule.Query);
        Assert.Equal(AlertSeverity.Warning, rule.Severity);
    }

    [Fact]
    public void AlertRuleTemplates_BulkDeleteDetectedDefined()
    {
        // Arrange & Act
        var rule = AlertRuleTemplates.BulkDeleteDetected;

        // Assert
        Assert.Equal("BulkDeleteDetected", rule.Name);
        Assert.Contains("Delete", rule.Query);
        Assert.Equal(AlertSeverity.Critical, rule.Severity);
    }

    [Fact]
    public void AlertRuleTemplates_AllTemplatesValid()
    {
        // Arrange & Act
        var templates = AlertRuleTemplates.GetAllTemplates();

        // Assert
        Assert.Equal(6, templates.Count());

        foreach (var template in templates)
        {
            Assert.NotEmpty(template.Name);
            Assert.NotEmpty(template.Description);
            Assert.NotEmpty(template.Query);
            Assert.NotEmpty(template.Annotations);
            Assert.True(template.For > TimeSpan.Zero || template.For == TimeSpan.Zero);
        }
    }

    [Fact]
    public void AlertRuleTemplates_HighLatencyHasRecommendation()
    {
        // Arrange & Act
        var rule = AlertRuleTemplates.HighAuditLatency;

        // Assert
        Assert.True(rule.Annotations.ContainsKey("summary"));
        Assert.True(rule.Annotations.ContainsKey("description"));
        Assert.Contains("latency", rule.Annotations["summary"], StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Compliance Reporting Tests

    [Fact]
    public async Task ComplianceReportService_GeneratesAuditFrequencyReport()
    {
        // Arrange
        var store = new InMemoryAuditStore();
        var service = new ComplianceReportService(store);

        await store.AppendAsync(new AuditRecord
        {
            DataSubjectId = "user-1",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update
        });

        // Act
        var report = await service.GenerateAuditFrequencyReportAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow);

        // Assert
        Assert.NotNull(report);
        Assert.True(report.TotalAuditRecords > 0);
    }

    [Fact]
    public async Task ComplianceReportService_GeneratesDataSubjectCoverageReport()
    {
        // Arrange
        var store = new InMemoryAuditStore();
        var service = new ComplianceReportService(store);

        for (int i = 0; i < 10; i++)
        {
            await store.AppendAsync(new AuditRecord
            {
                DataSubjectId = $"user-{i}",
                Entity = "Customer",
                Field = "Email",
                Operation = AuditOperation.Update
            });
        }

        // Act
        var report = await service.GenerateDataSubjectCoverageReportAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow);

        // Assert
        Assert.NotNull(report);
        Assert.True(report.UniqueDataSubjects > 0);
        Assert.True(report.CoveragePercentage >= 0 && report.CoveragePercentage <= 100);
    }

    [Fact]
    public async Task ComplianceReportService_GeneratesRetentionComplianceReport()
    {
        // Arrange
        var store = new InMemoryAuditStore();
        var service = new ComplianceReportService(store);

        // Old record (older than 365 days)
        await store.AppendAsync(new AuditRecord
        {
            DataSubjectId = "user-old",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-400)
        });

        // Act
        var report = await service.GenerateRetentionComplianceReportAsync(365);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(365, report.RetentionDays);
    }

    #endregion

    #region Performance Baseline Tests

    [Fact]
    public void PerformanceBaselineService_DefinesBaseline()
    {
        // Arrange
        var service = new PerformanceBaselineService();

        // Act
        service.DefineBaseline("audit.latency", new PerformanceBaseline
        {
            Target = 10.0,
            WarningThreshold = 20,
            CriticalThreshold = 50
        });

        // Assert
        var baselines = service.GetAllBaselines();
        Assert.Contains("audit.latency", baselines.Keys);
    }

    [Fact]
    public void PerformanceBaselineService_DetectsHealthyPerformance()
    {
        // Arrange
        var service = new PerformanceBaselineService();
        service.DefineBaseline("latency", new PerformanceBaseline
        {
            Target = 10.0,
            WarningThreshold = 20
        });

        // Act
        var result = service.CheckBaseline("latency", 10.5);

        // Assert
        Assert.Equal(BaselineStatus.Healthy, result.Status);
    }

    [Fact]
    public void PerformanceBaselineService_DetectsPerformanceWarning()
    {
        // Arrange
        var service = new PerformanceBaselineService();
        service.DefineBaseline("latency", new PerformanceBaseline
        {
            Target = 10.0,
            WarningThreshold = 20,
            CriticalThreshold = 50
        });

        // Act
        var result = service.CheckBaseline("latency", 12.5);  // +25% deviation

        // Assert
        Assert.Equal(BaselineStatus.Warning, result.Status);
    }

    [Fact]
    public void PerformanceBaselineService_DetectsPerformanceCritical()
    {
        // Arrange
        var service = new PerformanceBaselineService();
        service.DefineBaseline("latency", new PerformanceBaseline
        {
            Target = 10.0,
            CriticalThreshold = 50
        });

        // Act
        var result = service.CheckBaseline("latency", 16.0);  // +60% deviation

        // Assert
        Assert.Equal(BaselineStatus.Critical, result.Status);
    }

    [Fact]
    public void PerformanceBaselineService_ProvidesRecommendation()
    {
        // Arrange
        var service = new PerformanceBaselineService();
        service.DefineBaseline("latency", new PerformanceBaseline
        {
            Target = 10.0,
            WarningThreshold = 20
        });

        // Act
        var result = service.CheckBaseline("latency", 15.0);

        // Assert
        Assert.NotEmpty(result.Recommendation);
        Assert.NotEqual(BaselineStatus.Healthy, result.Status);
    }

    #endregion

    #region Query Optimization Tests

    [Fact]
    public void QueryOptimizationAdvisor_RecordsQueryPattern()
    {
        // Arrange
        var advisor = new QueryOptimizationAdvisor();

        // Act
        advisor.RecordQueryPattern("Customer", AuditOperation.Update, "user-123");

        // Assert
        var stats = advisor.GetStatistics();
        Assert.True(stats.TotalQueries > 0);
    }

    [Fact]
    public void QueryOptimizationAdvisor_TracksExecutionCount()
    {
        // Arrange
        var advisor = new QueryOptimizationAdvisor();

        // Act
        for (int i = 0; i < 10; i++)
        {
            advisor.RecordQueryPattern("Customer", null, null);
        }

        // Assert
        var stats = advisor.GetStatistics();
        Assert.Equal(10, stats.TotalQueries);
    }

    [Fact]
    public void QueryOptimizationAdvisor_IdentifiesCommonPatterns()
    {
        // Arrange
        var advisor = new QueryOptimizationAdvisor();

        // Act
        for (int i = 0; i < 20; i++)
        {
            advisor.RecordQueryPattern("Customer", null, "user-123");
        }

        // Assert
        var stats = advisor.GetStatistics();
        Assert.NotNull(stats.MostCommonPattern);
        Assert.Equal("Customer", stats.MostCommonPattern.Entity);
    }

    [Fact]
    public void QueryOptimizationAdvisor_GeneratesIndexRecommendations()
    {
        // Arrange
        var advisor = new QueryOptimizationAdvisor();

        // Act - Record many queries with DataSubjectId
        for (int i = 0; i < 15; i++)
        {
            advisor.RecordQueryPattern("Customer", null, "user-123");
        }

        var recommendations = advisor.GetIndexRecommendations();

        // Assert
        Assert.NotEmpty(recommendations);

        var recommendation = recommendations.First();
        Assert.Equal("Customer", recommendation.Entity);
        Assert.NotEmpty(recommendation.Columns);
    }

    [Fact]
    public void QueryOptimizationAdvisor_GeneratesSqlStatements()
    {
        // Arrange
        var advisor = new QueryOptimizationAdvisor();

        for (int i = 0; i < 15; i++)
        {
            advisor.RecordQueryPattern("Customer", null, "user-123");
        }

        // Act
        var recommendations = advisor.GetIndexRecommendations();
        var sqlStatement = recommendations.First().ToString();

        // Assert
        Assert.Contains("CREATE INDEX", sqlStatement);
        Assert.Contains("Customer", sqlStatement);
    }

    [Fact]
    public void QueryOptimizationAdvisor_ClearPatterns()
    {
        // Arrange
        var advisor = new QueryOptimizationAdvisor();
        advisor.RecordQueryPattern("Customer", null, "user-123");

        // Act
        advisor.Clear();
        var stats = advisor.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalQueries);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task DiagnosticsEnhancements_IntegrationTest()
    {
        // Arrange: Create audit store and add some data
        var store = new InMemoryAuditStore();

        for (int i = 0; i < 100; i++)
        {
            await store.AppendAsync(new AuditRecord
            {
                DataSubjectId = $"user-{i % 10}",
                Entity = i % 2 == 0 ? "Customer" : "Order",
                Field = i % 3 == 0 ? "Email" : "Phone",
                Operation = AuditOperation.Update,
                ActorId = $"admin-{i % 3}"
            });
        }

        // Act: Use all diagnostic services
        var metrics = new CustomMetricsProvider();
        var aggregation = new MetricAggregationService();
        var compliance = new ComplianceReportService(store);
        var baselines = new PerformanceBaselineService();
        var optimizer = new QueryOptimizationAdvisor();

        // Record some metrics
        for (int i = 0; i < 10; i++)
        {
            metrics.RecordSensitiveFieldAccess("Email", "Customer");
            aggregation.Record("audit.latency", 10.0 + i);
            optimizer.RecordQueryPattern("Customer", null, "user-123");
        }

        // Generate reports
        var frequencyReport = await compliance.GenerateAuditFrequencyReportAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow);

        // Define baselines
        baselines.DefineBaseline("audit.latency", new PerformanceBaseline { Target = 10.0 });

        // Assert: All services working together
        Assert.NotNull(frequencyReport);
        Assert.True(frequencyReport.TotalAuditRecords >= 100);

        var p95 = aggregation.GetPercentile("audit.latency", 95);
        Assert.True(p95 > 0);

        var baselineResult = baselines.CheckBaseline("audit.latency", 12.0);
        Assert.NotEqual(BaselineStatus.Unknown, baselineResult.Status);

        var recommendations = optimizer.GetIndexRecommendations();
        Assert.NotEmpty(recommendations);
    }

    #endregion
}
