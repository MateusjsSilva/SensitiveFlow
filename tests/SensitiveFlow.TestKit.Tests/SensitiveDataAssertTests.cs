using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.TestKit.Assertions;
using Xunit.Sdk;

namespace SensitiveFlow.TestKit.Tests;

public sealed class SensitiveDataAssertTests
{
    [Fact]
    public void DoesNotLeak_WhenPayloadHasNoSensitiveValue_Passes()
    {
        var customer = new Customer { Name = "Alice", Email = "alice@example.com" };
        var payload = "{\"Name\":\"A****\",\"Email\":\"a****@example.com\"}";

        Action act = () => SensitiveDataAssert.DoesNotLeak(payload, customer);

        act.Should().NotThrow();
    }

    [Fact]
    public void DoesNotLeak_WhenPayloadContainsSensitiveValueVerbatim_Throws()
    {
        var customer = new Customer { Name = "Alice", Email = "alice@example.com" };
        var leakyPayload = "{\"Name\":\"Alice\",\"Email\":\"a****@example.com\"}";

        Action act = () => SensitiveDataAssert.DoesNotLeak(leakyPayload, customer);

        act.Should().Throw<XunitException>().WithMessage("*Name*Alice*");
    }

    [Fact]
    public void DoesNotLeak_WithEmptySensitiveValue_DoesNotMatchEverything()
    {
        var customer = new Customer { Name = string.Empty, Email = "x@y.z" };
        var payload = "anything goes here, even the entire alphabet abcdefghij";

        Action act = () => SensitiveDataAssert.DoesNotLeak(payload, customer);

        act.Should().NotThrow();
    }

    [Fact]
    public void DoesNotLeak_RejectsNullPayload()
    {
        var act = () => SensitiveDataAssert.DoesNotLeak(null!, new Customer());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DoesNotLeak_RejectsNullEntity()
    {
        var act = () => SensitiveDataAssert.DoesNotLeak("payload", (object)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DoesNotLeak_IgnoresWriteOnlyAndNonStringSensitiveProperties()
    {
        var entity = new MixedCustomer { Age = 42 };
        entity.Secret = "hidden";

        var act = () => SensitiveDataAssert.DoesNotLeak("hidden 42", entity);

        act.Should().NotThrow();
    }

    [Fact]
    public void DoesNotLeak_AcrossMultipleEntities_ChecksAll()
    {
        var customer = new Customer { Name = "Alice", Email = "ok@x.com" };
        var other = new Customer { Name = "Bob", Email = "ok@x.com" };
        var payload = "leak: Bob is here";

        Action act = () => SensitiveDataAssert.DoesNotLeak(payload, customer, other);

        act.Should().Throw<XunitException>().WithMessage("*Bob*");
    }

    [Fact]
    public void DoesNotLeak_AcrossMultipleEntities_RejectsNullEntitiesArray()
    {
        var act = () => SensitiveDataAssert.DoesNotLeak("payload", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DoesNotLeak_AcrossMultipleEntities_SkipsNullEntityEntries()
    {
        var customer = new Customer { Name = "Alice", Email = "alice@example.com" };

        var act = () => SensitiveDataAssert.DoesNotLeak("redacted", null!, customer);

        act.Should().NotThrow();
    }

    public class Customer
    {
        [PersonalData(Category = DataCategory.Identification)]
        public string Name { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;
    }

    public class MixedCustomer
    {
        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string Secret
        {
            set { _ = value; }
        }

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public int Age { get; set; }
    }
}
