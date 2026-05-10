using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Tests;

public sealed class CoreValueObjectTests
{
    [Fact]
    public void RetentionDataAttribute_GetExpirationDate_AppliesYearsAndMonths()
    {
        var attribute = new RetentionDataAttribute
        {
            Years = 1,
            Months = 2,
            Policy = RetentionPolicy.DeleteOnExpiration,
        };
        var from = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);

        var expires = attribute.GetExpirationDate(from);

        expires.Should().Be(new DateTimeOffset(2027, 3, 10, 0, 0, 0, TimeSpan.Zero));
        attribute.Policy.Should().Be(RetentionPolicy.DeleteOnExpiration);
    }

    [Fact]
    public void RetentionDataAttribute_RejectsNegativePeriods()
    {
        var attribute = new RetentionDataAttribute();

        attribute.Invoking(a => a.Years = -1)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Years*zero or positive*");

        attribute.Invoking(a => a.Months = -1)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Months*zero or positive*");
    }

    [Fact]
    public void DataNotFoundException_ExposesLookupContext()
    {
        var exception = new DataNotFoundException("Customer", "123");

        exception.Entity.Should().Be("Customer");
        exception.Id.Should().Be("123");
        exception.Message.Should().Contain("Customer").And.Contain("123");
    }

    [Fact]
    public void RetentionExpiredException_ExposesExpirationContext()
    {
        var expiredAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z");

        var exception = new RetentionExpiredException("Customer", "Email", expiredAt);

        exception.Entity.Should().Be("Customer");
        exception.Field.Should().Be("Email");
        exception.ExpiredAt.Should().Be(expiredAt);
        exception.Message.Should().Contain("Customer").And.Contain("Email");
    }

    [Fact]
    public void AuditSnapshot_ExposesDefaultAndProvidedValues()
    {
        var timestamp = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var snapshot = new AuditSnapshot
        {
            Id = id,
            DataSubjectId = "subject-1",
            Aggregate = "Customer",
            AggregateId = "customer-1",
            Operation = AuditOperation.Create,
            Timestamp = timestamp,
            ActorId = "actor-1",
            IpAddressToken = "ip-token",
            BeforeJson = null,
            AfterJson = "{\"name\":\"Alice\"}",
        };

        snapshot.Id.Should().Be(id);
        snapshot.DataSubjectId.Should().Be("subject-1");
        snapshot.Aggregate.Should().Be("Customer");
        snapshot.AggregateId.Should().Be("customer-1");
        snapshot.Operation.Should().Be(AuditOperation.Create);
        snapshot.Timestamp.Should().Be(timestamp);
        snapshot.ActorId.Should().Be("actor-1");
        snapshot.IpAddressToken.Should().Be("ip-token");
        snapshot.BeforeJson.Should().BeNull();
        snapshot.AfterJson.Should().Be("{\"name\":\"Alice\"}");

        var defaultSnapshot = new AuditSnapshot
        {
            DataSubjectId = "subject-2",
            Aggregate = "Customer",
            AggregateId = "customer-2",
        };

        defaultSnapshot.Id.Should().NotBeEmpty();
        defaultSnapshot.Operation.Should().Be(AuditOperation.Update);
        defaultSnapshot.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
