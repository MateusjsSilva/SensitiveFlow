using FluentAssertions;
using SensitiveFlow.Retention.Contracts;
using Xunit;

namespace SensitiveFlow.TestKit;

/// <summary>
/// Conformance suite for <see cref="IRetentionExpirationHandler"/> implementations.
/// </summary>
public abstract class RetentionExpirationHandlerContractTests
{
    /// <summary>Creates a handler instance for the test.</summary>
    protected abstract IRetentionExpirationHandler CreateHandler();

    /// <summary>Verifies the handler accepts a minimal expired-field notification.</summary>
    [Fact]
    public async Task HandleAsync_AcceptsExpiredField()
    {
        var handler = CreateHandler();

        Func<Task> act = () => handler.HandleAsync(new { Id = "customer-1" }, "Email", DateTimeOffset.UtcNow.AddDays(-1));

        await act.Should().NotThrowAsync();
    }
}
