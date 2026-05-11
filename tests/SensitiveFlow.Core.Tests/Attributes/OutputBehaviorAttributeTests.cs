using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Tests.Attributes;

public sealed class OutputBehaviorAttributeTests
{
    [Fact]
    public void AllowSensitiveLogging_StoresJustificationAndRejectsNull()
    {
        var attribute = new AllowSensitiveLoggingAttribute("required for operational correlation");

        attribute.Justification.Should().Be("required for operational correlation");
        var act = () => new AllowSensitiveLoggingAttribute(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("justification");
    }

    [Fact]
    public void AllowSensitiveReturn_StoresJustificationAndRejectsBlank()
    {
        var attribute = new AllowSensitiveReturnAttribute("explicit data subject export");

        attribute.Justification.Should().Be("explicit data subject export");
        var act = () => new AllowSensitiveReturnAttribute(" ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RedactMaskAndOmitAttributes_ExposeOutputActions()
    {
        new RedactAttribute().Action.Should().Be(OutputRedactionAction.Redact);
        new OmitAttribute().Action.Should().Be(OutputRedactionAction.Omit);

        var mask = new MaskAttribute(MaskKind.Email);
        mask.Action.Should().Be(OutputRedactionAction.Mask);
        mask.Kind.Should().Be(MaskKind.Email);
    }

    [Fact]
    public void RedactionAttribute_ResolvesEveryContextAndUnknownAsNone()
    {
        var attribute = new RedactionAttribute
        {
            ApiResponse = OutputRedactionAction.Redact,
            Logs = OutputRedactionAction.Mask,
            Audit = OutputRedactionAction.Pseudonymize,
            Export = OutputRedactionAction.Omit,
        };

        attribute.ForContext(RedactionContext.ApiResponse).Should().Be(OutputRedactionAction.Redact);
        attribute.ForContext(RedactionContext.Log).Should().Be(OutputRedactionAction.Mask);
        attribute.ForContext(RedactionContext.Audit).Should().Be(OutputRedactionAction.Pseudonymize);
        attribute.ForContext(RedactionContext.Export).Should().Be(OutputRedactionAction.Omit);
        attribute.ForContext((RedactionContext)999).Should().Be(OutputRedactionAction.None);
    }

    [Fact]
    public void PersonalAndSensitiveData_DefaultSensitivityIsDocumentedByBehavior()
    {
        new PersonalDataAttribute().Sensitivity.Should().Be(DataSensitivity.Medium);
        new PersonalDataAttribute().Category.Should().Be(DataCategory.Other);

        new SensitiveDataAttribute().Sensitivity.Should().Be(DataSensitivity.High);
        new SensitiveDataAttribute().Category.Should().Be(SensitiveDataCategory.Other);
    }
}
