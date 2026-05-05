using FluentAssertions;
using LGPD.NET.Core.Attributes;
using LGPD.NET.Core.Enums;
using LGPD.NET.Core.Exceptions;
using LGPD.NET.Core.Interfaces;
using LGPD.NET.Core.Models;
using NSubstitute;
using Xunit;

namespace LGPD.NET.Core.Tests;

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
    public void SensitiveDataAttribute_UsesExplicitSensitiveLegalBasisByDefault()
    {
        var attribute = new SensitiveDataAttribute();

        attribute.Category.Should().Be(DataCategory.Other);
        attribute.SensitiveLegalBasis.Should().Be(SensitiveLegalBasis.ExplicitConsent);
        attribute.Purpose.Should().Be(ProcessingPurpose.ServiceProvision);
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
    public void RetentionDataAttribute_ComposesYearsAndMonthsIntoPeriod()
    {
        var attribute = new RetentionDataAttribute
        {
            Years = 1,
            Months = 2
        };

        attribute.Policy.Should().Be(RetentionPolicy.AnonymizeOnExpiration);
        attribute.Period.Should().Be(TimeSpan.FromDays(425));
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
        var notifiedAt = DateTimeOffset.UtcNow;
        var record = new IncidentRecord
        {
            Id = "INC-001",
            Nature = IncidentNature.UnauthorizedAccess,
            Severity = IncidentSeverity.High,
            Status = IncidentStatus.Notified,
            Summary = "Unauthorized access to customer records.",
            AffectedData = [DataCategory.Identification, DataCategory.Financial],
            EstimatedAffectedDataSubjects = 1500,
            RemediationAction = "Access revoked and credentials rotated.",
            AnpdNotificationGeneratedAt = notifiedAt
        };

        record.Nature.Should().Be(IncidentNature.UnauthorizedAccess);
        record.AffectedData.Should().ContainInOrder(DataCategory.Identification, DataCategory.Financial);
        record.EstimatedAffectedDataSubjects.Should().Be(1500);
        record.RemediationAction.Should().Be("Access revoked and credentials rotated.");
        record.AnpdNotificationGeneratedAt.Should().Be(notifiedAt);
    }

    [Fact]
    public void AuditRecord_CapturesTypedAuditOperationAndContext()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var record = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Export,
            Timestamp = timestamp,
            ActorId = "operator-1",
            IpAddress = "127.0.0.1",
            Details = "Export requested by data subject."
        };

        record.DataSubjectId.Should().Be("user-123");
        record.Entity.Should().Be("Customer");
        record.Field.Should().Be("Email");
        record.Operation.Should().Be(AuditOperation.Export);
        record.Timestamp.Should().Be(timestamp);
        record.ActorId.Should().Be("operator-1");
        record.IpAddress.Should().Be("127.0.0.1");
        record.Details.Should().Be("Export requested by data subject.");
    }

    [Fact]
    public void ConsentRecord_CapturesEvidencePolicyVersionAndLegalBasis()
    {
        var collectedAt = DateTimeOffset.UtcNow;
        var expiresAt = collectedAt.AddYears(1);
        var record = new ConsentRecord
        {
            Id = "consent-1",
            DataSubjectId = "user-123",
            Purpose = ProcessingPurpose.Marketing,
            LegalBasis = LegalBasis.Consent,
            CollectedAt = collectedAt,
            ExpiresAt = expiresAt,
            Evidence = "Checked signup box.",
            CollectionChannel = "web",
            PrivacyPolicyVersion = "2.1",
            Revoked = true,
            RevokedAt = collectedAt.AddDays(10)
        };

        record.Id.Should().Be("consent-1");
        record.DataSubjectId.Should().Be("user-123");
        record.Purpose.Should().Be(ProcessingPurpose.Marketing);
        record.LegalBasis.Should().Be(LegalBasis.Consent);
        record.CollectedAt.Should().Be(collectedAt);
        record.ExpiresAt.Should().Be(expiresAt);
        record.Evidence.Should().Be("Checked signup box.");
        record.CollectionChannel.Should().Be("web");
        record.PrivacyPolicyVersion.Should().Be("2.1");
        record.Revoked.Should().BeTrue();
        record.RevokedAt.Should().Be(collectedAt.AddDays(10));
    }

    [Fact]
    public void DataSubjectRequest_CapturesLifecycleFields()
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var request = new DataSubjectRequest
        {
            Id = "dsr-1",
            DataSubjectId = "user-123",
            Type = DataSubjectRequestType.Access,
            Status = DataSubjectRequestStatus.InProgress,
            RequestedAt = requestedAt,
            ResponseDueAt = requestedAt.AddDays(15),
            CompletedAt = requestedAt.AddDays(2),
            Notes = "Identity validated."
        };

        request.Id.Should().Be("dsr-1");
        request.DataSubjectId.Should().Be("user-123");
        request.Type.Should().Be(DataSubjectRequestType.Access);
        request.Status.Should().Be(DataSubjectRequestStatus.InProgress);
        request.RequestedAt.Should().Be(requestedAt);
        request.ResponseDueAt.Should().Be(requestedAt.AddDays(15));
        request.CompletedAt.Should().Be(requestedAt.AddDays(2));
        request.Notes.Should().Be("Identity validated.");
    }

    [Fact]
    public void ProcessingOperationRecord_CapturesDataMapAndRipdInputs()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var sharing = new DataSharingRecord
        {
            Recipient = "Email provider",
            Purpose = "Transactional-only delivery",
            Country = TransferCountry.UnitedStates,
            Mechanism = SafeguardMechanism.ContractualClauses
        };

        var operation = new ProcessingOperationRecord
        {
            Id = "operation-1",
            Entity = "Customer",
            Fields = ["Email", "Phone"],
            Purpose = ProcessingPurpose.ContractCommunication,
            LegalBasis = LegalBasis.ContractPerformance,
            RetentionYears = 5,
            Sharing = [sharing],
            CreatedAt = createdAt,
            Description = "Customer contractual communication."
        };

        operation.Id.Should().Be("operation-1");
        operation.Entity.Should().Be("Customer");
        operation.Fields.Should().ContainInOrder("Email", "Phone");
        operation.Purpose.Should().Be(ProcessingPurpose.ContractCommunication);
        operation.LegalBasis.Should().Be(LegalBasis.ContractPerformance);
        operation.RetentionYears.Should().Be(5);
        operation.Sharing.Should().ContainSingle().Which.Should().Be(sharing);
        operation.CreatedAt.Should().Be(createdAt);
        operation.Description.Should().Be("Customer contractual communication.");
    }

    [Fact]
    public void PseudonymizerContract_ExposesReverseMethod()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize("123.456.789-09").Returns("token-1");
        pseudonymizer.Reverse("token-1").Returns("123.456.789-09");

        var token = pseudonymizer.Pseudonymize("123.456.789-09");
        var original = pseudonymizer.Reverse(token);

        original.Should().Be("123.456.789-09");
    }

    [Fact]
    public void CoreExceptions_ExposeContextProperties()
    {
        var consent = new ConsentNotFoundException("user-123", ProcessingPurpose.Marketing);
        var data = new DataNotFoundException("Customer", "customer-123");
        var transfer = new InternationalTransferNotAllowedException(
            TransferCountry.UnitedStates,
            SafeguardMechanism.ContractualClauses,
            "missing DPA");
        var expiredAt = DateTimeOffset.UtcNow;
        var retention = new RetentionExpiredException("Order", "CardData", expiredAt);

        consent.DataSubjectId.Should().Be("user-123");
        consent.Purpose.Should().Be(ProcessingPurpose.Marketing);
        data.Entity.Should().Be("Customer");
        data.Id.Should().Be("customer-123");
        transfer.Country.Should().Be(TransferCountry.UnitedStates);
        transfer.Mechanism.Should().Be(SafeguardMechanism.ContractualClauses);
        transfer.Reason.Should().Be("missing DPA");
        retention.Entity.Should().Be("Order");
        retention.Field.Should().Be("CardData");
        retention.ExpiredAt.Should().Be(expiredAt);
    }

    [Fact]
    public async Task StoreContracts_ExposeQueriesNeededByModules()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var auditStore = Substitute.For<IAuditStore>();
        var consentStore = Substitute.For<IConsentStore>();
        var incidentStore = Substitute.For<IIncidentStore>();
        var inventory = Substitute.For<IProcessingInventory>();

        await auditStore.QueryAsync(cancellationToken: cancellationToken);
        await consentStore.ListByDataSubjectAsync("user-123", cancellationToken);
        await incidentStore.QueryAsync(IncidentStatus.Assessed, cancellationToken: cancellationToken);
        await inventory.ListAsync(cancellationToken);

        await auditStore.Received(1).QueryAsync(cancellationToken: cancellationToken);
        await consentStore.Received(1).ListByDataSubjectAsync("user-123", cancellationToken);
        await incidentStore.Received(1).QueryAsync(IncidentStatus.Assessed, cancellationToken: cancellationToken);
        await inventory.Received(1).ListAsync(cancellationToken);
    }
}
