using FluentAssertions;
using SensitiveFlow.Anonymization.Export;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Anonymization.Tests.Export;

public sealed class DataSubjectExporterTests
{
    private readonly DataSubjectExporter _exporter = new();

    [Fact]
    public void Export_IncludesEveryAnnotatedProperty()
    {
        var customer = new Customer
        {
            Id = 1,
            Name = "Maria",
            Email = "maria@example.com",
            TaxId = "12345678900",
            PublicNote = "ignore me",
        };

        var exported = _exporter.Export(customer);

        exported.Should().ContainKey("Name").WhoseValue.Should().Be("Maria");
        exported.Should().ContainKey("Email").WhoseValue.Should().Be("maria@example.com");
        exported.Should().ContainKey("TaxId").WhoseValue.Should().Be("12345678900");
        exported.Should().NotContainKey("PublicNote");
        exported.Should().NotContainKey("Id");
    }

    [Fact]
    public void Export_IncludesRetentionOnlyProperties()
    {
        var record = new Audited { ConsentedAt = "2026-05-01" };

        var exported = _exporter.Export(record);

        exported.Should().ContainKey("ConsentedAt").WhoseValue.Should().Be("2026-05-01");
    }

    [Fact]
    public void Export_SkipsUnreadableAndDuplicateRetentionProperties()
    {
        var exported = _exporter.Export(new ReadShape
        {
            Email = "maria@example.com",
        });

        exported.Should().ContainSingle();
        exported.Should().ContainKey("Email").WhoseValue.Should().Be("maria@example.com");
        exported.Should().NotContainKey("WriteOnly");
    }

    [Fact]
    public void Export_OnNullEntity_Throws()
    {
        Action act = () => _exporter.Export(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    public class Customer
    {
        public int Id { get; set; }

        [PersonalData(Category = DataCategory.Identification)]
        public string Name { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string TaxId { get; set; } = string.Empty;

        public string PublicNote { get; set; } = string.Empty;
    }

    public class Audited
    {
        [RetentionData(Years = 5)]
        public string ConsentedAt { get; set; } = string.Empty;
    }

    public class ReadShape
    {
        [PersonalData]
        [RetentionData(Years = 1)]
        public string Email { get; set; } = string.Empty;

        [PersonalData]
        public string WriteOnly
        {
            set { _ = value; }
        }
    }
}
