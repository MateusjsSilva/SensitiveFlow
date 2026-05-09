using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Audit.Tests.Stores;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class BufferedAuditStoreTests
{
    private static AuditRecord SampleRecord(string field = "Field") => new()
    {
        DataSubjectId = "subject",
        Entity = "Entity",
        Field = field,
        Operation = AuditOperation.Access,
    };

    [Fact]
    public async Task DisposeAsync_FlushesQueuedRecordsToBatchStore()
    {
        var inner = new RecordingBatchAuditStore();
        var sut = new BufferedAuditStore(inner, new BufferedAuditStoreOptions
        {
            Capacity = 10,
            MaxBatchSize = 10,
        });
        var records = new[] { SampleRecord("A"), SampleRecord("B") };

        await sut.AppendRangeAsync(records);
        await sut.DisposeAsync();

        inner.Appended.Select(r => r.Field).Should().BeEquivalentTo("A", "B");
    }

    [Fact]
    public async Task QueryAsync_DelegatesToInnerStore()
    {
        var inner = Substitute.For<IAuditStore>();
        var expected = new[] { SampleRecord() };
        inner.QueryAsync(null, null, 0, 100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AuditRecord>>(expected));

        await using var sut = new BufferedAuditStore(inner);

        var result = await sut.QueryAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void AddBufferedAuditStore_WrapsRegisteredAuditStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();
        services.AddBufferedAuditStore(options =>
        {
            options.Capacity = 10;
            options.MaxBatchSize = 5;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAuditStore>()
            .Should().BeOfType<BufferedAuditStore>();
    }

    [Fact]
    public void AddBufferedAuditStore_WithScopedAuditStore_ThrowsHelpfulException()
    {
        var services = new ServiceCollection();
        services.AddAuditStore<InMemoryAuditStore>();

        var act = () => services.AddBufferedAuditStore();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Singleton*IAuditStore*background worker*");
    }

    [Fact]
    public async Task GetHealth_ReturnsPendingItemsCount()
    {
        var inner = new RecordingBatchAuditStore();
        var sut = new BufferedAuditStore(inner, new BufferedAuditStoreOptions
        {
            Capacity = 100,
            MaxBatchSize = 10,
        });

        await sut.AppendAsync(SampleRecord("A"));
        await sut.AppendAsync(SampleRecord("B"));

        // Give the background worker a moment to pick up items
        await Task.Delay(50);

        var health = sut.GetHealth();

        health.PendingItems.Should().BeGreaterOrEqualTo(0);
        health.IsFaulted.Should().BeFalse();
        health.BackgroundFailure.Should().BeNull();
        health.DroppedItems.Should().Be(0);
        health.FlushFailures.Should().Be(0);

        await sut.DisposeAsync();
    }

    [Fact]
    public async Task GetHealth_AfterDispose_ShowsZeroPending()
    {
        var inner = new RecordingBatchAuditStore();
        var sut = new BufferedAuditStore(inner, new BufferedAuditStoreOptions
        {
            Capacity = 10,
            MaxBatchSize = 10,
        });

        await sut.AppendAsync(SampleRecord("X"));
        await sut.DisposeAsync();

        var health = sut.GetHealth();
        health.PendingItems.Should().Be(0);
    }

    [Fact]
    public async Task GetHealth_AfterBackgroundFailure_ReportsFaulted()
    {
        var failingInner = Substitute.For<IAuditStore>();
        failingInner
            .When(x => x.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Simulated failure"));

        var sut = new BufferedAuditStore(failingInner, new BufferedAuditStoreOptions
        {
            Capacity = 10,
            MaxBatchSize = 1,
        });

        await sut.AppendAsync(SampleRecord("Fail"));

        // Wait for background worker to process and fail
        await Task.Delay(200);

        var health = sut.GetHealth();
        health.IsFaulted.Should().BeTrue();
        health.FlushFailures.Should().BeGreaterThan(0);
        health.BackgroundFailure.Should().Contain("Simulated failure");
    }

    [Fact]
    public async Task AppendAsync_AfterBackgroundFailure_ThrowsInvalidOperation()
    {
        var failingInner = Substitute.For<IAuditStore>();
        failingInner
            .When(x => x.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Simulated failure"));

        var sut = new BufferedAuditStore(failingInner, new BufferedAuditStoreOptions
        {
            Capacity = 10,
            MaxBatchSize = 1,
        });

        await sut.AppendAsync(SampleRecord("Fail"));
        await Task.Delay(200);

        var act = () => sut.AppendAsync(SampleRecord("AfterFail"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Buffered audit store background flush has failed*");
    }

    private sealed class RecordingBatchAuditStore : IBatchAuditStore
    {
        public List<AuditRecord> Appended { get; } = [];

        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            Appended.Add(record);
            return Task.CompletedTask;
        }

        public Task AppendRangeAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            Appended.AddRange(records);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>(Appended);

        public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
            string dataSubjectId,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>(
                Appended.Where(r => r.DataSubjectId == dataSubjectId).ToList());
    }
}
