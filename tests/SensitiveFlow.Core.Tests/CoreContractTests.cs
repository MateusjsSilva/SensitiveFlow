using FluentAssertions;
using NSubstitute;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Discovery;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Export;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;
using Xunit;

namespace SensitiveFlow.Core.Tests;

public sealed class CoreContractTests
{
    [Fact]
    public void PersonalDataAttribute_DefaultsToOtherCategory()
    {
        var attribute = new PersonalDataAttribute();
        attribute.Category.Should().Be(DataCategory.Other);
    }

    [Fact]
    public void SensitiveDataAttribute_DefaultsToOtherCategory()
    {
        var attribute = new SensitiveDataAttribute();
        attribute.Category.Should().Be(SensitiveDataCategory.Other);
        attribute.Sensitivity.Should().Be(DataSensitivity.High);
    }

    [Fact]
    public void SensitiveDataAttribute_AcceptsProperSensitiveCategory()
    {
        var attribute = new SensitiveDataAttribute { Category = SensitiveDataCategory.Health };
        attribute.Category.Should().Be(SensitiveDataCategory.Health);
    }

    [Fact]
    public void DataCategory_DoesNotContainSensitiveCategories()
    {
        var names = Enum.GetNames<DataCategory>();
        names.Should().NotContain(["Health", "Biometric", "Genetic", "Ethnicity",
            "PoliticalOpinion", "ReligiousBelief", "SexualOrientation"]);
    }

    [Fact]
    public void SensitiveDataCategory_ContainsAllExpectedCategories()
    {
        var names = Enum.GetNames<SensitiveDataCategory>();
        names.Should().Contain([
            nameof(SensitiveDataCategory.Health),
            nameof(SensitiveDataCategory.Biometric),
            nameof(SensitiveDataCategory.Genetic),
            nameof(SensitiveDataCategory.Ethnicity),
            nameof(SensitiveDataCategory.PoliticalOpinion),
            nameof(SensitiveDataCategory.ReligiousBelief),
            nameof(SensitiveDataCategory.SexualOrientation),
        ]);
    }

    [Fact]
    public void RetentionDataAttribute_GetExpirationDate_UsesCalendarAccurateArithmetic()
    {
        var attribute = new RetentionDataAttribute { Years = 1, Months = 2 };
        var from = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);

        var expiration = attribute.GetExpirationDate(from);

        expiration.Should().Be(new DateTimeOffset(2025, 3, 31, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RetentionDataAttribute_GetExpirationDate_HandlesLeapYear()
    {
        var attribute = new RetentionDataAttribute { Years = 1 };
        var from = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero);

        var expiration = attribute.GetExpirationDate(from);

        expiration.Should().Be(new DateTimeOffset(2025, 2, 28, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RetentionDataAttribute_NegativeYears_Throws()
    {
        // §4.1.5: a negative retention silently produced an expiration in the past,
        // making the evaluator believe every annotated field was already expired.
        var act = () => new RetentionDataAttribute { Years = -1 };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RetentionDataAttribute_NegativeMonths_Throws()
    {
        var act = () => new RetentionDataAttribute { Months = -3 };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RetentionPolicy_UsesExpirationNaming()
    {
        Enum.GetNames<RetentionPolicy>().Should().Contain(
            nameof(RetentionPolicy.AnonymizeOnExpiration),
            nameof(RetentionPolicy.DeleteOnExpiration),
            nameof(RetentionPolicy.BlockOnExpiration));
    }

    [Fact]
    public void AuditRecord_HasUniqueIdByDefault()
    {
        var r1 = new AuditRecord { DataSubjectId = "u1", Entity = "E", Field = "F" };
        var r2 = new AuditRecord { DataSubjectId = "u2", Entity = "E", Field = "F" };

        r1.Id.Should().NotBeEmpty();
        r2.Id.Should().NotBeEmpty();
        r1.Id.Should().NotBe(r2.Id);
    }

    [Fact]
    public void AuditRecord_CapturesTypedAuditOperationAndContext()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var record = new AuditRecord
        {
            DataSubjectId  = "user-123",
            Entity         = "Customer",
            Field          = "Email",
            Operation      = AuditOperation.Export,
            Timestamp      = timestamp,
            ActorId        = "operator-1",
            IpAddressToken = "pseudonymized-token",
            Details        = "Export requested by data subject.",
        };

        record.DataSubjectId.Should().Be("user-123");
        record.Operation.Should().Be(AuditOperation.Export);
        record.ActorId.Should().Be("operator-1");
        record.IpAddressToken.Should().Be("pseudonymized-token");
        record.Details.Should().Be("Export requested by data subject.");
    }

    [Fact]
    public void AuditOperation_ContainsAllExpectedValues()
    {
        Enum.GetNames<AuditOperation>().Should().Contain([
            nameof(AuditOperation.Access),
            nameof(AuditOperation.Create),
            nameof(AuditOperation.Update),
            nameof(AuditOperation.Delete),
            nameof(AuditOperation.Export),
            nameof(AuditOperation.Anonymize),
            nameof(AuditOperation.Pseudonymize),
        ]);
    }

    [Fact]
    public void PseudonymizerContract_ExposesReverseMethod()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize("123.456.789-09").Returns("token-1");
        pseudonymizer.Reverse("token-1").Returns("123.456.789-09");

        var token    = pseudonymizer.Pseudonymize("123.456.789-09");
        var original = pseudonymizer.Reverse(token);

        original.Should().Be("123.456.789-09");
    }

    [Fact]
    public void DataNotFoundException_ExposesEntityAndId()
    {
        var ex = new DataNotFoundException("Customer", "customer-123");

        ex.Entity.Should().Be("Customer");
        ex.Id.Should().Be("customer-123");
        ex.Message.Should().Contain("Customer").And.Contain("customer-123");
    }

    [Fact]
    public void RetentionExpiredException_ExposesFields()
    {
        var expiredAt = DateTimeOffset.UtcNow;
        var ex = new RetentionExpiredException("Order", "CardData", expiredAt);

        ex.Entity.Should().Be("Order");
        ex.Field.Should().Be("CardData");
        ex.ExpiredAt.Should().Be(expiredAt);
    }

    [Fact]
    public async Task AuditStore_Contract_AppendAndQuery()
    {
        var store = Substitute.For<IAuditStore>();
        var record = new AuditRecord { DataSubjectId = "u1", Entity = "E", Field = "F" };

        await store.AppendAsync(record);
        await store.QueryAsync(cancellationToken: default);
        await store.QueryByDataSubjectAsync("u1", cancellationToken: default);

        await store.Received(1).AppendAsync(record);
        await store.Received(1).QueryAsync(cancellationToken: default);
        await store.Received(1).QueryByDataSubjectAsync("u1", cancellationToken: default);
    }

    [Fact]
    public void IAuditContext_Contract_ExposesActorAndIpToken()
    {
        var ctx = Substitute.For<IAuditContext>();
        ctx.ActorId.Returns("actor-1");
        ctx.IpAddressToken.Returns("token-x");

        ctx.ActorId.Should().Be("actor-1");
        ctx.IpAddressToken.Should().Be("token-x");
    }

    [Fact]
    public void SensitiveFlowOptions_StrictProfile_RequiresAuditForSensitiveCategories()
    {
        var options = new SensitiveFlowOptions().UseProfile(SensitiveFlowProfile.Strict);

        var rule = options.Policies.Find(SensitiveDataCategory.Health);

        rule.Should().NotBeNull();
        (rule!.Actions & SensitiveFlowPolicyAction.OmitInJson).Should().Be(SensitiveFlowPolicyAction.OmitInJson);
        (rule.Actions & SensitiveFlowPolicyAction.RequireAudit).Should().Be(SensitiveFlowPolicyAction.RequireAudit);
    }

    [Fact]
    public void SensitiveDataDiscovery_Scan_ReturnsAnnotatedMembers()
    {
        var report = SensitiveDataDiscovery.Scan(typeof(CoreContractTests).Assembly);

        report.Entries.Should().Contain(e => e.TypeName == nameof(DiscoveryCustomer)
            && e.MemberName == nameof(DiscoveryCustomer.Email)
            && e.Category == DataCategory.Contact);
        report.ToMarkdown().Should().Contain("DiscoveryCustomer.Email");
        report.ToJson().Should().Contain("DiscoveryCustomer");
    }

    [Fact]
    public void SensitiveFlowExceptionSanitizer_RemovesCommonRawValues()
    {
        var exception = new SensitiveFlowConfigurationException(
            "Failed for alice@example.com and 123.456.789-09.",
            "SF_CONFIG_001");

        var sanitized = SensitiveFlowExceptionSanitizer.Sanitize(exception);

        sanitized.Code.Should().Be("SF_CONFIG_001");
        sanitized.Message.Should().NotContain("alice@example.com");
        sanitized.Message.Should().NotContain("123.456.789-09");
    }

    [Fact]
    public void CsvDataExportFormatter_PrefixesFormulaValues()
    {
        var formatter = new CsvDataExportFormatter();

        var csv = formatter.Format([
            new Dictionary<string, object?> { ["Name"] = "=cmd" },
        ]);

        csv.Should().Contain("\"'=cmd\"");
    }

    private sealed class DiscoveryCustomer
    {
        [PersonalData(Category = DataCategory.Contact)]
        [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string Email { get; set; } = string.Empty;
    }
}
