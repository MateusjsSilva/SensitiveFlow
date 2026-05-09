using FluentAssertions;
using SensitiveFlow.Audit.InMemory;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class InMemoryAuditSnapshotStoreTests
{
    private readonly InMemoryAuditSnapshotStore _store = new();

    [Fact]
    public async Task QueryByAggregate_ReturnsOnlyMatchingSnapshots()
    {
        await _store.AppendAsync(Make("Customer", "1", "alice"));
        await _store.AppendAsync(Make("Customer", "2", "bob"));
        await _store.AppendAsync(Make("Order", "1", "alice"));

        var customerOne = await _store.QueryByAggregateAsync("Customer", "1");

        customerOne.Should().ContainSingle().Which.DataSubjectId.Should().Be("alice");
    }

    [Fact]
    public async Task QueryByDataSubject_ReturnsAllAggregatesForSubject()
    {
        await _store.AppendAsync(Make("Customer", "1", "alice"));
        await _store.AppendAsync(Make("Order", "9", "alice"));
        await _store.AppendAsync(Make("Customer", "2", "bob"));

        var aliceItems = await _store.QueryByDataSubjectAsync("alice");

        aliceItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryByAggregate_OrdersByTimestampAscending()
    {
        var older = Make("Customer", "1", "alice") with { Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10) };
        var newer = Make("Customer", "1", "alice") with { Timestamp = DateTimeOffset.UtcNow };

        await _store.AppendAsync(newer);
        await _store.AppendAsync(older);

        var result = await _store.QueryByAggregateAsync("Customer", "1");

        result.Should().HaveCount(2);
        result[0].Timestamp.Should().BeBefore(result[1].Timestamp);
    }

    [Fact]
    public async Task Append_OnNullSnapshot_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.AppendAsync(null!));
    }

    private static AuditSnapshot Make(string aggregate, string aggregateId, string dataSubjectId) => new()
    {
        DataSubjectId = dataSubjectId,
        Aggregate = aggregate,
        AggregateId = aggregateId,
        Operation = AuditOperation.Update,
        BeforeJson = "{\"Email\":\"[REDACTED]\"}",
        AfterJson = "{\"Email\":\"[REDACTED]\"}",
    };
}
