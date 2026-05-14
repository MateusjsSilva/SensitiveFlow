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

    private sealed class ParentEntity
    {
        public NestedEntity? MissingNested { get; set; }

        public NestedEntity Nested { get; set; } = new();
    }

    private sealed class NestedEntity
    {
        [RetentionData(Years = 1, Policy = RetentionPolicy.DeleteOnExpiration)]
        public string Secret { get; set; } = "nested-secret";
    }

    private sealed class MultipleRetentionEntity
    {
        [RetentionData(Years = 1, Policy = RetentionPolicy.DeleteOnExpiration)]
        public string Field1 { get; set; } = "value1";

        [RetentionData(Years = 2, Policy = RetentionPolicy.DeleteOnExpiration)]
        public string Field2 { get; set; } = "value2";

        [RetentionData(Years = 3, Policy = RetentionPolicy.DeleteOnExpiration)]
        public string Field3 { get; set; } = "value3";
    }

    private sealed class EntityWithCollection
    {
        public List<RetentionItem> Items { get; set; } = [];
    }

    private sealed class RetentionItem
    {
        [RetentionData(Years = 1, Policy = RetentionPolicy.DeleteOnExpiration)]
        public string Data { get; set; } = "item-data";
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

    [Fact]
    public async Task EvaluateAsync_RecursesIntoNestedEntities()
    {
        var handler = Substitute.For<IRetentionExpirationHandler>();
        var evaluator = new RetentionEvaluator([handler]);
        var entity = new ParentEntity();
        var reference = DateTimeOffset.UtcNow.AddYears(-2);

        await evaluator.EvaluateAsync(entity, reference);

        await handler.Received(1).HandleAsync(
            entity.Nested,
            nameof(NestedEntity.Secret),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_MultipleExpiredFields_CollectsAllBeforeThrowing()
    {
        var evaluator = new RetentionEvaluator([]);
        var entity = new MultipleRetentionEntity();
        var reference = DateTimeOffset.UtcNow.AddYears(-5);

        var exception = await evaluator.Invoking(e => e.EvaluateAsync(entity, reference))
            .Should().ThrowAsync<RetentionExpiredException>();

        exception.WithMessage("*Field1*");
    }

    [Fact]
    public async Task EvaluateAsync_MultipleExpiredFields_WithHandlers_AllCalled()
    {
        var handler = Substitute.For<IRetentionExpirationHandler>();
        var evaluator = new RetentionEvaluator([handler]);
        var entity = new MultipleRetentionEntity();
        var reference = DateTimeOffset.UtcNow.AddYears(-5);

        await evaluator.EvaluateAsync(entity, reference);

        await handler.Received(1).HandleAsync(entity, "Field1", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await handler.Received(1).HandleAsync(entity, "Field2", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await handler.Received(1).HandleAsync(entity, "Field3", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_EvaluatesItemsInCollections()
    {
        var handler = Substitute.For<IRetentionExpirationHandler>();
        var evaluator = new RetentionEvaluator([handler]);
        var item1 = new RetentionItem { Data = "item-1" };
        var item2 = new RetentionItem { Data = "item-2" };
        var entity = new EntityWithCollection { Items = [item1, item2] };
        var reference = DateTimeOffset.UtcNow.AddYears(-2);

        await evaluator.EvaluateAsync(entity, reference);

        await handler.Received(1).HandleAsync(item1, "Data", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await handler.Received(1).HandleAsync(item2, "Data", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_HandlesEmptyCollections()
    {
        var handler = Substitute.For<IRetentionExpirationHandler>();
        var evaluator = new RetentionEvaluator([handler]);
        var entity = new EntityWithCollection { Items = [] };
        var reference = DateTimeOffset.UtcNow.AddYears(-2);

        await evaluator.Invoking(e => e.EvaluateAsync(entity, reference))
            .Should().NotThrowAsync();

        await handler.DidNotReceive().HandleAsync(
            Arg.Any<object>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }
}
