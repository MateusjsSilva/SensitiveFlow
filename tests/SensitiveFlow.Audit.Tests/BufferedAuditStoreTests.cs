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
        services.AddAuditStore<InMemoryAuditStore>();
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
