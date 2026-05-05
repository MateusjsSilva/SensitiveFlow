using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using NSubstitute;
using Xunit;

namespace SensitiveFlow.Core.Tests;

public sealed class CoreContractTests
{
    [Fact]
    public void PersonalDataAttribute_DefaultsToConsentAndServiceProvision()
    {
        var attribute = new PersonalDataAttribute();

        attribute.Category.Should().Be(DataCategory.Other);
        attribute.LegalBasis.Should().Be(LegalBasis.Consent);
        attribute.Purpose.Should().Be(ProcessingPurpose.ServiceProvision);
    }

    [Fact]
    public void SensitiveDataAttribute_UsesSensitiveDataCategoryAndExplicitConsentByDefault()
    {
        var attribute = new SensitiveDataAttribute();

        // Category is now SensitiveDataCategory — not DataCategory
        attribute.Category.Should().Be(SensitiveDataCategory.Other);
        attribute.SensitiveLegalBasis.Should().Be(SensitiveLegalBasis.ExplicitConsent);
        attribute.Purpose.Should().Be(ProcessingPurpose.ServiceProvision);
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
        // Health, Biometric, etc. must not appear in DataCategory — they belong in SensitiveDataCategory.
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
    public void InternationalTransferAttribute_UsesContractualClausesByDefault()
    {
        var attribute = new InternationalTransferAttribute();

        attribute.Country.Should().Be(TransferCountry.Other);
        attribute.Mechanism.Should().Be(SafeguardMechanism.ContractualClauses);
        attribute.Recipient.Should().BeNull();
    }

    [Fact]
    public void RetentionDataAttribute_GetExpirationDate_UsesCalendarAccurateArithmetic()
    {
        var attribute = new RetentionDataAttribute { Years = 1, Months = 2 };
        var from = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);

        var expiration = attribute.GetExpirationDate(from);

        // AddYears(1).AddMonths(2) from 2024-01-31 = 2025-03-31
        expiration.Should().Be(new DateTimeOffset(2025, 3, 31, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RetentionDataAttribute_GetExpirationDate_HandlesLeapYear()
    {
        var attribute = new RetentionDataAttribute { Years = 1 };
        // 2024 is a leap year; 2024-02-29 + 1 year = 2025-02-28
        var from = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero);

        var expiration = attribute.GetExpirationDate(from);

        expiration.Should().Be(new DateTimeOffset(2025, 2, 28, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void EraseDataAttribute_DefaultsToDeleteBehavior()
    {
        var attribute = new EraseDataAttribute();

        attribute.AnonymizeInsteadOfDelete.Should().BeFalse();
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
    public void ProcessingPurpose_ContainsContractCommunication()
    {
        ProcessingPurpose.ContractCommunication.Should().BeDefined();
    }

    [Fact]
    public void IncidentRecord_CapturesBreachNotificationContractData()
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var notifiedAt  = generatedAt.AddHours(1);

        var record = new IncidentRecord
        {
            Id                            = "INC-001",
            Nature                        = IncidentNature.UnauthorizedAccess,
            Severity                      = IncidentSeverity.High,
            RiskLevel                     = RiskLevel.High,
            Status                        = IncidentStatus.Notified,
            Summary                       = "Unauthorized access to customer records.",
            AffectedData                  = [DataCategory.Identification, DataCategory.Financial],
            EstimatedAffectedDataSubjects = 1500,
            RemediationAction             = "Access revoked and credentials rotated.",
            AuthorityNotificationGeneratedAt   = generatedAt,
            AuthorityNotifiedAt                = notifiedAt,
        };

        record.Nature.Should().Be(IncidentNature.UnauthorizedAccess);
        record.RiskLevel.Should().Be(RiskLevel.High);
        record.AffectedData.Should().ContainInOrder(DataCategory.Identification, DataCategory.Financial);
        record.EstimatedAffectedDataSubjects.Should().Be(1500);
        record.AuthorityNotificationGeneratedAt.Should().Be(generatedAt);
        record.AuthorityNotifiedAt.Should().Be(notifiedAt);
    }

    [Fact]
    public void AuditRecord_HasUniqueIdByDefault()
    {
        var r1 = new AuditRecord { DataSubjectId = "u1", Entity = "E", Field = "F" };
        var r2 = new AuditRecord { DataSubjectId = "u2", Entity = "E", Field = "F" };

        r1.Id.Should().NotBeNullOrEmpty();
        r2.Id.Should().NotBeNullOrEmpty();
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
    public void ConsentRecord_LegalBasisMustBeConsent()
    {
        // Valid — LegalBasis.Consent is the only accepted value
        var act = () => new ConsentRecord
        {
            Id            = "c1",
            DataSubjectId = "u1",
            Purpose       = ProcessingPurpose.Marketing,
            LegalBasis    = LegalBasis.Consent,
        };
        act.Should().NotThrow();

        // Invalid — any other basis throws ArgumentException
        var invalid = () => new ConsentRecord
        {
            Id            = "c2",
            DataSubjectId = "u1",
            Purpose       = ProcessingPurpose.Marketing,
            LegalBasis    = LegalBasis.LegalObligation,
        };
        invalid.Should().Throw<ArgumentException>().WithMessage("*LegalBasis.Consent*");
    }

    [Fact]
    public void ConsentRecord_CapturesEvidencePolicyVersionAndLifecycle()
    {
        var collectedAt = DateTimeOffset.UtcNow;
        var record = new ConsentRecord
        {
            Id                   = "consent-1",
            DataSubjectId        = "user-123",
            Purpose              = ProcessingPurpose.Marketing,
            CollectedAt          = collectedAt,
            ExpiresAt            = collectedAt.AddYears(1),
            Evidence             = "Checked signup box.",
            CollectionChannel    = "web",
            PrivacyPolicyVersion = "2.1",
            Revoked              = true,
            RevokedAt            = collectedAt.AddDays(10),
        };

        record.LegalBasis.Should().Be(LegalBasis.Consent);
        record.Evidence.Should().Be("Checked signup box.");
        record.Revoked.Should().BeTrue();
        record.RevokedAt.Should().Be(collectedAt.AddDays(10));
    }

    [Fact]
    public void DataSubjectRequest_CapturesRejectionReason()
    {
        var request = new DataSubjectRequest
        {
            Id              = "dsr-1",
            DataSubjectId   = "user-123",
            Type            = DataSubjectRequestType.Deletion,
            Status          = DataSubjectRequestStatus.Rejected,
            RejectionReason = "Data is required for an ongoing legal proceeding.",
        };

        request.RejectionReason.Should().Contain("ongoing legal proceeding");
        request.Status.Should().Be(DataSubjectRequestStatus.Rejected);
    }

    [Fact]
    public void DataSubjectRequest_CapturesLifecycleFields()
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var request = new DataSubjectRequest
        {
            Id            = "dsr-2",
            DataSubjectId = "user-123",
            DataSubjectKind = DataSubjectKind.Adolescent,
            Type          = DataSubjectRequestType.Access,
            Status        = DataSubjectRequestStatus.InProgress,
            RequestedAt   = requestedAt,
            ResponseDueAt = requestedAt.AddDays(15),
            CompletedAt   = requestedAt.AddDays(2),
            Notes         = "Identity validated.",
        };

        request.DataSubjectKind.Should().Be(DataSubjectKind.Adolescent);
        request.ResponseDueAt.Should().Be(requestedAt.AddDays(15));
        request.Notes.Should().Be("Identity validated.");
    }

    [Fact]
    public void ProcessingOperationRecord_CapturesDataMapAndRipdInputs()
    {
        var sharing = new DataSharingRecord
        {
            Recipient = "Email provider",
            Purpose   = "Transactional-only delivery",
            Country   = TransferCountry.UnitedStates,
            Mechanism = SafeguardMechanism.ContractualClauses,
        };

        var operation = new ProcessingOperationRecord
        {
            Id         = "operation-1",
            Entity     = "Customer",
            AgentRole  = ProcessingAgentRole.Controller,
            Fields     = ["Email", "Phone"],
            Purpose    = ProcessingPurpose.ContractCommunication,
            LegalBasis = LegalBasis.ContractPerformance,
            Principles = [ProcessingPrinciple.Purpose, ProcessingPrinciple.Necessity],
            Sharing    = [sharing],
        };

        operation.LegalBasis.Should().Be(LegalBasis.ContractPerformance);
        operation.Fields.Should().ContainInOrder("Email", "Phone");
        operation.Sharing.Should().ContainSingle();
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
    public void CoreExceptions_ExposeContextProperties()
    {
        var consent   = new ConsentNotFoundException("user-123", ProcessingPurpose.Marketing);
        var data      = new DataNotFoundException("Customer", "customer-123");
        var transfer  = new InternationalTransferNotAllowedException(
            TransferCountry.UnitedStates, SafeguardMechanism.ContractualClauses, "missing DPA");
        var expiredAt = DateTimeOffset.UtcNow;
        var retention = new RetentionExpiredException("Order", "CardData", expiredAt);

        consent.DataSubjectId.Should().Be("user-123");
        data.Entity.Should().Be("Customer");
        transfer.Country.Should().Be(TransferCountry.UnitedStates);
        retention.ExpiredAt.Should().Be(expiredAt);
    }

    [Fact]
    public void InternationalTransferNotAllowedException_UsesDefaultMessageWhenReasonIsNull()
    {
        var transfer = new InternationalTransferNotAllowedException(TransferCountry.UnitedStates);

        transfer.Mechanism.Should().BeNull();
        transfer.Message.Should().Contain("safeguard mechanism missing or invalid");
    }

    [Fact]
    public async Task StoreContracts_ExposeQueriesNeededByModules()
    {
        var ct           = new CancellationTokenSource().Token;
        var auditStore   = Substitute.For<IAuditStore>();
        var consentStore = Substitute.For<IConsentStore>();
        var incidentStore = Substitute.For<IIncidentStore>();
        var inventory    = Substitute.For<IProcessingInventory>();

        consentStore.RevokeAsync(Arg.Any<string>(), Arg.Any<ProcessingPurpose>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await auditStore.QueryAsync(cancellationToken: ct);
        await consentStore.ListByDataSubjectAsync("user-123", ct);
        var revoked = await consentStore.RevokeAsync("user-123", ProcessingPurpose.Marketing, ct);
        await incidentStore.QueryAsync(IncidentStatus.Assessed, cancellationToken: ct);
        await inventory.ListAsync(ct);

        revoked.Should().BeTrue();
        await auditStore.Received(1).QueryAsync(cancellationToken: ct);
        await consentStore.Received(1).RevokeAsync("user-123", ProcessingPurpose.Marketing, ct);
        await incidentStore.Received(1).QueryAsync(IncidentStatus.Assessed, cancellationToken: ct);
    }

    [Fact]
    public void CrossCuttingPrivacyEnums_CoverSharedLegalVocabulary()
    {
        Enum.GetNames<ProcessingAgentRole>().Should().Contain(
            nameof(ProcessingAgentRole.Controller),
            nameof(ProcessingAgentRole.Processor),
            nameof(ProcessingAgentRole.Dpo));

        Enum.GetNames<DataSubjectKind>().Should().Contain(
            nameof(DataSubjectKind.Adult),
            nameof(DataSubjectKind.Child),
            nameof(DataSubjectKind.Adolescent));

        Enum.GetNames<RiskLevel>().Should().Contain(
            nameof(RiskLevel.Low),
            nameof(RiskLevel.Medium),
            nameof(RiskLevel.High),
            nameof(RiskLevel.Critical));
    }
}

