using FluentAssertions;
using SensitiveFlow.Anonymization.Comparison;

namespace SensitiveFlow.Anonymization.Tests.Comparison;

public sealed class DeterministicFingerprintTests
{
    private const string Key = "this-is-a-32-byte-key-for-test!!";
    private readonly DeterministicFingerprint _fp = new(Key);

    [Fact]
    public void Fingerprint_IsDeterministic_ForEqualInputs()
    {
        _fp.Fingerprint("alice@example.com").Should().Be(_fp.Fingerprint("alice@example.com"));
    }

    [Fact]
    public void Fingerprint_DiffersForDifferentInputs()
    {
        _fp.Fingerprint("alice@example.com").Should().NotBe(_fp.Fingerprint("bob@example.com"));
    }

    [Fact]
    public void Fingerprint_IsShortHexString()
    {
        var token = _fp.Fingerprint("any value");
        token.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void Fingerprint_OfNullOrEmpty_IsEmptyString()
    {
        _fp.Fingerprint(null).Should().BeEmpty();
        _fp.Fingerprint(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void AreEquivalent_ReturnsTrueForEqualInputs()
    {
        _fp.AreEquivalent("x@example.com", "x@example.com").Should().BeTrue();
    }

    [Fact]
    public void AreEquivalent_ReturnsFalseForDifferentInputs()
    {
        _fp.AreEquivalent("x@example.com", "y@example.com").Should().BeFalse();
    }

    [Fact]
    public void DifferentKey_ProducesDifferentFingerprint()
    {
        var other = new DeterministicFingerprint("another-32-byte-key-for-test!!!!");
        _fp.Fingerprint("same value").Should().NotBe(other.Fingerprint("same value"));
    }

    [Fact]
    public void TooShortKey_Throws()
    {
        Action act = () => _ = new DeterministicFingerprint("short");
        act.Should().Throw<ArgumentException>();
    }
}
