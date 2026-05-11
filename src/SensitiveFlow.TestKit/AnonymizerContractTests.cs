using FluentAssertions;
using SensitiveFlow.Core.Interfaces;
using Xunit;

namespace SensitiveFlow.TestKit;

/// <summary>
/// Conformance suite for <see cref="IAnonymizer"/> implementations.
/// </summary>
public abstract class AnonymizerContractTests
{
    /// <summary>Creates an anonymizer instance for the test.</summary>
    protected abstract IAnonymizer CreateAnonymizer();

    /// <summary>Verifies that supported values are changed.</summary>
    [Fact]
    public void Anonymize_SupportedValue_DoesNotReturnRawValue()
    {
        var anonymizer = CreateAnonymizer();
        const string value = "12345678909";

        if (anonymizer.CanAnonymize(value))
        {
            anonymizer.Anonymize(value).Should().NotBe(value);
        }
    }
}

