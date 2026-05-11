using FluentAssertions;
using SensitiveFlow.Core.Exceptions;

namespace SensitiveFlow.Core.Tests.Exceptions;

public sealed class SensitiveFlowExceptionSanitizerTests
{
    [Fact]
    public void Sanitize_ForSensitiveFlowException_PreservesTypeAndCodeButSanitizesMessage()
    {
        var exception = new SensitiveFlowConfigurationException(
            "Failed for maria@example.com and tax id 123.456.789-00.",
            "SF_TEST_001");

        var sanitized = SensitiveFlowExceptionSanitizer.Sanitize(exception);

        sanitized.Type.Should().Be(nameof(SensitiveFlowConfigurationException));
        sanitized.Code.Should().Be("SF_TEST_001");
        sanitized.Message.Should().Be("Failed for [email] and tax id [number].");
    }

    [Fact]
    public void Sanitize_ForRegularException_UsesNullCode()
    {
        var sanitized = SensitiveFlowExceptionSanitizer.Sanitize(new InvalidOperationException("email joao@example.com"));

        sanitized.Type.Should().Be(nameof(InvalidOperationException));
        sanitized.Code.Should().BeNull();
        sanitized.Message.Should().Be("email [email]");
    }

    [Fact]
    public void Sanitize_WithNullException_Throws()
    {
        var act = () => SensitiveFlowExceptionSanitizer.Sanitize(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("exception");
    }

    [Fact]
    public void SanitizeMessage_WithNullMessage_Throws()
    {
        var act = () => SensitiveFlowExceptionSanitizer.SanitizeMessage(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("message");
    }
}
