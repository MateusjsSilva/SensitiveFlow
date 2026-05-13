using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Correlation;
using SensitiveFlow.Core.Discovery;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;

namespace SensitiveFlow.Core.Tests;

public sealed class CoreCoverageTests
{
    [Theory]
    [InlineData("SQLite Error 1: 'no such table: SensitiveFlow_AuditRecords'.", "SensitiveFlow_AuditRecords")]
    [InlineData("Invalid object name 'SensitiveFlow_TokenMappings'.", "SensitiveFlow_TokenMappings")]
    [InlineData("relation \"sensitiveflow_tokenmappings\" does not exist", "sensitiveflow_tokenmappings")]
    [InlineData("Table 'app.sensitiveflow_tokenmappings' doesn't exist", "app.sensitiveflow_tokenmappings")]
    [InlineData("ORA-00942: table or view does not exist", null)]
    public void SchemaErrorTranslator_TranslatesKnownMissingTableMessages(string message, string? tableName)
    {
        var original = new InvalidOperationException(message);

        var translated = SchemaErrorTranslator.Translate(original, "AuditDbContext");

        translated.Should().BeOfType<SensitiveFlowSchemaNotInitializedException>();
        var schema = (SensitiveFlowSchemaNotInitializedException)translated;
        schema.TableName.Should().Be(tableName);
        schema.ContextName.Should().Be("AuditDbContext");
        schema.InnerException.Should().Be(original);
    }

    [Fact]
    public void SchemaErrorTranslator_UsesInnerExceptionAndReturnsOriginalWhenNoMatch()
    {
        var inner = new InvalidOperationException("no such table: NestedTable");
        var outer = new InvalidOperationException("wrapper", inner);

        SchemaErrorTranslator.Translate(outer, "Ctx")
            .Should().BeOfType<SensitiveFlowSchemaNotInitializedException>();

        var unrelated = new InvalidOperationException("connection failed");
        SchemaErrorTranslator.Translate(unrelated, "Ctx").Should().BeSameAs(unrelated);
        FluentActions.Invoking(() => SchemaErrorTranslator.Translate(null!, "Ctx"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PolicyRegistry_ReusesBuildersAndAccumulatesActions()
    {
        var registry = new SensitiveFlowPolicyRegistry();

        var personal = registry.ForCategory(DataCategory.Contact)
            .MaskInLogs()
            .RedactInJson()
            .AuditOnChange();
        var samePersonal = registry.ForCategory(DataCategory.Contact)
            .RequireAudit();
        registry.ForSensitiveCategory(SensitiveDataCategory.Health)
            .OmitInJson()
            .RequireAudit();

        samePersonal.Should().BeSameAs(personal);
        registry.Rules.Should().HaveCount(2);
        registry.Find(DataCategory.Contact)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.MaskInLogs);
        registry.Find(DataCategory.Contact)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.RedactInJson);
        registry.Find(DataCategory.Contact)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.AuditOnChange);
        registry.Find(DataCategory.Contact)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.RequireAudit);
        registry.Find(SensitiveDataCategory.Health)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.OmitInJson);
        registry.Find(DataCategory.Financial).Should().BeNull();
        registry.Find(SensitiveDataCategory.Financial).Should().BeNull();
    }

    [Fact]
    public void SimpleCoreModelsAndAttributes_ExposeConfiguredValues()
    {
        var snapshot = new AuditSnapshot
        {
            DataSubjectId = "subject",
            Aggregate = "Customer",
            AggregateId = "customer-1",
            Operation = AuditOperation.Update,
            BeforeJson = "{}",
            AfterJson = "{\"ok\":true}",
            ActorId = "actor",
            IpAddressToken = "ip-token",
        };
        var record = new AuditRecord
        {
            DataSubjectId = "subject",
            Entity = "Customer",
            Field = "Email",
        };

        snapshot.Id.Should().NotBeEmpty();
        snapshot.AggregateId.Should().Be("customer-1");
        record.Operation.Should().Be(AuditOperation.Access);
        new MaskAttribute(MaskKind.Email).Kind.Should().Be(MaskKind.Email);
        new MaskAttribute().Action.Should().Be(OutputRedactionAction.Mask);
        new RedactAttribute().Action.Should().Be(OutputRedactionAction.Redact);
        new OmitAttribute().Action.Should().Be(OutputRedactionAction.Omit);
        new AllowSensitiveLoggingAttribute("diagnostic export").Justification.Should().Be("diagnostic export");
        new AllowSensitiveReturnAttribute("admin-only endpoint").Justification.Should().Be("admin-only endpoint");
    }

    [Fact]
    public void RedactionAndRetentionAttributes_ExposeContextualValues()
    {
        var redaction = new RedactionAttribute
        {
            ApiResponse = OutputRedactionAction.Omit,
            Logs = OutputRedactionAction.Mask,
            Audit = OutputRedactionAction.Redact,
        };
        var retention = new RetentionDataAttribute
        {
            Years = 2,
            Months = 3,
            Policy = RetentionPolicy.DeleteOnExpiration,
        };

        redaction.ForContext(RedactionContext.ApiResponse).Should().Be(OutputRedactionAction.Omit);
        redaction.ForContext(RedactionContext.Log).Should().Be(OutputRedactionAction.Mask);
        redaction.ForContext(RedactionContext.Audit).Should().Be(OutputRedactionAction.Redact);
        retention.GetExpirationDate(new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2028, 4, 30, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void DiscoveryReport_SortsEntriesAndRejectsNull()
    {
        var report = new SensitiveDataDiscoveryReport([
            new SensitiveDataDiscoveryEntry { TypeName = "B", MemberName = "Second", Annotation = "PersonalDataAttribute" },
            new SensitiveDataDiscoveryEntry { TypeName = "A", MemberName = "First", Annotation = "SensitiveDataAttribute", SensitiveCategory = SensitiveDataCategory.Health },
        ]);

        report.Entries.Select(e => $"{e.TypeName}.{e.MemberName}")
            .Should().Equal("A.First", "B.Second");
        report.ToJson().Should().Contain("Health");
        report.ToMarkdown().Should().Contain("| A.First |");
        FluentActions.Invoking(() => new SensitiveDataDiscoveryReport(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => SensitiveDataDiscovery.Scan((System.Reflection.Assembly)null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => SensitiveDataDiscovery.Scan((IEnumerable<System.Reflection.Assembly>)null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SensitiveFlowOptions_DevelopmentAndAuditOnlyProfiles_AddExpectedRules()
    {
        var development = new SensitiveFlowOptions().UseProfile(SensitiveFlowProfile.Development);
        var auditOnly = new SensitiveFlowOptions().UseProfile(SensitiveFlowProfile.AuditOnly);

        development.Policies.Find(DataCategory.Contact)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.RedactInJson);
        development.Policies.Find(DataCategory.Identification)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.MaskInLogs);
        auditOnly.Policies.Find(DataCategory.Other)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.AuditOnChange);
        auditOnly.Policies.Find(SensitiveDataCategory.Other)!.Actions.Should().HaveFlag(SensitiveFlowPolicyAction.RequireAudit);
    }

    [Fact]
    public void SensitiveFlowCorrelation_DefaultsToNull()
    {
        SensitiveFlowCorrelation.Current = null;

        SensitiveFlowCorrelation.Current.Should().BeNull();
        new AuditCorrelationSnapshot().CorrelationId.Should().BeNull();
    }
}
