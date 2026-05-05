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
        var transfer = new InternationalTransferNotAllowedException(TransferCountry.UnitedStates);
        var expiredAt = DateTimeOffset.UtcNow;
        var retention = new RetentionExpiredException("Order", "CardData", expiredAt);

        consent.DataSubjectId.Should().Be("user-123");
        consent.Purpose.Should().Be(ProcessingPurpose.Marketing);
        data.Entity.Should().Be("Customer");
        data.Id.Should().Be("customer-123");
        transfer.Country.Should().Be(TransferCountry.UnitedStates);
        retention.Entity.Should().Be("Order");
        retention.Field.Should().Be("CardData");
        retention.ExpiredAt.Should().Be(expiredAt);
    }
}
