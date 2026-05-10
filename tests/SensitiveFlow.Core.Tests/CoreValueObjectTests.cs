using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Exceptions;

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
}
