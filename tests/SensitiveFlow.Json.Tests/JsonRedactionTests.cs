using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Json.Attributes;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Json.Extensions;

namespace SensitiveFlow.Json.Tests;

public sealed class JsonRedactionTests
{
    private static JsonSerializerOptions Build(JsonRedactionOptions? options = null)
        => new JsonSerializerOptions().WithSensitiveDataRedaction(options);

    [Fact]
    public void DefaultMode_Mask_PartiallyMasksKnownPatterns()
    {
        var json = JsonSerializer.Serialize(new Customer
        {
            Id = 1,
            Name = "João da Silva",
            Email = "joao@example.com",
            PublicNote = "ok",
        }, Build());

        json.Should().NotContain("João da Silva");
        json.Should().NotContain("joao@example.com");
        json.Should().Contain("\"PublicNote\":\"ok\"");
    }

    [Fact]
    public void Mode_Redacted_ReplacesWithPlaceholder()
    {
        var json = JsonSerializer.Serialize(new Customer
        {
            Id = 1,
            Name = "anyone",
            Email = "any@x.com",
        }, Build(new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Redacted }));

        json.Should().Contain("\"Name\":\"[REDACTED]\"");
        json.Should().Contain("\"Email\":\"[REDACTED]\"");
    }

    [Fact]
    public void Mode_Omit_RemovesPropertyFromOutput()
    {
        var json = JsonSerializer.Serialize(new Customer
        {
            Id = 1,
            Name = "anyone",
            Email = "any@x.com",
        }, Build(new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Omit }));

        json.Should().NotContain("Name");
        json.Should().NotContain("Email");
        json.Should().Contain("\"Id\":1");
    }

    [Fact]
    public void PerPropertyOverride_TakesPrecedenceOverGlobalDefault()
    {
        var json = JsonSerializer.Serialize(new MixedRedaction
        {
            Email = "secret@x.com",
            TaxId = "12345678900",
        }, Build(new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask }));

        // Email overrides to None — appears verbatim
        json.Should().Contain("\"Email\":\"secret@x.com\"");
        // TaxId overrides to Omit
        json.Should().NotContain("TaxId");
    }

    [Fact]
    public void NonAnnotatedProperty_IsUntouched()
    {
        var json = JsonSerializer.Serialize(new Customer
        {
            Id = 42,
            Name = "n",
            Email = "e@x.com",
            PublicNote = "hello world",
        }, Build());

        json.Should().Contain("\"Id\":42");
        json.Should().Contain("\"PublicNote\":\"hello world\"");
    }

    [Fact]
    public void Mode_Mask_HandlesPhoneNameGenericSingleCharacterEmptyAndNullValues()
    {
        var json = JsonSerializer.Serialize(new ExtendedCustomer
        {
            Phone = "+55 11 99999-8877",
            NickName = "Ana Maria",
            TaxId = "12345678900",
            OneCharacterSecret = "x",
            EmptySecret = string.Empty,
            NullSecret = null,
        }, Build());

        json.Should().NotContain("+55 11 99999-8877");
        json.Should().Contain("\"Phone\":\"\\u002B** ** *****-**77\"");
        json.Should().Contain("\"NickName\":\"A** M****\"");
        json.Should().Contain("\"TaxId\":\"1**********\"");
        json.Should().Contain("\"OneCharacterSecret\":\"*\"");
        json.Should().Contain("\"EmptySecret\":\"\"");
        json.Should().Contain("\"NullSecret\":null");
    }

    [Fact]
    public void Mode_Mask_ReplacesNonStringSensitiveValuesWithPlaceholder()
    {
        var json = JsonSerializer.Serialize(new NumericSensitiveData
        {
            Score = 42,
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Mask,
            RedactedPlaceholder = "***",
        }));

        json.Should().Contain("\"Score\":0");
        json.Should().NotContain("42");
    }

    [Fact]
    public void Mode_Redacted_UsesCustomPlaceholder()
    {
        var json = JsonSerializer.Serialize(new Customer
        {
            Name = "Alice",
            Email = "alice@example.com",
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Redacted,
            RedactedPlaceholder = "***",
        }));

        json.Should().Contain("\"Name\":\"***\"");
        json.Should().Contain("\"Email\":\"***\"");
    }

    [Fact]
    public void WithSensitiveDataRedaction_AllowsNullOptions()
    {
        Action act = () => JsonSerializer.Serialize(new Customer(), Build(null));

        act.Should().NotThrow();
    }

    [Fact]
    public void WithSensitiveDataRedaction_RejectsNullSerializerOptions()
    {
        var act = () => JsonRedactionExtensions.WithSensitiveDataRedaction(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithSensitiveDataRedaction_HandlesCollectionsAndWriteOnlyProperties()
    {
        var json = JsonSerializer.Serialize(new List<WriteOnlySensitiveData>
        {
            new() { Secret = "hidden" },
        }, Build());

        json.Should().Be("[{}]");
        json.Should().NotContain("hidden");
    }

    [Fact]
    public void Mode_Redacted_ReplacesNonStringReferenceValuesWithNull()
    {
        var json = JsonSerializer.Serialize(new ReferenceSensitiveData
        {
            Payload = new Payload("secret"),
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Redacted,
        }));

        json.Should().Contain("\"Payload\":null");
        json.Should().NotContain("secret");
    }

    [Fact]
    public void AddSensitiveFlowJsonRedaction_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();

        var result = services.AddSensitiveFlowJsonRedaction();
        using var provider = services.BuildServiceProvider();

        result.Should().BeSameAs(services);
        provider.GetRequiredService<IOptions<JsonRedactionOptions>>()
            .Value.DefaultMode.Should().Be(JsonRedactionMode.Mask);
    }

    [Fact]
    public void AddSensitiveFlowJsonRedaction_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        services.AddSensitiveFlowJsonRedaction(options =>
        {
            options.DefaultMode = JsonRedactionMode.Redacted;
            options.RedactedPlaceholder = "***";
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<JsonRedactionOptions>>().Value;
        options.DefaultMode.Should().Be(JsonRedactionMode.Redacted);
        options.RedactedPlaceholder.Should().Be("***");
    }

    [Fact]
    public void AddSensitiveFlowJsonRedaction_RejectsNullServices()
    {
        var act = () => JsonRedactionExtensions.AddSensitiveFlowJsonRedaction(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    public class Customer
    {
        public int Id { get; set; }

        [PersonalData(Category = DataCategory.Identification)]
        public string Name { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        public string PublicNote { get; set; } = string.Empty;
    }

    public class MixedRedaction
    {
        [PersonalData(Category = DataCategory.Contact)]
        [JsonRedaction(JsonRedactionMode.None)]
        public string Email { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [JsonRedaction(JsonRedactionMode.Omit)]
        public string TaxId { get; set; } = string.Empty;
    }

    public class ExtendedCustomer
    {
        [PersonalData(Category = DataCategory.Contact)]
        public string Phone { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Identification)]
        public string NickName { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string TaxId { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string OneCharacterSecret { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string EmptySecret { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string? NullSecret { get; set; }
    }

    public class NumericSensitiveData
    {
        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public int Score { get; set; }
    }

    public class WriteOnlySensitiveData
    {
        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string Secret
        {
            set { _ = value; }
        }
    }

    public class ReferenceSensitiveData
    {
        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public Payload? Payload { get; set; }
    }

    public sealed record Payload(string Value);
}
