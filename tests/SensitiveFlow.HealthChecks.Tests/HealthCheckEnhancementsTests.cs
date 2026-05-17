using FluentAssertions;
using SensitiveFlow.HealthChecks.Alerting;
using SensitiveFlow.HealthChecks.AuditTracking;
using SensitiveFlow.HealthChecks.DataQuality;
using SensitiveFlow.HealthChecks.PerformanceMetrics;
using SensitiveFlow.HealthChecks.PolicyValidation;
using Xunit;

namespace SensitiveFlow.HealthChecks.Tests;

public class HealthCheckEnhancementsTests
{
    // Policy Validation Tests
    [Fact]
    public void RetentionPolicyValidator_ValidateEmptyPolicies_ReturnsFalse()
    {
        var validator = new RetentionPolicyValidator();

        var result = validator.Validate();

        result.IsValid.Should().BeFalse();
        result.PolicyCount.Should().Be(0);
        result.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public void RetentionPolicyValidator_ValidateWithPolicies_ReturnsTrue()
    {
        var validator = new RetentionPolicyValidator();
        validator.AddPolicy("TestCategory", 30);

        var result = validator.Validate();

        result.IsValid.Should().BeTrue();
        result.PolicyCount.Should().Be(1);
    }

    [Fact]
    public void RetentionPolicyValidator_GetPolicyCount_ReturnsCorrectCount()
    {
        var validator = new RetentionPolicyValidator();
        validator.AddPolicy("Cat1", 30);
        validator.AddPolicy("Cat2", 60);

        var count = validator.GetPolicyCount();

        count.Should().Be(2);
    }

    [Fact]
    public void RetentionPolicyValidator_HasPolicyForCategory_ReturnsTrueWhenExists()
    {
        var validator = new RetentionPolicyValidator();
        validator.AddPolicy("TestCategory", 30);

        validator.HasPolicyForCategory("TestCategory").Should().BeTrue();
        validator.HasPolicyForCategory("NonExistent").Should().BeFalse();
    }

    // Performance Metrics Tests
    [Fact]
    public void HealthCheckPerformanceCollector_RecordHealthCheck_TracksMetrics()
    {
        var collector = new HealthCheckPerformanceCollector();
        collector.RecordHealthCheck("Audit", 100, success: true);

        var metric = collector.GetMetric("Audit");

        metric.Should().NotBeNull();
        metric!.Count.Should().Be(1);
        metric.TotalTimeMs.Should().Be(100);
        metric.Failures.Should().Be(0);
    }

    [Fact]
    public void HealthCheckPerformanceCollector_RecordMultipleChecks_AggregatesMetrics()
    {
        var collector = new HealthCheckPerformanceCollector();
        collector.RecordHealthCheck("Audit", 50, success: true);
        collector.RecordHealthCheck("Audit", 75, success: false);

        var metric = collector.GetMetric("Audit");

        metric!.Count.Should().Be(2);
        metric.TotalTimeMs.Should().Be(125);
        metric.Failures.Should().Be(1);
    }

    [Fact]
    public void HealthCheckPerformanceCollector_RecordAuditOperation_TracksRecords()
    {
        var collector = new HealthCheckPerformanceCollector();
        collector.RecordAuditOperation("Insert", 1000, 500);

        var metric = collector.GetMetric("Audit.Insert");

        metric.Should().NotBeNull();
        metric!.RecordCount.Should().Be(1000);
    }

    [Fact]
    public void HealthCheckPerformanceCollector_GetAverageLatencyMs_CalculatesCorrectly()
    {
        var collector = new HealthCheckPerformanceCollector();
        collector.RecordHealthCheck("Check1", 100, success: true);
        collector.RecordHealthCheck("Check2", 200, success: true);

        var avgLatency = collector.GetAverageLatencyMs();

        avgLatency.Should().Be(150);
    }

    [Fact]
    public void HealthCheckPerformanceCollector_GetAuditThroughputRecordsPerSec_CalculatesThroughput()
    {
        var collector = new HealthCheckPerformanceCollector();
        collector.RecordAuditOperation("Insert", 1000, 1000); // 1000 records in 1000ms = 1000/s

        var throughput = collector.GetAuditThroughputRecordsPerSec();

        throughput.Should().Be(1000);
    }

    [Fact]
    public void HealthCheckPerformanceCollector_GetSuccessRate_CalculatesCorrectly()
    {
        var collector = new HealthCheckPerformanceCollector();
        collector.RecordHealthCheck("Check", 50, success: true);
        collector.RecordHealthCheck("Check", 50, success: false);

        var successRate = collector.GetSuccessRate();

        successRate.Should().Be(50);
    }

    [Fact]
    public void HealthCheckPerformanceCollector_GetSlowChecks_FiltersAboveThreshold()
    {
        var collector = new HealthCheckPerformanceCollector();
        collector.RecordHealthCheck("Fast", 10, success: true);
        collector.RecordHealthCheck("Slow", 200, success: true);

        var slowChecks = collector.GetSlowChecks(100).ToList();

        slowChecks.Should().HaveCount(1);
        slowChecks[0].CheckName.Should().Be("Slow");
    }

    // Alerting Tests
    [Fact]
    public void HealthAlertingPolicy_AddRule_RegistersRule()
    {
        var policy = new HealthAlertingPolicy();
        policy.AddRule("Audit", AlertSeverity.Critical, "http://webhook");

        var rule = policy.FindRule("Audit");

        rule.Should().NotBeNull();
        rule!.CheckName.Should().Be("Audit");
        rule.Severity.Should().Be(AlertSeverity.Critical);
    }

    [Fact]
    public void HealthAlertingPolicy_FindRule_ReturnsNullForUnknownCheck()
    {
        var policy = new HealthAlertingPolicy();

        var rule = policy.FindRule("Unknown");

        rule.Should().BeNull();
    }

    [Fact]
    public void HealthAlertingPolicy_GetRulesBySeverity_FiltersCorrectly()
    {
        var policy = new HealthAlertingPolicy();
        policy.AddRule("Check1", AlertSeverity.Critical);
        policy.AddRule("Check2", AlertSeverity.Warning);
        policy.AddRule("Check3", AlertSeverity.Critical);

        var criticalRules = policy.GetRulesBySeverity(AlertSeverity.Critical).ToList();

        criticalRules.Should().HaveCount(2);
    }

    [Fact]
    public void HealthAlertingPolicy_RemoveRule_DeletesRule()
    {
        var policy = new HealthAlertingPolicy();
        policy.AddRule("Test", AlertSeverity.Warning);

        var removed = policy.RemoveRule("Test");

        removed.Should().BeTrue();
        policy.FindRule("Test").Should().BeNull();
    }

    [Fact]
    public void AlertingRule_HasNotificationConfigured_ReturnsTrueWhenWebhook()
    {
        var rule = new AlertingRule { WebhookUrl = "http://example.com" };

        rule.HasNotificationConfigured.Should().BeTrue();
    }

    // Audit Age Tracking Tests
    [Fact]
    public void AuditAgeTracker_AnalyzeRecentRecord_ReturnsHealthy()
    {
        var tracker = new AuditAgeTracker();
        var recent = DateTime.UtcNow.AddDays(-5);

        var analysis = tracker.Analyze(recent);

        analysis.Status.Should().Be(AuditAgeStatus.Healthy);
        analysis.AgeInDays.Should().BeLessThan(10);
    }

    [Fact]
    public void AuditAgeTracker_AnalyzeOldRecord_ReturnsWarning()
    {
        var tracker = new AuditAgeTracker { WarningDaysThreshold = 30 };
        var old = DateTime.UtcNow.AddDays(-50);

        var analysis = tracker.Analyze(old);

        analysis.Status.Should().Be(AuditAgeStatus.Warning);
    }

    [Fact]
    public void AuditAgeTracker_AnalyzeVeryOldRecord_ReturnsCritical()
    {
        var tracker = new AuditAgeTracker { CriticalDaysThreshold = 90 };
        var veryOld = DateTime.UtcNow.AddDays(-100);

        var analysis = tracker.Analyze(veryOld);

        analysis.Status.Should().Be(AuditAgeStatus.Critical);
    }

    [Fact]
    public void AuditAgeTracker_AnalyzeByCategory_AggregatesMultipleCategories()
    {
        var tracker = new AuditAgeTracker();
        var categories = new Dictionary<string, DateTime>
        {
            ["Users"] = DateTime.UtcNow.AddDays(-10),
            ["Orders"] = DateTime.UtcNow.AddDays(-20)
        };

        var analysis = tracker.AnalyzeByCategory(categories);

        analysis.CategoryAges.Should().HaveCount(2);
        analysis.Message.Should().Contain("Categories:");
    }

    [Fact]
    public void AuditAgeTracker_GetRecommendations_ReturnsSuggestionsForCritical()
    {
        var tracker = new AuditAgeTracker();
        var analysis = new AuditAgeAnalysis { Status = AuditAgeStatus.Critical };

        var recommendations = tracker.GetRecommendations(analysis);

        recommendations.Should().Contain(r => r.Contains("archival"));
        recommendations.Should().Contain(r => r.Contains("URGENT"));
    }

    [Fact]
    public void AuditAgeTracker_SetWarningThreshold_ValidatesPositive()
    {
        var tracker = new AuditAgeTracker();

        var action = () => tracker.WarningDaysThreshold = -1;

        action.Should().Throw<ArgumentException>();
    }

    // Data Quality Tests
    [Fact]
    public void DataQualityChecker_CheckForMissingFields_ReturnsHealthyByDefault()
    {
        var checker = new DataQualityChecker();

        var result = checker.CheckForMissingFields("TestEntity", new[] { "Field1" });

        result.Should().NotBeNull();
        result.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void DataQualityChecker_CheckForDuplicates_ReturnsHealthyByDefault()
    {
        var checker = new DataQualityChecker();

        var result = checker.CheckForDuplicates("TestEntity", new[] { "Id" });

        result.Should().NotBeNull();
        result.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void DataQualityChecker_ValidateEntityConfiguration_ReturnsHealthy()
    {
        var checker = new DataQualityChecker();

        var result = checker.ValidateEntityConfiguration("TestEntity", 5);

        result.IsHealthy.Should().BeTrue();
        result.EntityName.Should().Be("TestEntity");
    }

    [Fact]
    public void DataQualityResult_IssuesFound_TracksProblemCount()
    {
        var result = new DataQualityResult
        {
            EntityName = "Test",
            IsHealthy = false,
            IssuesFound = 3,
            Issues = new[] { "Issue1", "Issue2", "Issue3" }
        };

        result.Issues.Should().HaveCount(3);
        result.IssuesFound.Should().Be(3);
    }
}
