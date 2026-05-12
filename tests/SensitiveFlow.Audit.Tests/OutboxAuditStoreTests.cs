using FluentAssertions;
using NSubstitute;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class OutboxAuditStoreTests
{
    [Fact]
    public async Task AppendAsync_WhenInnerSupportsTransaction_ExecutesInTransaction()
    {
        var inner = Substitute.For<IAuditStore, IAuditStoreTransaction>();
        var outbox = Substitute.For<IAuditOutbox>();
        var sut = new OutboxAuditStore(inner, outbox);
        var record = SampleRecord();

        await sut.AppendAsync(record);

        await ((IAuditStoreTransaction)inner).Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendAsync_WhenInnerDoesNotSupportTransaction_ExecutesDirectly()
    {
        var inner = Substitute.For<IAuditStore>();
        var outbox = Substitute.For<IAuditOutbox>();
        var sut = new OutboxAuditStore(inner, outbox);
        var record = SampleRecord();

        await sut.AppendAsync(record);

        await inner.Received(1).AppendAsync(record, Arg.Any<CancellationToken>());
        await outbox.Received(1).EnqueueAsync(record, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_WhenInnerIsBatchAndSupportsTransaction_ExecutesInTransaction()
    {
        var inner = Substitute.For<IBatchAuditStore, IAuditStoreTransaction>();
        var outbox = Substitute.For<IAuditOutbox>();
        var sut = new OutboxAuditStore(inner, outbox);
        var records = new[] { SampleRecord(), SampleRecord() };

        await sut.AppendRangeAsync(records);

        await ((IAuditStoreTransaction)inner).Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_WhenInnerIsBatchWithoutTransaction_DelegatesBatch()
    {
        var inner = Substitute.For<IBatchAuditStore>();
        var outbox = Substitute.For<IAuditOutbox>();
        var sut = new OutboxAuditStore(inner, outbox);
        var records = new[] { SampleRecord(), SampleRecord() };

        await sut.AppendRangeAsync(records);

        await inner.Received(1).AppendRangeAsync(records, Arg.Any<CancellationToken>());
        await outbox.Received(2).EnqueueAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_WhenInnerIsNotBatchWithoutTransaction_FallsBackToIndividualAppends()
    {
        var inner = Substitute.For<IAuditStore>();
        var outbox = Substitute.For<IAuditOutbox>();
        var sut = new OutboxAuditStore(inner, outbox);
        var records = new[] { SampleRecord(), SampleRecord() };

        await sut.AppendRangeAsync(records);

        await inner.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
        await outbox.Received(2).EnqueueAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_DelegatesToInner()
    {
        var inner = Substitute.For<IAuditStore>();
        var outbox = Substitute.For<IAuditOutbox>();
        var sut = new OutboxAuditStore(inner, outbox);
        var expected = new[] { SampleRecord() };
        inner.QueryAsync(null, null, 0, 100, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await sut.QueryAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task QueryByDataSubjectAsync_DelegatesToInner()
    {
        var inner = Substitute.For<IAuditStore>();
        var outbox = Substitute.For<IAuditOutbox>();
        var sut = new OutboxAuditStore(inner, outbox);
        var expected = new[] { SampleRecord() };
        inner.QueryByDataSubjectAsync("subject-1", null, null, 0, 100, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await sut.QueryByDataSubjectAsync("subject-1");

        result.Should().BeEquivalentTo(expected);
    }

    private static AuditRecord SampleRecord() => new()
    {
        DataSubjectId = "subject-1",
        Entity = "Customer",
        Field = "Email",
        Operation = AuditOperation.Update,
    };
}
