using FluentAssertions;
using Newtonsoft.Json;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Json.Newtonsoft;

namespace SensitiveFlow.Json.Tests;

public sealed class SensitiveDataNewtonsoftConverterTests
{
    private class TestModel
    {
        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Identification)]
        public string Name { get; set; } = string.Empty;

        public string PublicField { get; set; } = string.Empty;
    }

    [Fact]
    public void Converter_RedactsPersonalDataByDefault()
    {
        var model = new TestModel
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            PublicField = "PublicValue"
        };

        var settings = new JsonSerializerSettings();
        settings.AddSensitiveDataRedaction();
        
        var json = JsonConvert.SerializeObject(model, settings);

        json.Should().Contain("[REDACTED]");
        json.Should().NotContain("alice@example.com");
        json.Should().NotContain("Alice Smith");
        json.Should().Contain("PublicValue");
    }

    [Fact]
    public void Converter_SupportsMaskingMode()
    {
        var model = new TestModel
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            PublicField = "PublicValue"
        };

        var settings = new JsonSerializerSettings();
        settings.AddSensitiveDataRedaction(OutputRedactionAction.Mask);

        var json = JsonConvert.SerializeObject(model, settings);

        json.Should().NotContain("alice@example.com");
        json.Should().NotContain("Alice Smith");
        json.Should().Contain("PublicValue");
        // Masked values should be partial (first char + asterisks)
        json.Should().Contain("a*");
    }

    [Fact]
    public void CreateWithSensitiveDataRedaction_CreatesConfiguredSettings()
    {
        var settings = SensitiveFlowNewtonsoftExtensions.CreateWithSensitiveDataRedaction();

        var model = new TestModel
        {
            Email = "alice@example.com",
            Name = "Alice Smith",
            PublicField = "PublicValue"
        };

        var json = JsonConvert.SerializeObject(model, settings);

        json.Should().Contain("[REDACTED]");
        json.Should().NotContain("alice@example.com");
    }

    [Fact]
    public void Converter_HandlesNullValues()
    {
        var model = new TestModel
        {
            Email = "",
            Name = "",
            PublicField = "PublicValue"
        };

        var settings = new JsonSerializerSettings();
        settings.AddSensitiveDataRedaction();

        var json = JsonConvert.SerializeObject(model, settings);

        json.BeValidJson();
        json.Should().Contain("PublicValue");
    }

    [Fact]
    public void Converter_CanConvert_RefereceTypes()
    {
        var converter = new SensitiveDataNewtonsoftConverter();

        converter.CanConvert(typeof(TestModel)).Should().BeTrue();
        converter.CanConvert(typeof(string)).Should().BeFalse();
        converter.CanConvert(typeof(int)).Should().BeFalse();
    }

    [Fact]
    public void Converter_PreservesPublicFields()
    {
        var model = new TestModel
        {
            Email = "secret@example.com",
            Name = "SecretName",
            PublicField = "PublicValue"
        };

        var settings = new JsonSerializerSettings();
        settings.AddSensitiveDataRedaction();

        var json = JsonConvert.SerializeObject(model, settings);
        var parsed = JsonConvert.DeserializeObject<TestModel>(json);

        parsed.Should().NotBeNull();
        parsed.PublicField.Should().Be("PublicValue");
    }
}

public sealed class SensitiveFlowNewtonsoftExtensionsTests
{
    private class TestModel
    {
        [SensitiveData(Category = SensitiveDataCategory.Financial)]
        public string CreditCard { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }

    [Fact]
    public void AddSensitiveDataRedaction_AppliesRedactionToSettings()
    {
        var settings = new JsonSerializerSettings();
        var result = settings.AddSensitiveDataRedaction();

        result.Should().Be(settings); // Fluent API
        settings.Converters.Should().NotBeEmpty();
        settings.Converters.Should().Contain(c => c is SensitiveDataNewtonsoftConverter);
    }

    [Fact]
    public void AddSensitiveDataRedaction_AllowsConfigurableAction()
    {
        var settings1 = new JsonSerializerSettings();
        settings1.AddSensitiveDataRedaction(OutputRedactionAction.Redact);

        var settings2 = new JsonSerializerSettings();
        settings2.AddSensitiveDataRedaction(OutputRedactionAction.Mask);

        settings1.Converters.Should().HaveCount(1);
        settings2.Converters.Should().HaveCount(1);
    }

    [Fact]
    public void CreateWithSensitiveDataRedaction_CreatesFreshSettings()
    {
        var settings = SensitiveFlowNewtonsoftExtensions.CreateWithSensitiveDataRedaction();

        settings.Should().NotBeNull();
        settings.Converters.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateWithSensitiveDataRedaction_WithAction_AppliesAction()
    {
        var settings = SensitiveFlowNewtonsoftExtensions.CreateWithSensitiveDataRedaction(
            OutputRedactionAction.Mask);

        var model = new TestModel
        {
            CreditCard = "4532123456789123",
            Description = "Test"
        };

        var json = JsonConvert.SerializeObject(model, settings);

        json.Should().NotContain("4532123456789123");
        json.Should().Contain("Test");
    }
}

/// <summary>
/// Helper extensions for test assertions
/// </summary>
internal static class TestExtensions
{
    internal static void BeValidJson(this string json)
    {
        var action = () => JsonConvert.DeserializeObject(json);
        action.Should().NotThrow();
    }
}
