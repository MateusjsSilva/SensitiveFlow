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
    public void Export_RespectsContextualExportRedaction()
    {
        var exported = _exporter.Export(new ContextualExportCustomer());

        exported.Should().ContainKey("Email").WhoseValue.Should().Be("m****@example.com");
        exported.Should().ContainKey("TaxId").WhoseValue.Should().Be("[REDACTED]");
        exported.Should().NotContainKey("SecretNote");
    }

    [Fact]
    public void Export_MasksPhoneNameGenericAndEmptyValues()
    {
        var exported = _exporter.Export(new MaskedExportShape());

        exported["Phone"].Should().Be("(**) *****-**89");
        exported["Name"].Should().Be("M**** S****");
        exported["Code"].Should().Be("A***");
        exported["Single"].Should().Be("*");
        exported["Empty"].Should().Be(string.Empty);
    }

    [Fact]
    public void Export_MaskOnNonStringValue_ReturnsNull()
    {
        var exported = _exporter.Export(new NonStringMaskedShape());

        exported.Should().ContainKey("Score").WhoseValue.Should().BeNull();
    }

    [Fact]
    public void Export_EmailWithoutUsablePrefix_UsesGenericMask()
    {
        var exported = _exporter.Export(new ShortEmailShape());

        exported["Email"].Should().Be("a**");
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

    public class ContextualExportCustomer
    {
        [PersonalData(Category = DataCategory.Contact)]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public string Email { get; set; } = "maria@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [Redaction(Export = OutputRedactionAction.Redact)]
        public string TaxId { get; set; } = "12345678900";

        [PersonalData(Category = DataCategory.Other)]
        [Redaction(Export = OutputRedactionAction.Omit)]
        public string SecretNote { get; set; } = "hide me";
    }

    public class MaskedExportShape
    {
        [PersonalData]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public string Phone { get; set; } = "(11) 99999-8889";

        [PersonalData]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public string Name { get; set; } = "Maria Silva";

        [PersonalData]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public string Code { get; set; } = "ABCD";

        [PersonalData]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public string Single { get; set; } = "Z";

        [PersonalData]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public string Empty { get; set; } = string.Empty;
    }

    public class NonStringMaskedShape
    {
        [PersonalData]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public int Score { get; set; } = 42;
    }

    public class ShortEmailShape
    {
        [PersonalData]
        [Redaction(Export = OutputRedactionAction.Mask)]
        public string Email { get; set; } = "a@x";
    }
}
