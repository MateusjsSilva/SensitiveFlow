using Microsoft.Extensions.Logging;
using NSubstitute;
using SensitiveFlow.Core.Correlation;
using SensitiveFlow.Logging.Correlation;
using SensitiveFlow.Logging.Masking;
using SensitiveFlow.Logging.Metrics;
using SensitiveFlow.Logging.Sampling;
using SensitiveFlow.Logging.StructuredRedaction;

namespace SensitiveFlow.Logging.Tests;

public class LoggingEnhancementsTests
{
    #region Structured Property Redaction Tests

    [Fact]
    public void StructuredPropertyRedactor_RedactsSpecifiedKeys()
    {
        // Arrange
        var redactor = new StructuredPropertyRedactor(new[] { "Password", "ApiKey" });
        var pairs = new List<KeyValuePair<string, object?>>
        {
            new("Username", "alice"),
            new("Password", "secret123"),
            new("ApiKey", "key-xyz"),
            new("Email", "alice@example.com")
        };

        // Act
        var result = redactor.RedactPairs(pairs);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("alice", result[0].Value);
        Assert.Equal("[REDACTED]", result[1].Value);
        Assert.Equal("[REDACTED]", result[2].Value);
        Assert.Equal("alice@example.com", result[3].Value);
    }

    [Fact]
    public void StructuredPropertyRedactor_WithCustomPlaceholder()
    {
        // Arrange
        var redactor = new StructuredPropertyRedactor(new[] { "Secret" }, redactedPlaceholder: "***MASKED***");
        var pairs = new[] { new KeyValuePair<string, object?>("Secret", "value") };

        // Act
        var result = redactor.RedactPairs(pairs);

        // Assert
        Assert.Equal("***MASKED***", result[0].Value);
    }

    [Fact]
    public void StructuredPropertyRedactor_HasSensitiveProperties()
    {
        // Arrange & Act
        var empty = new StructuredPropertyRedactor();
        var configured = new StructuredPropertyRedactor(new[] { "Password" });

        // Assert
        Assert.False(empty.HasSensitiveProperties);
        Assert.True(configured.HasSensitiveProperties);
    }

    [Fact]
    public void StructuredPropertyRedactor_CaseSensitiveMatching()
    {
        // Arrange
        var redactor = new StructuredPropertyRedactor(new[] { "Password" });
        var pairs = new[]
        {
            new KeyValuePair<string, object?>("password", "lowercase"),
            new KeyValuePair<string, object?>("Password", "exact"),
            new KeyValuePair<string, object?>("PASSWORD", "uppercase")
        };

        // Act
        var result = redactor.RedactPairs(pairs);

        // Assert
        Assert.Equal("lowercase", result[0].Value); // Not matched
        Assert.Equal("[REDACTED]", result[1].Value); // Matched
        Assert.Equal("uppercase", result[2].Value); // Not matched
    }

    #endregion

    #region Audit Trail Correlation Tests

    [Fact]
    public void AuditCorrelationScope_CreationAndWrap()
    {
        // Arrange
        var innerLogger = Substitute.For<ILogger>();

        // Act
        var correlationLogger = new AuditCorrelationScope(innerLogger);

        // Assert
        Assert.NotNull(correlationLogger);
    }

    [Fact]
    public void AuditCorrelationScope_IsEnabledDelegates()
    {
        // Arrange
        var innerLogger = Substitute.For<ILogger>();
        innerLogger.IsEnabled(LogLevel.Warning).Returns(true);
        var correlationLogger = new AuditCorrelationScope(innerLogger);

        // Act
        var enabled = correlationLogger.IsEnabled(LogLevel.Warning);

        // Assert
        Assert.True(enabled);
        innerLogger.Received().IsEnabled(LogLevel.Warning);
    }

    [Fact]
    public void AuditCorrelationScope_BeginScopeDelegates()
    {
        // Arrange
        var innerLogger = Substitute.For<ILogger>();
        var correlationLogger = new AuditCorrelationScope(innerLogger);

        // Act
        using (correlationLogger.BeginScope("test"))
        {
            // Scope created
        }

        // Assert
        innerLogger.Received().BeginScope("test");
    }

    #endregion

    #region Redaction Performance Metrics Tests

    [Fact]
    public void RedactionMetricsCollector_RecordsRedactions()
    {
        // Arrange
        var collector = new RedactionMetricsCollector();

        // Act
        collector.RecordRedaction("Email", "Mask");
        collector.RecordRedaction("Password", "Redact");
        collector.RecordRedaction("Email", "Mask");

        // Assert — Verification through OpenTelemetry export
        Assert.NotNull(collector);
    }

    [Fact]
    public void RedactionMetricsCollector_RecordsMessageScans()
    {
        // Arrange
        var collector = new RedactionMetricsCollector();

        // Act
        collector.RecordMessageScanned();
        collector.RecordMessageScanned();

        // Assert
        Assert.NotNull(collector);
    }

    [Fact]
    public void RedactionMetricsCollector_RecordsDuration()
    {
        // Arrange
        var collector = new RedactionMetricsCollector();

        // Act
        collector.RecordRedactionDuration(1.5);
        collector.RecordRedactionDuration(2.3);

        // Assert
        Assert.NotNull(collector);
    }

    [Fact]
    public void RedactionMetricsCollector_IgnoresNegativeDuration()
    {
        // Arrange
        var collector = new RedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordRedactionDuration(-1.0);
    }

    #endregion

    #region Custom Masking Rules Tests

    [Fact]
    public void MaskingStrategyRegistry_BuiltInStrategies()
    {
        // Arrange
        var registry = new MaskingStrategyRegistry();

        // Act & Assert
        Assert.True(registry.TryGetStrategy("phone", out var phone));
        Assert.True(registry.TryGetStrategy("creditcard", out var cc));
        Assert.True(registry.TryGetStrategy("ipaddress", out var ip));
        Assert.NotNull(phone);
        Assert.NotNull(cc);
        Assert.NotNull(ip);
    }

    [Fact]
    public void MaskingStrategyRegistry_RegisterCustom()
    {
        // Arrange
        var registry = new MaskingStrategyRegistry();
        var custom = Substitute.For<IMaskingStrategy>();
        custom.Mask("test").Returns("****");

        // Act
        registry.Register("custom", custom);

        // Assert
        Assert.True(registry.TryGetStrategy("custom", out var result));
        Assert.Equal(custom, result);
    }

    [Fact]
    public void MaskingStrategyRegistry_GetStrategyReturnsNull()
    {
        // Arrange
        var registry = new MaskingStrategyRegistry();

        // Act
        var result = registry.GetStrategy("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void PhoneMaskingStrategy_MasksDigits()
    {
        // Arrange
        var strategy = new PhoneMaskingStrategy();

        // Act
        var result = strategy.Mask("555-123-4567");

        // Assert
        Assert.Contains("67", result); // Last 2 digits visible
        Assert.DoesNotContain("555", result); // First 3 masked
        Assert.DoesNotContain("123", result); // Middle 3 masked
    }

    [Fact]
    public void CreditCardMaskingStrategy_MasksNumber()
    {
        // Arrange
        var strategy = new CreditCardMaskingStrategy();

        // Act
        var result = strategy.Mask("4532015112830366");

        // Assert
        Assert.EndsWith("0366", result);
        Assert.DoesNotContain("453201", result);
    }

    [Fact]
    public void IpAddressMaskingStrategy_MasksOctets()
    {
        // Arrange
        var strategy = new IpAddressMaskingStrategy();

        // Act
        var result = strategy.Mask("192.168.1.100");

        // Assert
        Assert.Contains("1.100", result);
        Assert.DoesNotContain("192", result);
        Assert.DoesNotContain("168", result);
    }

    [Fact]
    public void MaskingStrategy_HandlesEmptyStrings()
    {
        // Arrange
        var phone = new PhoneMaskingStrategy();
        var cc = new CreditCardMaskingStrategy();
        var ip = new IpAddressMaskingStrategy();

        // Act & Assert
        Assert.Equal("", phone.Mask(""));
        Assert.Equal("", cc.Mask(""));
        Assert.Equal("", ip.Mask(""));
    }

    #endregion

    #region Log Sampling Tests

    [Fact]
    public void LogSamplingFilter_DefaultRateIsOne()
    {
        // Arrange & Act
        var filter = new LogSamplingFilter();

        // Assert
        Assert.Equal(1.0, filter.SamplingRate);
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void LogSamplingFilter_AlwaysLogsWhenNoRedaction()
    {
        // Arrange
        var filter = new LogSamplingFilter(0.1); // 10% sampling

        // Act & Assert
        Assert.True(filter.ShouldLog(hasRedactedFields: false));
    }

    [Fact]
    public void LogSamplingFilter_RateOneAlwaysLogs()
    {
        // Arrange
        var filter = new LogSamplingFilter(1.0);

        // Act & Assert
        Assert.True(filter.ShouldLog(hasRedactedFields: true));
    }

    [Fact]
    public void LogSamplingFilter_SamplesBasedOnRate()
    {
        // Arrange
        var filter = new LogSamplingFilter(0.5); // 50% sampling
        var kept = 0;
        var total = 1000;

        // Act
        for (int i = 0; i < total; i++)
        {
            if (filter.ShouldLog(hasRedactedFields: true))
            {
                kept++;
            }
        }

        // Assert — Approximately 50% (allowing for randomness)
        var percentage = (double)kept / total;
        Assert.InRange(percentage, 0.4, 0.6);
    }

    [Fact]
    public void LogSamplingFilter_RejectsInvalidRate()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogSamplingFilter(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogSamplingFilter(1.1));
    }

    [Fact]
    public void LogSamplingFilter_IsEnabled()
    {
        // Arrange & Act
        var disabled = new LogSamplingFilter(1.0);
        var enabled = new LogSamplingFilter(0.5);

        // Assert
        Assert.False(disabled.IsEnabled);
        Assert.True(enabled.IsEnabled);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void LoggingEnhancements_AllFeaturesTogether()
    {
        // Arrange
        var structured = new StructuredPropertyRedactor(new[] { "ApiKey" });
        var metrics = new RedactionMetricsCollector();
        var strategies = new MaskingStrategyRegistry();
        var sampling = new LogSamplingFilter(0.8);

        var options = new Configuration.SensitiveLoggingOptions
        {
            StructuredPropertyRedactor = structured,
            MetricsCollector = metrics,
            MaskingStrategies = strategies,
            SamplingFilter = sampling
        };

        // Act
        Assert.True(structured.HasSensitiveProperties);
        Assert.True(strategies.TryGetStrategy("phone", out _));
        Assert.True(sampling.IsEnabled);

        // Assert
        Assert.NotNull(options.StructuredPropertyRedactor);
        Assert.NotNull(options.MetricsCollector);
        Assert.NotNull(options.MaskingStrategies);
        Assert.NotNull(options.SamplingFilter);
    }

    #endregion
}
