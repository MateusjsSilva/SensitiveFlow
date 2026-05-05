using FluentAssertions;
using SensitiveFlow.Audit.Stores;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class InMemoryAuditStoreTests
{
    private static AuditRecord MakeRecord(string dataSubjectId, AuditOperation op = AuditOperation.Access, DateTimeOffset? timestamp = null)
        => new()
        {
            DataSubjectId = dataSubjectId,
            Entity = "User",
            Field = "Email",
            Operation = op,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task AppendAsync_StoresRecord()
    {
        var store = new InMemoryAuditStore();
        var record = MakeRecord("subject-1");

        await store.AppendAsync(record);

        var results = await store.QueryAsync();
        results.Should().ContainSingle(r => r.Id == record.Id);
    }

    [Fact]
    public async Task AppendAsync_NullRecord_Throws()
    {
        var store = new InMemoryAuditStore();
        await store.Invoking(s => s.AppendAsync(null!))
                   .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryAsync_ReturnsAll_WhenNoFilter()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(MakeRecord("a"));
        await store.AppendAsync(MakeRecord("b"));
        await store.AppendAsync(MakeRecord("c"));

        var results = await store.QueryAsync();
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryAsync_FiltersBy_From()
    {
        var store = new InMemoryAuditStore();
        var old = MakeRecord("a", timestamp: DateTimeOffset.UtcNow.AddDays(-10));
        var recent = MakeRecord("b", timestamp: DateTimeOffset.UtcNow.AddDays(-1));
        await store.AppendAsync(old);
        await store.AppendAsync(recent);

        var results = await store.QueryAsync(from: DateTimeOffset.UtcNow.AddDays(-2));
        results.Should().ContainSingle(r => r.DataSubjectId == "b");
    }

    [Fact]
    public async Task QueryAsync_FiltersBy_To()
    {
        var store = new InMemoryAuditStore();
        var old = MakeRecord("a", timestamp: DateTimeOffset.UtcNow.AddDays(-10));
        var recent = MakeRecord("b", timestamp: DateTimeOffset.UtcNow.AddDays(-1));
        await store.AppendAsync(old);
        await store.AppendAsync(recent);

        var results = await store.QueryAsync(to: DateTimeOffset.UtcNow.AddDays(-5));
        results.Should().ContainSingle(r => r.DataSubjectId == "a");
    }

    [Fact]
    public async Task QueryAsync_Pagination_Skip()
    {
        var store = new InMemoryAuditStore();
        for (var i = 0; i < 5; i++)
            await store.AppendAsync(MakeRecord($"sub-{i}", timestamp: DateTimeOffset.UtcNow.AddSeconds(i)));

        var results = await store.QueryAsync(skip: 3);
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryAsync_Pagination_Take()
    {
        var store = new InMemoryAuditStore();
        for (var i = 0; i < 5; i++)
            await store.AppendAsync(MakeRecord($"sub-{i}"));

        var results = await store.QueryAsync(take: 2);
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryByDataSubjectAsync_ReturnsOnlyMatchingSubject()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(MakeRecord("alice"));
        await store.AppendAsync(MakeRecord("bob"));
        await store.AppendAsync(MakeRecord("alice"));

        var results = await store.QueryByDataSubjectAsync("alice");
        results.Should().HaveCount(2).And.OnlyContain(r => r.DataSubjectId == "alice");
    }

    [Fact]
    public async Task QueryByDataSubjectAsync_EmptyId_Throws()
    {
        var store = new InMemoryAuditStore();
        await store.Invoking(s => s.QueryByDataSubjectAsync(""))
                   .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task QueryAsync_ReturnsOrderedByTimestamp()
    {
        var store = new InMemoryAuditStore();
        var t = DateTimeOffset.UtcNow;
        await store.AppendAsync(MakeRecord("a", timestamp: t.AddSeconds(2)));
        await store.AppendAsync(MakeRecord("b", timestamp: t.AddSeconds(1)));
        await store.AppendAsync(MakeRecord("c", timestamp: t));

        var results = await store.QueryAsync();
        results.Select(r => r.DataSubjectId).Should().Equal("c", "b", "a");
    }
}
