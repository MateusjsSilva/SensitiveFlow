using FluentAssertions;
using SensitiveFlow.Core.Interfaces;
using Xunit;

namespace SensitiveFlow.TestKit;

/// <summary>
/// Conformance suite for <see cref="IMasker"/> implementations.
/// </summary>
public abstract class MaskerContractTests
{
    /// <summary>Creates a masker instance for the test.</summary>
    protected abstract IMasker CreateMasker();

    /// <summary>Verifies that supported values are changed.</summary>
    [Fact]
    public void Mask_SupportedValue_DoesNotReturnRawValue()
    {
        var masker = CreateMasker();
        const string value = "alice@example.com";

        if (masker.CanMask(value))
        {
            masker.Mask(value).Should().NotBe(value);
        }
    }
}

