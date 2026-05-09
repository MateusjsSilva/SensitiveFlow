using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.SourceGenerators.Tests;

/// <summary>
/// End-to-end tests for the source generator. The generator runs against this assembly
/// at compile time; its <c>ModuleInitializer</c> registers the metadata into
/// <see cref="SensitiveMemberCache"/> before any test executes. These tests verify the
/// resulting cache entries match the annotations on the test types.
/// </summary>
public sealed class GeneratedMetadataTests
{
    [Fact]
    public void Sensitive_PropertiesOnSimpleClass_AreDiscovered()
    {
        var properties = SensitiveMemberCache.GetSensitiveProperties(typeof(SimpleAnnotated));
        var names = properties.Select(p => p.Name).ToArray();

        names.Should().BeEquivalentTo("Email", "TaxId");
    }

    [Fact]
    public void Sensitive_PropertiesOnDerivedClass_IncludeBaseProperties()
    {
        var properties = SensitiveMemberCache.GetSensitiveProperties(typeof(Derived));
        var names = properties.Select(p => p.Name).ToArray();

        // Email comes from the base; Phone is declared on the derived type.
        names.Should().Contain(["Email", "Phone"]);
    }

    [Fact]
    public void Sensitive_PropertyAnnotatedDirectlyOnImplementation_IsDiscovered()
    {
        var properties = SensitiveMemberCache.GetSensitiveProperties(typeof(ImplementsInterfaceWithLocalAnnotation));
        var names = properties.Select(p => p.Name).ToArray();

        names.Should().Contain("InterfaceEmail");
    }

    [Fact]
    public void Sensitive_PropertyAnnotatedOnlyOnInterface_IsDiscoveredOnImplementer()
    {
        // Both the source generator and the reflection fallback merge attributes from
        // implemented interfaces into the implementing class.
        var properties = SensitiveMemberCache.GetSensitiveProperties(typeof(ImplementsInterface));
        properties.Select(p => p.Name).Should().Contain("InterfaceEmail");
    }

    [Fact]
    public void Retention_PropertyMetadataIsPreserved()
    {
        var retentions = SensitiveMemberCache.GetRetentionProperties(typeof(SimpleAnnotated));

        retentions.Should().ContainSingle();
        retentions[0].Property.Name.Should().Be("TaxId");
        retentions[0].Attribute.Years.Should().Be(5);
        retentions[0].Attribute.Policy.Should().Be(RetentionPolicy.AnonymizeOnExpiration);
    }

    [Fact]
    public void NonAnnotatedType_ReturnsEmptyResults()
    {
        var properties = SensitiveMemberCache.GetSensitiveProperties(typeof(NoAnnotations));
        var retentions = SensitiveMemberCache.GetRetentionProperties(typeof(NoAnnotations));

        properties.Should().BeEmpty();
        retentions.Should().BeEmpty();
    }

    [Fact]
    public void GenericType_IsHandledByGenerator()
    {
        var properties = SensitiveMemberCache.GetSensitiveProperties(typeof(GenericContainer<int>));

        // The generator emits typeof(GenericContainer<>); reflection lookup of a closed generic
        // resolves through the open definition, so the cache is built via the reflection fallback
        // path. Either way, the annotation must be discovered.
        properties.Select(p => p.Name).Should().Contain("Payload");
    }

    // ── Test types ────────────────────────────────────────────────────────────

    public class SimpleAnnotated
    {
        public string DataSubjectId { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string TaxId { get; set; } = string.Empty;

        public string PublicNonSensitive { get; set; } = string.Empty;
    }

    public sealed class Derived : SimpleAnnotated
    {
        [PersonalData(Category = DataCategory.Contact)]
        public string Phone { get; set; } = string.Empty;
    }

    public interface IHasInterfaceEmail
    {
        [PersonalData(Category = DataCategory.Contact)]
        string InterfaceEmail { get; set; }
    }

    public sealed class ImplementsInterface : IHasInterfaceEmail
    {
        public string InterfaceEmail { get; set; } = string.Empty;
    }

    public sealed class ImplementsInterfaceWithLocalAnnotation : IHasInterfaceEmail
    {
        // Recommended pattern: re-declare the attribute on the implementation so both
        // the interface contract and the runtime metadata are explicit.
        [PersonalData(Category = DataCategory.Contact)]
        public string InterfaceEmail { get; set; } = string.Empty;
    }

    public sealed class NoAnnotations
    {
        public string SomeField { get; set; } = string.Empty;
    }

    public class GenericContainer<T>
    {
        [PersonalData(Category = DataCategory.Other)]
        public string Payload { get; set; } = string.Empty;
    }
}
