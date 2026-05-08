using FluentAssertions;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using Xunit;

namespace SensitiveFlow.TestKit;

/// <summary>
/// Conformance suite for <see cref="IAuditStore"/> implementations. Inherit from this
/// class in your test project, override <see cref="CreateStoreAsync"/>, and you get
/// a baseline of behavior tests.
/// </summary>
/// <remarks>
/// The tests do <b>not</b> assume durability across test invocations — each test creates
/// a fresh store instance. If your store is durable, ensure that <see cref="CreateStoreAsync"/>
/// returns an isolated namespace (separate database, table prefix, etc.) per call.
/// </remarks>
public abstract class AuditStoreContractTests
{
    /// <summary>Creates a fresh store instance for the test.</summary>
    protected abstract Task<IAuditStore> CreateStoreAsync();

    /// <summary>Cleanup hook for stores that need teardown.</summary>
    protected virtual Task DisposeStoreAsync(IAuditStore store) => Task.CompletedTask;

    [Fact]
    public async Task AppendAsync_ThenQuery_ReturnsTheRecord()
    {
        var store = await CreateStoreAsync();
        try
        {
            var record = SampleRecord();
            await store.AppendAsync(record);

            var page = await store.QueryAsync();
            page.Should().ContainSingle(r => r.Id == record.Id);
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task QueryByDataSubject_FiltersBySubject()
    {
        var store = await CreateStoreAsync();
        try
        {
            await store.AppendAsync(SampleRecord(subject: "alice"));
            await store.AppendAsync(SampleRecord(subject: "bob"));

            var alice = await store.QueryByDataSubjectAsync("alice");
            alice.Should().OnlyContain(r => r.DataSubjectId == "alice");
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task Query_RespectsFromAndTo()
    {
        var store = await CreateStoreAsync();
        try
        {
            var t0 = DateTimeOffset.UtcNow.AddDays(-2);
            var t1 = DateTimeOffset.UtcNow.AddDays(-1);
            var t2 = DateTimeOffset.UtcNow;

            await store.AppendAsync(SampleRecord(timestamp: t0));
            await store.AppendAsync(SampleRecord(timestamp: t1));
            await store.AppendAsync(SampleRecord(timestamp: t2));

            var window = await store.QueryAsync(from: t1.AddMinutes(-1), to: t2.AddMinutes(1));
            window.Should().HaveCount(2);
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task Query_RespectsSkipAndTake()
    {
        var store = await CreateStoreAsync();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                await store.AppendAsync(SampleRecord());
            }

            var page = await store.QueryAsync(skip: 2, take: 2);
            page.Should().HaveCount(2);
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task Append_PreservesAllFields()
    {
        var store = await CreateStoreAsync();
        try
        {
            var record = new AuditRecord
            {
                DataSubjectId  = "subject-x",
                Entity         = "Customer",
                Field          = "Email",
                Operation      = AuditOperation.Update,
                Timestamp      = DateTimeOffset.UtcNow,
                ActorId        = "actor-y",
                IpAddressToken = "tok-z",
                Details        = "demo",
            };

            await store.AppendAsync(record);

            var page = await store.QueryByDataSubjectAsync("subject-x");
            var loaded = page.Should().ContainSingle().Subject;

            loaded.Entity.Should().Be("Customer");
            loaded.Field.Should().Be("Email");
            loaded.Operation.Should().Be(AuditOperation.Update);
            loaded.ActorId.Should().Be("actor-y");
            loaded.IpAddressToken.Should().Be("tok-z");
            loaded.Details.Should().Be("demo");
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    private static AuditRecord SampleRecord(
        string subject = "subject-a",
        DateTimeOffset? timestamp = null) =>
        new()
        {
            DataSubjectId = subject,
            Entity        = "TestEntity",
            Field         = "TestField",
            Operation     = AuditOperation.Access,
            Timestamp     = timestamp ?? DateTimeOffset.UtcNow,
        };
}
