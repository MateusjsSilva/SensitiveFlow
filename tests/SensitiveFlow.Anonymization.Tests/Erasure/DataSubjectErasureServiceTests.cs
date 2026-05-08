using FluentAssertions;
using SensitiveFlow.Anonymization.Erasure;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Anonymization.Tests.Erasure;

public sealed class DataSubjectErasureServiceTests
{
    private sealed class Customer
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "subject-1";

        [PersonalData(Category = DataCategory.Identification)]
        public string Name { get; set; } = "João";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "joao@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Health)]
        public string HealthNote { get; set; } = "private";

        public string PublicField { get; set; } = "should-stay";
    }

    [Fact]
    public void Erase_OverwritesAnnotatedProperties()
    {
        var sut = new DataSubjectErasureService(new RedactionErasureStrategy());
        var customer = new Customer();

        var count = sut.Erase(customer);

        count.Should().Be(3);
        customer.Name.Should().Be("[ERASED]");
        customer.Email.Should().Be("[ERASED]");
        customer.HealthNote.Should().Be("[ERASED]");
    }

    [Fact]
    public void Erase_PreservesNonAnnotatedProperties()
    {
        var sut = new DataSubjectErasureService(new RedactionErasureStrategy());
        var customer = new Customer();

        sut.Erase(customer);

        customer.Id.Should().Be(0); // unchanged default
        customer.DataSubjectId.Should().Be("subject-1");
        customer.PublicField.Should().Be("should-stay");
    }

    [Fact]
    public void Erase_CustomMarker_IsApplied()
    {
        var sut = new DataSubjectErasureService(new RedactionErasureStrategy("[GONE]"));
        var customer = new Customer();

        sut.Erase(customer);

        customer.Name.Should().Be("[GONE]");
    }

    [Fact]
    public void Erase_NullEntity_Throws()
    {
        var sut = new DataSubjectErasureService(new RedactionErasureStrategy());
        var act = () => sut.Erase(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
