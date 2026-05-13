using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Json.Attributes;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Json.Extensions;
using SensitiveFlow.Core.Policies;

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
            Token = "abcdef12345",
        }, Build(new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask }));

        // Email overrides to None — appears verbatim
        json.Should().Contain("\"Email\":\"secret@x.com\"");
        // TaxId overrides to Omit
        json.Should().NotContain("TaxId");
        json.Should().Contain("\"Token\":\"abc********\"");
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
    public void Mode_Mask_ReplacesNonStringSensitiveValuesWithTypedPlaceholders()
    {
        var json = JsonSerializer.Serialize(new NumericSensitiveData
        {
            Score = 42,
            Salary = 1234.56m,
            BirthDate = new DateTime(1990, 5, 15),
            IsActive = true,
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Mask,
            RedactedPlaceholder = "***",
            NonStringRedactionMode = JsonNonStringRedactionMode.Placeholder,
        }));

        json.Should().Contain("\"Score\":\"[NUMBER_REDACTED]\"");
        json.Should().Contain("\"Salary\":\"[NUMBER_REDACTED]\"");
        json.Should().Contain("\"BirthDate\":\"[DATE_REDACTED]\"");
        json.Should().Contain("\"IsActive\":\"[BOOLEAN_REDACTED]\"");
        json.Should().NotContain("42");
        json.Should().NotContain("1234.56");
        json.Should().NotContain("1990-05-15");
        json.Should().NotContain("true");
    }

    [Fact]
    public void Mode_Mask_CanReplaceNonStringSensitiveValuesWithNull()
    {
        var json = JsonSerializer.Serialize(new NumericSensitiveData
        {
            Score = 42,
            Salary = 1234.56m,
            BirthDate = new DateTime(1990, 5, 15),
            IsActive = true,
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Mask,
            NonStringRedactionMode = JsonNonStringRedactionMode.Null,
        }));

        json.Should().Contain("\"Score\":null");
        json.Should().Contain("\"Salary\":null");
        json.Should().Contain("\"BirthDate\":null");
        json.Should().Contain("\"IsActive\":null");
        json.Should().NotContain("42");
        json.Should().NotContain("1234.56");
        json.Should().NotContain("1990-05-15");
        json.Should().NotContain("true");
    }

    [Fact]
    public void Mode_Mask_CanOmitNonStringSensitiveValues()
    {
        var json = JsonSerializer.Serialize(new NumericSensitiveData
        {
            Score = 42,
            Salary = 1234.56m,
            BirthDate = new DateTime(1990, 5, 15),
            IsActive = true,
            PublicCount = 7,
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Mask,
            NonStringRedactionMode = JsonNonStringRedactionMode.Omit,
        }));

        json.Should().NotContain("Score");
        json.Should().NotContain("Salary");
        json.Should().NotContain("BirthDate");
        json.Should().NotContain("IsActive");
        json.Should().Contain("\"PublicCount\":7");
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
    public void Mode_Mask_ReplacesCollectionsWithNullByDefault()
    {
        var json = JsonSerializer.Serialize(new CollectionSensitiveData
        {
            EmailAddresses = ["alice@example.com", "bob@example.com"],
            Tokens = ["tok_12345", "tok_67890"],
            EmptyValues = [],
        }, Build());

        json.Should().Contain("\"EmailAddresses\":null");
        json.Should().Contain("\"Tokens\":null");
        json.Should().Contain("\"EmptyValues\":null");
        json.Should().NotContain("alice@example.com");
        json.Should().NotContain("tok_12345");
    }

    [Fact]
    public void Mode_Mask_CanRedactCollectionsElementByElementWithPlaceholderMode()
    {
        var json = JsonSerializer.Serialize(new CollectionSensitiveData
        {
            EmailAddresses = ["alice@example.com", "bob@example.com"],
            Tokens = ["tok_12345", "tok_67890"],
            EmptyValues = [],
        }, Build(new JsonRedactionOptions
        {
            NonStringRedactionMode = JsonNonStringRedactionMode.Placeholder,
        }));

        json.Should().Contain("\"EmailAddresses\":[\"a****@example.com\",\"b**@example.com\"]");
        json.Should().Contain("\"Tokens\":[\"t********\",\"t********\"]");
        json.Should().Contain("\"EmptyValues\":[]");
        json.Should().NotContain("alice@example.com");
        json.Should().NotContain("tok_12345");
    }

    [Fact]
    public void Mode_Redacted_RedactsCollectionsWithoutDroppingCount()
    {
        var json = JsonSerializer.Serialize(new CollectionSensitiveData
        {
            EmailAddresses = ["alice@example.com", "bob@example.com"],
            Tokens = ["tok_12345"],
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Redacted,
            RedactedPlaceholder = "***",
            NonStringRedactionMode = JsonNonStringRedactionMode.Placeholder,
        }));

        json.Should().Contain("\"EmailAddresses\":[\"***\",\"***\"]");
        json.Should().Contain("\"Tokens\":[\"***\"]");
        json.Should().NotContain("alice@example.com");
        json.Should().NotContain("tok_12345");
    }

    [Fact]
    public void Modifier_SkipsSyntheticPropertiesWithoutClrAttributeProvider()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(Customer))
            {
                return;
            }

            var synthetic = typeInfo.CreateJsonPropertyInfo(typeof(string), "Synthetic");
            synthetic.Get = _ => "public";
            typeInfo.Properties.Add(synthetic);
        });

        var options = new JsonSerializerOptions { TypeInfoResolver = resolver }
            .WithSensitiveDataRedaction();

        var json = JsonSerializer.Serialize(new Customer
        {
            Name = "Alice",
            Email = "alice@example.com",
        }, options);

        json.Should().Contain("\"Synthetic\":\"public\"");
        json.Should().NotContain("alice@example.com");
    }

    [Fact]
    public void Mode_Mask_UsesPlaceholderWhenStringPropertyGetterReturnsNonStringValue()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(OddGetterSensitiveData))
            {
                return;
            }

            var original = typeInfo.Properties.Single(p => p.Name == nameof(OddGetterSensitiveData.Secret));
            var index = typeInfo.Properties.IndexOf(original);
            typeInfo.Properties.RemoveAt(index);

            var replacement = typeInfo.CreateJsonPropertyInfo(typeof(string), nameof(OddGetterSensitiveData.Secret));
            replacement.AttributeProvider = typeof(OddGetterSensitiveData).GetProperty(nameof(OddGetterSensitiveData.Secret));
            replacement.Get = _ => 123;
            typeInfo.Properties.Insert(index, replacement);
        });

        var options = new JsonSerializerOptions { TypeInfoResolver = resolver }
            .WithSensitiveDataRedaction(new JsonRedactionOptions
            {
                DefaultMode = JsonRedactionMode.Mask,
                RedactedPlaceholder = "***",
            });

        var json = JsonSerializer.Serialize(new OddGetterSensitiveData(), options);

        json.Should().Contain("\"Secret\":\"***\"");
    }

    [Fact]
    public void UnknownRedactionMode_LeavesStringValueUnchanged()
    {
        var json = JsonSerializer.Serialize(new UnknownModeSensitiveData
        {
            Secret = "visible",
        }, Build());

        json.Should().Contain("\"Secret\":\"visible\"");
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

    [Fact]
    public void OutputAttributes_TakePrecedenceOverDefaultMode()
    {
        var json = JsonSerializer.Serialize(new OutputAttributeCustomer
        {
            Email = "alice@example.com",
            TaxId = "12345678900",
            Secret = "hidden",
        }, Build(new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask }));

        json.Should().Contain("\"Email\":\"[REDACTED]\"");
        json.Should().Contain("\"TaxId\":\"1**********\"");
        json.Should().NotContain("Secret");
        json.Should().NotContain("hidden");
    }

    [Fact]
    public void ContextualRedaction_ApiResponseMode_TakesPrecedenceOverDefaultMode()
    {
        var json = JsonSerializer.Serialize(new ContextualCustomer
        {
            Email = "alice@example.com",
        }, Build(new JsonRedactionOptions { DefaultMode = JsonRedactionMode.Mask }));

        json.Should().NotContain("Email");
        json.Should().NotContain("alice@example.com");
    }

    [Fact]
    public void Policies_ResolveJsonActionsByCategory()
    {
        var policies = new SensitiveFlowPolicyRegistry();
        policies.ForCategory(DataCategory.Contact).RedactInJson();
        policies.ForSensitiveCategory(SensitiveDataCategory.Other).OmitInJson();

        var json = JsonSerializer.Serialize(new PolicyCustomer
        {
            Email = "alice@example.com",
            TaxId = "12345678900",
            Name = "Alice",
        }, Build(new JsonRedactionOptions
        {
            DefaultMode = JsonRedactionMode.Mask,
            Policies = policies,
        }));

        json.Should().Contain("\"Email\":\"[REDACTED]\"");
        json.Should().NotContain("TaxId");
        json.Should().Contain("\"Name\":\"A****\"");
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

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [JsonRedaction(RedactionMode = JsonRedactionMode.Mask, PreservePrefixLength = 3)]
        public string Token { get; set; } = string.Empty;
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

        [SensitiveData(Category = SensitiveDataCategory.Financial)]
        public decimal Salary { get; set; }

        [PersonalData(Category = DataCategory.Identification)]
        public DateTime BirthDate { get; set; }

        [SensitiveData(Category = SensitiveDataCategory.Health)]
        public bool IsActive { get; set; }

        public int PublicCount { get; set; }
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

    public class OddGetterSensitiveData
    {
        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string Secret { get; set; } = "hidden";
    }

    public class UnknownModeSensitiveData
    {
        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [JsonRedaction((JsonRedactionMode)999)]
        public string Secret { get; set; } = string.Empty;
    }

    public sealed record Payload(string Value);

    public class CollectionSensitiveData
    {
        [PersonalData(Category = DataCategory.Contact)]
        public List<string> EmailAddresses { get; set; } = [];

        [SensitiveData(Category = SensitiveDataCategory.Financial)]
        public string[] Tokens { get; set; } = [];

        [PersonalData(Category = DataCategory.Other)]
        public List<string> EmptyValues { get; set; } = [];
    }

    public class OutputAttributeCustomer
    {
        [PersonalData(Category = DataCategory.Contact)]
        [Redact]
        public string Email { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [Mask(MaskKind.Generic)]
        public string TaxId { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [Omit]
        public string Secret { get; set; } = string.Empty;
    }

    public class ContextualCustomer
    {
        [PersonalData(Category = DataCategory.Contact)]
        [Redaction(ApiResponse = OutputRedactionAction.Omit, Logs = OutputRedactionAction.Mask)]
        public string Email { get; set; } = string.Empty;
    }

    public class PolicyCustomer
    {
        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string TaxId { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Identification)]
        public string Name { get; set; } = string.Empty;
    }
}
