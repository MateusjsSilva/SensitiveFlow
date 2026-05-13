using FluentAssertions;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Policies;

namespace SensitiveFlow.Core.Tests;

public sealed class RedactionPolicyValidatorTests
{
    [Fact]
    public void ValidateAuditStore_ThrowsWhenNull()
    {
        var action = () => RedactionPolicyValidator.ValidateAuditStore(null);

        action.Should().Throw<RedactionPolicyViolationException>()
            .Which.Code.Should().Be("SF_REDACTION_002");
    }

    [Fact]
    public void ValidateTokenStore_ThrowsWhenNull()
    {
        var action = () => RedactionPolicyValidator.ValidateTokenStore(null);

        action.Should().Throw<RedactionPolicyViolationException>()
            .Which.Code.Should().Be("SF_REDACTION_002");
    }

    [Fact]
    public void ValidatePoliciesRegistered_ThrowsWhenNull()
    {
        var action = () => RedactionPolicyValidator.ValidatePoliciesRegistered(null);

        action.Should().Throw<RedactionPolicyViolationException>()
            .Which.Code.Should().Be("SF_REDACTION_004");
    }

    [Fact]
    public void ValidateAuditContext_ThrowsWhenNull()
    {
        var action = () => RedactionPolicyValidator.ValidateAuditContext(null);

        action.Should().Throw<RedactionPolicyViolationException>()
            .Which.Code.Should().Be("SF_REDACTION_002");
    }

    [Fact]
    public void ValidateTypeAnnotation_ThrowsWhenMissing()
    {
        var action = () => RedactionPolicyValidator.ValidateTypeAnnotation("Customer", hasAnnotation: false);

        action.Should().Throw<RedactionPolicyViolationException>()
            .Which.Code.Should().Be("SF_REDACTION_001");
    }

    [Fact]
    public void ValidateTypeAnnotation_SucceedsWhenPresent()
    {
        var action = () => RedactionPolicyValidator.ValidateTypeAnnotation("Customer", hasAnnotation: true);

        action.Should().NotThrow();
    }

    [Fact]
    public void ValidatePropertyAnnotation_ThrowsWhenMissing()
    {
        var action = () =>
            RedactionPolicyValidator.ValidatePropertyAnnotation("Customer", "Email", hasAnnotation: false);

        action.Should().Throw<RedactionPolicyViolationException>()
            .Which.Code.Should().Be("SF_REDACTION_001");
    }

    [Fact]
    public void ValidatePropertyAnnotation_SucceedsWhenPresent()
    {
        var action = () =>
            RedactionPolicyValidator.ValidatePropertyAnnotation("Customer", "Email", hasAnnotation: true);

        action.Should().NotThrow();
    }
}

public sealed class RedactionPolicyViolationExceptionTests
{
    [Fact]
    public void MissingAnnotation_CreatesDescriptiveMessage()
    {
        var exception = RedactionPolicyViolationException.MissingAnnotation("Customer");

        exception.Message.Should().Contain("Customer");
        exception.Message.Should().Contain("missing");
        exception.Code.Should().Be("SF_REDACTION_001");
    }

    [Fact]
    public void MissingAnnotation_WithProperty_IncludesPropertyName()
    {
        var exception = RedactionPolicyViolationException.MissingAnnotation("Customer", "Email");

        exception.Message.Should().Contain("Customer");
        exception.Message.Should().Contain("Email");
        exception.Code.Should().Be("SF_REDACTION_001");
    }

    [Fact]
    public void MissingInfrastructure_CreatesDescriptiveMessage()
    {
        var exception = RedactionPolicyViolationException.MissingInfrastructure("IAuditStore");

        exception.Message.Should().Contain("IAuditStore");
        exception.Message.Should().Contain("not registered");
        exception.Code.Should().Be("SF_REDACTION_002");
    }

    [Fact]
    public void MissingInfrastructure_PreservesInnerException()
    {
        var inner = new InvalidOperationException("Connection failed");
        var exception = RedactionPolicyViolationException.MissingInfrastructure("IAuditStore", inner);

        exception.InnerException.Should().Be(inner);
    }

    [Fact]
    public void RedactionFailed_CreatesDescriptiveMessage()
    {
        var exception = RedactionPolicyViolationException.RedactionFailed("Email", "serialize");

        exception.Message.Should().Contain("Email");
        exception.Message.Should().Contain("serialize");
        exception.Code.Should().Be("SF_REDACTION_003");
    }

    [Fact]
    public void RedactionFailed_PreservesInnerException()
    {
        var inner = new NotImplementedException("Redaction strategy not supported");
        var exception = RedactionPolicyViolationException.RedactionFailed("Phone", "mask", inner);

        exception.InnerException.Should().Be(inner);
    }

    [Fact]
    public void Exception_HasDistinctCodes()
    {
        var ex1 = RedactionPolicyViolationException.MissingAnnotation("Type");
        var ex2 = RedactionPolicyViolationException.MissingInfrastructure("Service");
        var ex3 = RedactionPolicyViolationException.RedactionFailed("Field", "operation");

        ex1.Code.Should().NotBe(ex2.Code);
        ex2.Code.Should().NotBe(ex3.Code);
        ex1.Code.Should().NotBe(ex3.Code);
    }
}
