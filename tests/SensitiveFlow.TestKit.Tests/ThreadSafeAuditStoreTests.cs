using FluentAssertions;
using SensitiveFlow.TestKit.Threading;
using Xunit;

namespace SensitiveFlow.TestKit.Tests;

public class ThreadSafeAuditStoreTests
{
    [Fact]
    public void AddRecord_StoresRecord()
    {
        var store = new ThreadSafeAuditStore();
        var record = new { Id = 1, Entity = "Customer" };

        store.AddRecord(record);

        store.RecordCount.Should().Be(1);
    }

    [Fact]
    public void GetAllRecords_ReturnsAllRecords()
    {
        var store = new ThreadSafeAuditStore();
        var record1 = new { Id = 1 };
        var record2 = new { Id = 2 };

        store.AddRecord(record1);
        store.AddRecord(record2);

        var records = store.GetAllRecords();

        records.Should().HaveCount(2);
    }

    [Fact]
    public void RecordCount_ReturnsCorrectCount()
    {
        var store = new ThreadSafeAuditStore();

        store.AddRecord(new { Id = 1 });
        store.AddRecord(new { Id = 2 });
        store.AddRecord(new { Id = 3 });

        store.RecordCount.Should().Be(3);
    }

    [Fact]
    public void Clear_RemovesAllRecords()
    {
        var store = new ThreadSafeAuditStore();
        store.AddRecord(new { Id = 1 });
        store.AddRecord(new { Id = 2 });

        store.Clear();

        store.RecordCount.Should().Be(0);
        store.GetAllRecords().Should().BeEmpty();
    }

    [Fact]
    public void AddRecord_ThrowsOnNull()
    {
        var store = new ThreadSafeAuditStore();

        var act = () => store.AddRecord(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MultiThreaded_AddRecords_IsThreadSafe()
    {
        var store = new ThreadSafeAuditStore();
        var threadCount = 10;
        var recordsPerThread = 100;

        Parallel.For(0, threadCount, i =>
        {
            for (int j = 0; j < recordsPerThread; j++)
            {
                store.AddRecord(new { ThreadId = i, RecordId = j });
            }
        });

        store.RecordCount.Should().Be(threadCount * recordsPerThread);
    }

    [Fact]
    public void MultiThreaded_MixedOperations_IsThreadSafe()
    {
        var store = new ThreadSafeAuditStore();
        var tasks = new List<Task>();

        // Thread 1: Adding records
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                store.AddRecord(new { Id = i });
            }
        }));

        // Thread 2: Adding different records
        tasks.Add(Task.Run(() =>
        {
            for (int i = 100; i < 150; i++)
            {
                store.AddRecord(new { Id = i });
            }
        }));

        // Thread 3: Checking count
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                var count = store.RecordCount;
                count.Should().BeGreaterThanOrEqualTo(0);
            }
        }));

        Task.WaitAll(tasks.ToArray());

        store.RecordCount.Should().Be(100);
    }

    [Fact]
    public void GetRecordsOfType_FiltersCorrectly()
    {
        var store = new ThreadSafeAuditStore();
        store.AddRecord(new AuditRecord { Id = 1, Entity = "Customer" });
        store.AddRecord(new AuditRecord { Id = 2, Entity = "Order" });
        store.AddRecord("StringRecord");

        var auditRecords = store.GetRecordsOfType<AuditRecord>().ToList();

        auditRecords.Should().HaveCount(2);
    }

    // Test helper class
    private class AuditRecord
    {
        public int Id { get; set; }
        public string? Entity { get; set; }
    }
}
