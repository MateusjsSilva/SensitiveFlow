using System.Security.Claims;
using SensitiveFlow.Core;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Lazy;
using SensitiveFlow.Json.Masking;
using SensitiveFlow.Json.Metrics;
using SensitiveFlow.Json.OpenApi;
using SensitiveFlow.Json.Roles;

namespace SensitiveFlow.Json.Tests;

public class JsonEnhancementsTests
{
    #region Redaction Context Resolver Tests

    [Fact]
    public void IRedactionContextResolver_Interface_Exists()
    {
        var resolver = new TestRedactionContextResolver();
        Assert.NotNull(resolver);
    }

    [Fact]
    public void ClaimsPrincipalRedactionContextResolver_AdminRole_ReturnsAdminView()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Role, "Admin") };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new ClaimsPrincipalRedactionContextResolver(principal);

        // Act
        var context = resolver.ResolveContext();

        // Assert
        Assert.Equal(RedactionContext.AdminView, context);
    }

    [Fact]
    public void ClaimsPrincipalRedactionContextResolver_SupportRole_ReturnsSupportView()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Role, "Support") };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new ClaimsPrincipalRedactionContextResolver(principal);

        // Act
        var context = resolver.ResolveContext();

        // Assert
        Assert.Equal(RedactionContext.SupportView, context);
    }

    [Fact]
    public void ClaimsPrincipalRedactionContextResolver_CustomerRole_ReturnsCustomerView()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Role, "Customer") };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new ClaimsPrincipalRedactionContextResolver(principal);

        // Act
        var context = resolver.ResolveContext();

        // Assert
        Assert.Equal(RedactionContext.CustomerView, context);
    }

    [Fact]
    public void ClaimsPrincipalRedactionContextResolver_NoMatchingRole_ReturnsDefault()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Role, "Guest") };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new ClaimsPrincipalRedactionContextResolver(principal);

        // Act
        var context = resolver.ResolveContext();

        // Assert
        Assert.Equal(RedactionContext.ApiResponse, context);
    }

    [Fact]
    public void ClaimsPrincipalRedactionContextResolver_NullPrincipal_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ClaimsPrincipalRedactionContextResolver(null!));
    }

    #endregion

    #region JSON Masking Strategy Tests

    [Fact]
    public void JsonMaskingStrategyRegistry_HasEmailStrategy()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result = registry.TryGetStrategy("email", out var strategy);

        // Assert
        Assert.True(result);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_HasPhoneStrategy()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result = registry.TryGetStrategy("phone", out var strategy);

        // Assert
        Assert.True(result);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_HasCreditCardStrategy()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result = registry.TryGetStrategy("creditcard", out var strategy);

        // Assert
        Assert.True(result);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_HasSsnStrategy()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result = registry.TryGetStrategy("ssn", out var strategy);

        // Assert
        Assert.True(result);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_HasIpAddressStrategy()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result = registry.TryGetStrategy("ipaddress", out var strategy);

        // Assert
        Assert.True(result);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_MasksEmail()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var strategy = registry.GetStrategy("email");

        // Act
        var result = strategy?.Mask("john@example.com");

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("john", result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_MasksPhone()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var strategy = registry.GetStrategy("phone");

        // Act
        var result = strategy?.Mask("555-123-4567");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("*", result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_MasksCreditCard()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var strategy = registry.GetStrategy("creditcard");

        // Act
        var result = strategy?.Mask("4532015112830366");

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith("0366", result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_MasksSSN()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var strategy = registry.GetStrategy("ssn");

        // Act
        var result = strategy?.Mask("123-45-6789");

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith("6789", result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_MasksIpAddress()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var strategy = registry.GetStrategy("ipaddress");

        // Act
        var result = strategy?.Mask("192.168.1.100");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("*", result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_RegisterCustom()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var custom = new TestMaskingStrategy();

        // Act
        registry.Register("custom", custom);

        // Assert
        Assert.True(registry.TryGetStrategy("custom", out var result));
        Assert.Equal(custom, result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_GetNonexistentStrategy_ReturnsNull()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result = registry.GetStrategy("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_NullStrategyName_ReturnsNull()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result = registry.GetStrategy(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_CaseInsensitiveStrategyLookup()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();

        // Act
        var result1 = registry.TryGetStrategy("EMAIL", out _);
        var result2 = registry.TryGetStrategy("Email", out _);
        var result3 = registry.TryGetStrategy("email", out _);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_StrategyHandlesEmptyString()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var strategy = registry.GetStrategy("email");

        // Act
        var result = strategy?.Mask("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void JsonMaskingStrategyRegistry_StrategyHandlesNull()
    {
        // Arrange
        var registry = new JsonMaskingStrategyRegistry();
        var strategy = registry.GetStrategy("email");

        // Act
        var result = strategy?.Mask(null);

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region Lazy Redaction Tests

    [Fact]
    public void LazyRedactionWrapper_CreatesWithValue()
    {
        // Arrange & Act
        var wrapper = new LazyRedactionWrapper<string>("secret");

        // Assert
        Assert.NotNull(wrapper);
        Assert.Equal("secret", wrapper.OriginalValue);
    }

    [Fact]
    public void LazyRedactionWrapper_CreatesWithNullValue()
    {
        // Arrange & Act
        var wrapper = new LazyRedactionWrapper<string>(null);

        // Assert
        Assert.Null(wrapper.OriginalValue);
    }

    [Fact]
    public void LazyRedactionWrapper_InitiallyNotResolved()
    {
        // Arrange & Act
        var wrapper = new LazyRedactionWrapper<string>("secret");

        // Assert
        Assert.False(wrapper.IsResolved);
    }

    [Fact]
    public void LazyRedactionWrapper_ToStringResolvesRedaction()
    {
        // Arrange
        var wrapper = new LazyRedactionWrapper<string>("secret", v => v?.ToUpper() ?? string.Empty);

        // Act
        var result = wrapper.ToString();

        // Assert
        Assert.Equal("SECRET", result);
        Assert.True(wrapper.IsResolved);
    }

    [Fact]
    public void LazyRedactionWrapper_CachesRedactedValue()
    {
        // Arrange
        var callCount = 0;
        var wrapper = new LazyRedactionWrapper<string>("test", v =>
        {
            callCount++;
            return v?.ToUpper()!;
        });

        // Act
        var result1 = wrapper.ToString();
        var result2 = wrapper.ToString();

        // Assert
        Assert.Equal("TEST", result1);
        Assert.Equal("TEST", result2);
        Assert.Equal(1, callCount); // Only called once
    }

    [Fact]
    public void LazyRedactionWrapper_NoRedactionFunc_UsesToString()
    {
        // Arrange
        var value = new TestObjectWithToString("value");
        var wrapper = new LazyRedactionWrapper<TestObjectWithToString>(value);

        // Act
        var result = wrapper.ToString();

        // Assert
        Assert.Equal("TestValue:value", result);
    }

    [Fact]
    public void LazyRedactionWrapper_WithNullValue_ReturnEmpty()
    {
        // Arrange
        var wrapper = new LazyRedactionWrapper<string?>(null);

        // Act
        var result = wrapper.ToString();

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region OpenAPI Schema Filter Tests

    [Fact]
    public void SensitiveDataSchemaFilter_GetRedactedProperties_ReturnsCollection()
    {
        // Arrange & Act
        var result = SensitiveDataSchemaFilter.GetRedactedProperties(typeof(TestClassNoSensitive));

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void SensitiveDataSchemaFilter_GetSensitiveProperties_ReturnsCollection()
    {
        // Arrange & Act
        var result = SensitiveDataSchemaFilter.GetSensitiveProperties(typeof(TestClassWithPersonalData));

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void SensitiveDataSchemaFilter_GetOmittedProperties_ReturnsCollection()
    {
        // Arrange & Act
        var result = SensitiveDataSchemaFilter.GetOmittedProperties(typeof(TestClassWithOmit));

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void SensitiveDataSchemaFilter_NullType_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SensitiveDataSchemaFilter.GetRedactedProperties(null!));
        Assert.Throws<ArgumentNullException>(() => SensitiveDataSchemaFilter.GetSensitiveProperties(null!));
        Assert.Throws<ArgumentNullException>(() => SensitiveDataSchemaFilter.GetOmittedProperties(null!));
    }

    #endregion

    #region JSON Redaction Metrics Tests

    [Fact]
    public void JsonRedactionMetricsCollector_CreatesInstance()
    {
        // Arrange & Act
        var collector = new JsonRedactionMetricsCollector();

        // Assert
        Assert.NotNull(collector);
    }

    [Fact]
    public void JsonRedactionMetricsCollector_RecordsRedaction()
    {
        // Arrange
        var collector = new JsonRedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordRedaction("Email", OutputRedactionAction.Mask);
    }

    [Fact]
    public void JsonRedactionMetricsCollector_RecordsPropertySerialized()
    {
        // Arrange
        var collector = new JsonRedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordPropertySerialized();
    }

    [Fact]
    public void JsonRedactionMetricsCollector_RecordsDuration()
    {
        // Arrange
        var collector = new JsonRedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordRedactionDuration(1.5);
    }

    [Fact]
    public void JsonRedactionMetricsCollector_IgnoresNegativeDuration()
    {
        // Arrange
        var collector = new JsonRedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordRedactionDuration(-1.0);
    }

    [Fact]
    public void JsonRedactionMetricsCollector_IgnoresNullPropertyName()
    {
        // Arrange
        var collector = new JsonRedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordRedaction(null!, OutputRedactionAction.Mask);
    }

    [Fact]
    public void JsonRedactionMetricsCollector_IgnoresEmptyPropertyName()
    {
        // Arrange
        var collector = new JsonRedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordRedaction("", OutputRedactionAction.Mask);
    }

    [Fact]
    public void JsonRedactionMetricsCollector_RecordsWithAllContexts()
    {
        // Arrange
        var collector = new JsonRedactionMetricsCollector();

        // Act & Assert — Should not throw
        collector.RecordRedaction("Email", OutputRedactionAction.Mask, RedactionContext.ApiResponse);
        collector.RecordRedaction("Email", OutputRedactionAction.Mask, RedactionContext.AdminView);
        collector.RecordRedaction("Email", OutputRedactionAction.Mask, RedactionContext.SupportView);
        collector.RecordRedaction("Email", OutputRedactionAction.Mask, RedactionContext.CustomerView);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void JsonEnhancements_AllFeaturesIntegrate()
    {
        // Arrange
        var resolver = new TestRedactionContextResolver();
        var strategies = new JsonMaskingStrategyRegistry();
        var metrics = new JsonRedactionMetricsCollector();

        var options = new JsonRedactionOptions
        {
            ContextResolver = resolver,
            MaskingStrategies = strategies,
            EnableLazyRedaction = true,
            MetricsCollector = metrics
        };

        // Act
        var context = resolver.ResolveContext();
        var emailStrategy = strategies.GetStrategy("email");
        var masked = emailStrategy?.Mask("test@example.com");

        metrics.RecordRedaction("Email", OutputRedactionAction.Mask);
        var wrapper = new LazyRedactionWrapper<string>("secret");

        // Assert
        Assert.Equal(RedactionContext.ApiResponse, context);
        Assert.NotNull(emailStrategy);
        Assert.NotNull(masked);
        Assert.True(options.EnableLazyRedaction);
    }

    #endregion

    #region Test Helpers

    private sealed class TestRedactionContextResolver : IRedactionContextResolver
    {
        public RedactionContext ResolveContext() => RedactionContext.ApiResponse;
    }

    private sealed class TestMaskingStrategy : IJsonMaskingStrategy
    {
        public string Mask(string? value) => "***";
    }

    private sealed class TestObjectWithToString
    {
        private readonly string _value;

        public TestObjectWithToString(string value) => _value = value;

        public override string ToString() => $"TestValue:{_value}";
    }

    private class TestClassNoSensitive
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private class TestClassWithPersonalData
    {
        [PersonalData]
        public string Email { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private class TestClassWithSensitiveData
    {
        [SensitiveData]
        public string Secret { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private class TestClassWithOmit
    {
        [SensitiveData]
        [Redaction(ApiResponse = OutputRedactionAction.Omit)]
        public string Password { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
