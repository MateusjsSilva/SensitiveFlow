using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.Core.Tests;

public sealed class SensitiveMemberCacheTests
{
    [Fact]
    public void GeneratedSensitiveType_HandlesNullCollectionsAsEmpty()
    {
        var metadata = new GeneratedSensitiveType(typeof(GeneratedCustomer), null!, null!);

        metadata.Type.Should().Be(typeof(GeneratedCustomer));
        metadata.SensitivePropertyNames.Should().BeEmpty();
        metadata.RetentionProperties.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedSensitiveType_RejectsNullType()
    {
        var act = () => new GeneratedSensitiveType(null!, [], []);

        act.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    [Fact]
    public void GeneratedRetentionProperty_RejectsNullPropertyName()
    {
        var act = () => new GeneratedRetentionProperty(null!, 1, 2, RetentionPolicy.BlockOnExpiration);

        act.Should().Throw<ArgumentNullException>().WithParameterName("propertyName");
    }

    [Fact]
    public void RegisterGeneratedMetadata_UsesGeneratedSensitiveAndRetentionProperties()
    {
        SensitiveMemberCache.RegisterGeneratedMetadata([
            new GeneratedSensitiveType(
                typeof(GeneratedCustomer),
                ["Email", "", "Missing"],
                [
                    new GeneratedRetentionProperty("TaxId", 3, 6, RetentionPolicy.DeleteOnExpiration),
                    new GeneratedRetentionProperty("Missing", 1, 0, RetentionPolicy.BlockOnExpiration),
                ]),
        ]);

        var sensitive = SensitiveMemberCache.GetSensitiveProperties(typeof(GeneratedCustomer));
        var retention = SensitiveMemberCache.GetRetentionProperties(typeof(GeneratedCustomer));

        sensitive.Should().ContainSingle(p => p.Name == nameof(GeneratedCustomer.Email));
        retention.Should().ContainSingle();
        retention[0].Property.Name.Should().Be(nameof(GeneratedCustomer.TaxId));
        retention[0].Attribute.Years.Should().Be(3);
        retention[0].Attribute.Months.Should().Be(6);
        retention[0].Attribute.Policy.Should().Be(RetentionPolicy.DeleteOnExpiration);
    }

    [Fact]
    public void RegisterGeneratedMetadata_IgnoresNullEntries()
    {
        SensitiveMemberCache.RegisterGeneratedMetadata([null!]);

        var sensitive = SensitiveMemberCache.GetSensitiveProperties(typeof(ReflectionCustomer));

        sensitive.Should().ContainSingle(p => p.Name == nameof(ReflectionCustomer.Name));
    }

    [Fact]
    public void SensitiveMemberCache_ReadsAttributesFromInterfaces()
    {
        var sensitive = SensitiveMemberCache.GetSensitiveProperties(typeof(InterfaceCustomer));
        var retention = SensitiveMemberCache.GetRetentionProperties(typeof(InterfaceCustomer));

        sensitive.Should().ContainSingle(p => p.Name == nameof(InterfaceCustomer.Email));
        retention.Should().ContainSingle(r => r.Property.Name == nameof(InterfaceCustomer.Email));
    }

    [Fact]
    public void RetentionProperty_ExposesPropertyAndAttribute()
    {
        var property = typeof(ReflectionCustomer).GetProperty(nameof(ReflectionCustomer.Name))!;
        var attribute = new RetentionDataAttribute { Years = 2 };

        var retention = new RetentionProperty(property, attribute);

        retention.Property.Should().BeSameAs(property);
        retention.Attribute.Should().BeSameAs(attribute);
    }

    private sealed class GeneratedCustomer
    {
        public string Email { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
    }

    private sealed class ReflectionCustomer
    {
        [PersonalData]
        public string Name { get; set; } = string.Empty;
    }

    private interface IInterfaceCustomer
    {
        [PersonalData]
        [RetentionData(Months = 1)]
        string Email { get; set; }
    }

    private sealed class InterfaceCustomer : IInterfaceCustomer
    {
        public string Email { get; set; } = string.Empty;
    }
}
