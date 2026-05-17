using FluentAssertions;
using SensitiveFlow.SourceGenerators.Incremental;
using Xunit;

namespace SensitiveFlow.SourceGenerators.Tests;

public class IncrementalGenerationTrackerTests
{
    [Fact]
    public void RegisterGeneratedType_TracksType()
    {
        var tracker = new IncrementalGenerationTracker();
        var info = new GeneratedTypeInfo
        {
            FullyQualifiedName = "MyApp.Models.Customer",
            SensitivePropertyCount = 3
        };

        tracker.RegisterGeneratedType("MyApp.Models.Customer", info);

        tracker.GeneratedTypes.Should().ContainKey("MyApp.Models.Customer");
    }

    [Fact]
    public void MarkAsModified_AddsToModifiedSet()
    {
        var tracker = new IncrementalGenerationTracker();
        tracker.MarkAsModified("MyApp.Models.Customer");

        tracker.ModifiedTypes.Should().Contain("MyApp.Models.Customer");
    }

    [Fact]
    public void NeedsRegeneration_ReturnsTrueForModified()
    {
        var tracker = new IncrementalGenerationTracker();
        tracker.MarkAsModified("MyApp.Models.Customer");

        tracker.NeedsRegeneration("MyApp.Models.Customer").Should().BeTrue();
    }

    [Fact]
    public void NeedsRegeneration_ReturnsTrueForUnregistered()
    {
        var tracker = new IncrementalGenerationTracker();

        tracker.NeedsRegeneration("Unknown.Type").Should().BeTrue();
    }

    [Fact]
    public void NeedsRegeneration_ReturnsFalseForUnmodifiedRegistered()
    {
        var tracker = new IncrementalGenerationTracker();
        var info = new GeneratedTypeInfo { FullyQualifiedName = "MyApp.Models.Customer" };

        tracker.RegisterGeneratedType("MyApp.Models.Customer", info);

        tracker.NeedsRegeneration("MyApp.Models.Customer").Should().BeFalse();
    }

    [Fact]
    public void RegisterGeneratedType_ClearsModificationFlag()
    {
        var tracker = new IncrementalGenerationTracker();
        tracker.MarkAsModified("MyApp.Models.Customer");
        var info = new GeneratedTypeInfo { FullyQualifiedName = "MyApp.Models.Customer" };

        tracker.RegisterGeneratedType("MyApp.Models.Customer", info);

        tracker.ModifiedTypes.Should().NotContain("MyApp.Models.Customer");
    }

    [Fact]
    public void GetTypesNeedingRegeneration_ReturnsOnlyModified()
    {
        var tracker = new IncrementalGenerationTracker();
        tracker.MarkAsModified("Type1");
        tracker.MarkAsModified("Type2");

        var needsRegen = tracker.GetTypesNeedingRegeneration().ToList();

        needsRegen.Should().HaveCount(2);
        needsRegen.Should().Contain("Type1");
        needsRegen.Should().Contain("Type2");
    }

    [Fact]
    public void ClearModifications_RemovesAllModifiedMarks()
    {
        var tracker = new IncrementalGenerationTracker();
        tracker.MarkAsModified("Type1");
        tracker.MarkAsModified("Type2");

        tracker.ClearModifications();

        tracker.ModifiedTypes.Should().BeEmpty();
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var tracker = new IncrementalGenerationTracker();
        var info = new GeneratedTypeInfo { FullyQualifiedName = "MyApp.Models.Customer" };
        tracker.RegisterGeneratedType("MyApp.Models.Customer", info);
        tracker.MarkAsModified("Type1");

        tracker.Reset();

        tracker.GeneratedTypes.Should().BeEmpty();
        tracker.ModifiedTypes.Should().BeEmpty();
    }

    [Fact]
    public void RegisterGeneratedType_ThrowsOnNullName()
    {
        var tracker = new IncrementalGenerationTracker();
        var info = new GeneratedTypeInfo();

        var act = () => tracker.RegisterGeneratedType(null!, info);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MarkAsModified_ThrowsOnNullName()
    {
        var tracker = new IncrementalGenerationTracker();

        var act = () => tracker.MarkAsModified(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GeneratedTypeInfo_StoresMetadata()
    {
        var info = new GeneratedTypeInfo
        {
            FullyQualifiedName = "MyApp.Customer",
            SensitivePropertyCount = 5
        };

        info.FullyQualifiedName.Should().Be("MyApp.Customer");
        info.SensitivePropertyCount.Should().Be(5);
        info.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
