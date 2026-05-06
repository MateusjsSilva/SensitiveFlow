using FluentAssertions;
using SensitiveFlow.Anonymization.Masking;

namespace SensitiveFlow.Anonymization.Tests.Masking;

public sealed class PhoneMaskerEdgeCaseTests
{
    private readonly PhoneMasker _sut = new();

    [Fact]
    public void Mask_SingleDigitPhone_ReplacesWithAsterisk()
    {
        // 7 chars with exactly one digit — CanMask returns true, but fewer than 2 digits
        // triggers the fallback branch that replaces all digits with *.
        var result = _sut.Mask("(((((1)");
        result.Should().Be("(((((*)");
    }

    [Fact]
    public void CanMask_NullOrWhiteSpace_ReturnsFalse()
    {
        _sut.CanMask("   ").Should().BeFalse();
    }
}
