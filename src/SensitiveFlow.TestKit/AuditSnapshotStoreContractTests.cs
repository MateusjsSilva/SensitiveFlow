using FluentAssertions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using Xunit;

namespace SensitiveFlow.TestKit;

/// <summary>
/// Conformance suite for <see cref="IAuditSnapshotStore"/> implementations.
/// </summary>
public abstract class AuditSnapshotStoreContractTests
{
    /// <summary>Creates a fresh store instance for the test.</summary>
    protected abstract Task<IAuditSnapshotStore> CreateStoreAsync();

    /// <summary>Cleanup hook for stores that need teardown.</summary>
    protected virtual Task DisposeStoreAsync(IAuditSnapshotStore store) => Task.CompletedTask;

    /// <summary>Verifies append and aggregate query behavior.</summary>
    [Fact]
    public async Task AppendAsync_ThenQueryByAggregate_ReturnsSnapshot()
    {
        var store = await CreateStoreAsync();
        try
        {
            var snapshot = SampleSnapshot();
            await store.AppendAsync(snapshot);

            var page = await store.QueryByAggregateAsync("Customer", "42");
            page.Should().ContainSingle(s => s.Id == snapshot.Id);
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    /// <summary>Verifies data-subject filtering behavior.</summary>
    [Fact]
    public async Task QueryByDataSubject_FiltersBySubject()
    {
        var store = await CreateStoreAsync();
        try
        {
            await store.AppendAsync(SampleSnapshot(subject: "alice"));
            await store.AppendAsync(SampleSnapshot(subject: "bob", aggregateId: "43"));

            var alice = await store.QueryByDataSubjectAsync("alice");
            alice.Should().OnlyContain(s => s.DataSubjectId == "alice");
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    private static AuditSnapshot SampleSnapshot(string subject = "alice", string aggregateId = "42")
    {
        return new AuditSnapshot
        {
            DataSubjectId = subject,
            Aggregate = "Customer",
            AggregateId = aggregateId,
            BeforeJson = "{\"Email\":\"old\"}",
            AfterJson = "{\"Email\":\"[REDACTED]\"}",
        };
    }
}
