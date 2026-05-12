using FluentAssertions;
using SensitiveFlow.Core.Interfaces;
using Xunit;

namespace SensitiveFlow.TestKit;

/// <summary>
/// Conformance suite for <see cref="IPseudonymizer"/> implementations.
/// </summary>
public abstract class PseudonymizerContractTests
{
    /// <summary>Creates a pseudonymizer instance for the test.</summary>
    protected abstract Task<IPseudonymizer> CreatePseudonymizerAsync();

    /// <summary>Verifies stable reversible pseudonymization.</summary>
    [Fact]
    public async Task PseudonymizeAsync_ReturnsStableReversibleValue()
    {
        var pseudonymizer = await CreatePseudonymizerAsync();
        var first = await pseudonymizer.PseudonymizeAsync("alice@example.com");
        var second = await pseudonymizer.PseudonymizeAsync("alice@example.com");

        first.Should().Be(second);
        var original = await pseudonymizer.ReverseAsync(first);
        original.Should().Be("alice@example.com");
    }

    /// <summary>Verifies capability checks for valid values.</summary>
    [Fact]
    public async Task CanPseudonymize_ReturnsTrueForNonEmptyValue()
    {
        var pseudonymizer = await CreatePseudonymizerAsync();
        pseudonymizer.CanPseudonymize("value").Should().BeTrue();
    }
}

