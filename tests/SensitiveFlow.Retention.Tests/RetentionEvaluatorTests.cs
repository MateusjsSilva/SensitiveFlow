using FluentAssertions;
using NSubstitute;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Retention.Contracts;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Tests;

public sealed class RetentionEvaluatorTests
{
    private sealed class UserEntity
    {
        public string Id { get; set; } = "u1";

        [RetentionData(Years = 1, Policy = RetentionPolicy.DeleteOnExpiration)]
        public string Email { get; set; } = "test@example.com";

        public string Name { get; set; } = "Test";
    }

    private sealed class NoRetentionEntity
    {
        public string Data { get; set; } = "value";
    }

    [Fact]
    public async Task EvaluateAsync_NoExpiredFields_DoesNothing()
    {
        var evaluator = new RetentionEvaluator([]);
        var entity = new UserEntity();

        await evaluator.Invoking(e => e.EvaluateAsync(entity, DateTimeOffset.UtcNow))
                       .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EvaluateAsync_ExpiredField_NoHandlers_ThrowsRetentionExpiredException()
    {
        var evaluator = new RetentionEvaluator([]);
        var entity = new UserEntity();
        var reference = DateTimeOffset.UtcNow.AddYears(-2);

        await evaluator.Invoking(e => e.EvaluateAsync(entity, reference))
                       .Should().ThrowAsync<RetentionExpiredException>()
                       .WithMessage("*Email*");
    }

    [Fact]
    public async Task EvaluateAsync_ExpiredField_WithHandlers_CallsHandler()
    {
        var handler = Substitute.For<IRetentionExpirationHandler>();
        var evaluator = new RetentionEvaluator([handler]);
        var entity = new UserEntity();
        var reference = DateTimeOffset.UtcNow.AddYears(-2);

        await evaluator.EvaluateAsync(entity, reference);

        await handler.Received(1).HandleAsync(
            entity,
            "Email",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_NoRetentionAttributes_NoHandlerCalled()
    {
        var handler = Substitute.For<IRetentionExpirationHandler>();
        var evaluator = new RetentionEvaluator([handler]);
        var entity = new NoRetentionEntity();

        await evaluator.EvaluateAsync(entity, DateTimeOffset.UtcNow.AddYears(-10));

        await handler.DidNotReceive().HandleAsync(
            Arg.Any<object>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_NullEntity_Throws()
    {
        var evaluator = new RetentionEvaluator([]);
        await evaluator.Invoking(e => e.EvaluateAsync(null!, DateTimeOffset.UtcNow))
                       .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_MultipleHandlers_AllCalled()
    {
        var handler1 = Substitute.For<IRetentionExpirationHandler>();
        var handler2 = Substitute.For<IRetentionExpirationHandler>();
        var evaluator = new RetentionEvaluator([handler1, handler2]);
        var entity = new UserEntity();
        var reference = DateTimeOffset.UtcNow.AddYears(-2);

        await evaluator.EvaluateAsync(entity, reference);

        await handler1.Received(1).HandleAsync(entity, "Email", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await handler2.Received(1).HandleAsync(entity, "Email", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
