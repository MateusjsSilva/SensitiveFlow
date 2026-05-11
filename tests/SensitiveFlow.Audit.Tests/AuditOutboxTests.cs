using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class AuditOutboxTests
{
    [Fact]
    public async Task InMemoryAuditOutbox_EnqueuesRecords()
    {
        var outbox = new InMemoryAuditOutbox();
        var record = SampleRecord();

        await outbox.EnqueueAsync(record);

        outbox.Records.Should().ContainSingle(r => r.Id == record.Id);
    }

    [Fact]
    public void JsonAuditOutboxSerializer_RoundTripsRecord()
    {
        var serializer = new JsonAuditOutboxSerializer();
        var record = SampleRecord();

        var payload = serializer.Serialize(record);
        var restored = serializer.Deserialize(payload);

        restored.Id.Should().Be(record.Id);
        restored.DataSubjectId.Should().Be(record.DataSubjectId);
        restored.Operation.Should().Be(record.Operation);
    }

    [Fact]
    public async Task AddInMemoryAuditOutbox_WrapsAuditStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, TestAuditStore>();
        services.AddInMemoryAuditOutbox();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IAuditStore>();
        var outbox = provider.GetRequiredService<InMemoryAuditOutbox>();
        var record = SampleRecord();

        await store.AppendAsync(record);

        outbox.Records.Should().ContainSingle(r => r.Id == record.Id);
    }

    private static AuditRecord SampleRecord() => new()
    {
        DataSubjectId = "subject-1",
        Entity = "Customer",
        Field = "Email",
        Operation = AuditOperation.Update,
    };

    private sealed class TestAuditStore : IAuditStore
    {
        private readonly List<AuditRecord> _records = [];

        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>(_records);

        public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
            string dataSubjectId,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>(
                _records.Where(r => r.DataSubjectId == dataSubjectId).ToArray());
    }
}
