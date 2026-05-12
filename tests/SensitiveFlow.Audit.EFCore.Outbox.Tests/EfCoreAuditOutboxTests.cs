using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Outbox.Extensions;
using SensitiveFlow.Audit.EFCore.Outbox.Stores;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.EFCore.Outbox.Tests;

public sealed class EfCoreAuditOutboxTests : IAsyncLifetime
{
    private DbContextFactory? _factory;

    public async Task InitializeAsync()
    {
        _factory = new DbContextFactory();
        using var ctx = await _factory.CreateDbContextAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task EnqueueAsync_AddsEntryToDatabase()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        var record = SampleRecord();

        await outbox.EnqueueAsync(record);

        using var ctx = await _factory!.CreateDbContextAsync();
        var entries = await ctx.Set<SensitiveFlow.Audit.EFCore.Outbox.Entities.AuditOutboxEntryEntity>()
            .Where(e => e.AuditRecordId == record.Id.ToString())
            .ToListAsync();

        entries.Should().HaveCount(1);
        entries[0].Payload.Should().NotBeNullOrEmpty();
        entries[0].IsProcessed.Should().BeFalse();
        entries[0].IsDeadLettered.Should().BeFalse();
        entries[0].Attempts.Should().Be(0);
    }

    [Fact]
    public async Task DequeueBatchAsync_ReturnsPendingEntries()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);

        // Enqueue 3 records
        for (int i = 0; i < 3; i++)
        {
            await outbox.EnqueueAsync(SampleRecord());
        }

        // Dequeue first 2
        var batch = await outbox.DequeueBatchAsync(2);

        batch.Should().HaveCount(2);
        batch.Should().AllSatisfy(e => e.Attempts.Should().Be(0));
    }

    [Fact]
    public async Task DequeueBatchAsync_WithNonPositiveMax_Throws()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);

        var act = () => outbox.DequeueBatchAsync(0);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("max");
    }

    [Fact]
    public async Task DequeueBatchAsync_IgnoresProcessedEntries()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        var record1 = SampleRecord();
        var record2 = SampleRecord();

        await outbox.EnqueueAsync(record1);
        await outbox.EnqueueAsync(record2);

        // Get first batch
        var batch1 = await outbox.DequeueBatchAsync(10);
        var processedId = batch1.First().Id;

        // Mark as processed
        await outbox.MarkProcessedAsync([processedId]);

        // Dequeue again - should only get the unprocessed one
        var batch2 = await outbox.DequeueBatchAsync(10);

        batch2.Should().HaveCount(1);
        batch2[0].Id.Should().NotBe(processedId);
    }

    [Fact]
    public async Task MarkProcessedAsync_UpdatesEntry()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        await outbox.EnqueueAsync(SampleRecord());

        var batch = await outbox.DequeueBatchAsync(10);
        await outbox.MarkProcessedAsync([batch[0].Id]);

        using var ctx = await _factory!.CreateDbContextAsync();
        var entry = await ctx.Set<SensitiveFlow.Audit.EFCore.Outbox.Entities.AuditOutboxEntryEntity>()
            .FirstAsync(e => e.Id == batch[0].Id);

        entry.IsProcessed.Should().BeTrue();
        entry.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkProcessedAsync_WithEmptyIds_ReturnsWithoutChangingPendingCount()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        await outbox.EnqueueAsync(SampleRecord());

        await outbox.MarkProcessedAsync([]);

        var count = await outbox.GetPendingCountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkProcessedAsync_WithNullIds_Throws()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);

        var act = () => outbox.MarkProcessedAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("ids");
    }

    [Fact]
    public async Task MarkFailedAsync_IncrementsAttemptsAndSetsError()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        await outbox.EnqueueAsync(SampleRecord());

        var batch = await outbox.DequeueBatchAsync(10);
        var entryId = batch[0].Id;
        var errorMsg = "Network timeout";

        await outbox.MarkFailedAsync(entryId, errorMsg);

        using var ctx = await _factory!.CreateDbContextAsync();
        var entry = await ctx.Set<SensitiveFlow.Audit.EFCore.Outbox.Entities.AuditOutboxEntryEntity>()
            .FirstAsync(e => e.Id == entryId);

        entry.Attempts.Should().Be(1);
        entry.LastError.Should().Be(errorMsg);
        entry.LastAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkFailedAsync_WithBlankError_Throws()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);

        var act = () => outbox.MarkFailedAsync(Guid.NewGuid(), " ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MarkFailedAsync_ForMissingEntry_DoesNotThrow()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);

        await outbox.MarkFailedAsync(Guid.NewGuid(), "timeout");

        (await outbox.GetPendingCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DequeueBatchAsync_IgnoresDeadLetteredEntries()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        var record1 = SampleRecord();
        var record2 = SampleRecord();

        await outbox.EnqueueAsync(record1);
        await outbox.EnqueueAsync(record2);

        var batch = await outbox.DequeueBatchAsync(10);
        var deadLetterId = batch[0].Id;

        // Mark as dead-lettered
        using (var ctx = await _factory!.CreateDbContextAsync())
        {
            var entry = await ctx.Set<SensitiveFlow.Audit.EFCore.Outbox.Entities.AuditOutboxEntryEntity>()
                .FirstAsync(e => e.Id == deadLetterId);
            entry.IsDeadLettered = true;
            entry.DeadLetterReason = "Max retries exceeded";
            await ctx.SaveChangesAsync();
        }

        // Dequeue again - should only get the non-dead-lettered one
        var batch2 = await outbox.DequeueBatchAsync(10);

        batch2.Should().HaveCount(1);
        batch2[0].Id.Should().NotBe(deadLetterId);
    }

    [Fact]
    public async Task MarkDeadLetteredAsync_SetsDeadLetterStateAndRemovesFromPending()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        await outbox.EnqueueAsync(SampleRecord());
        var entry = (await outbox.DequeueBatchAsync(10)).Single();

        await outbox.MarkDeadLetteredAsync(entry.Id, "publisher failed too many times");

        (await outbox.GetPendingCountAsync()).Should().Be(0);
        var deadLetters = await outbox.GetDeadLetteredAsync();
        deadLetters.Should().ContainSingle(e => e.Id == entry.Id);
    }

    [Fact]
    public async Task MarkDeadLetteredAsync_WithBlankReason_Throws()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);

        var act = () => outbox.MarkDeadLetteredAsync(Guid.NewGuid(), "");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetDeadLetteredAsync_AppliesSkipAndTake()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);
        for (var i = 0; i < 3; i++)
        {
            await outbox.EnqueueAsync(SampleRecord());
        }

        var entries = await outbox.DequeueBatchAsync(10);
        foreach (var entry in entries)
        {
            await outbox.MarkDeadLetteredAsync(entry.Id, "failed");
        }

        var page = await outbox.GetDeadLetteredAsync(skip: 1, take: 1);

        page.Should().ContainSingle();
    }

    [Fact]
    public async Task EnqueueAsync_WithNullRecord_Throws()
    {
        var outbox = new EfCoreAuditOutbox(_factory!);

        var act = () => outbox.EnqueueAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("record");
    }

    private static AuditRecord SampleRecord() => new()
    {
        DataSubjectId = $"subject-{Guid.NewGuid()}",
        Entity = "Customer",
        Field = "Email",
        Operation = AuditOperation.Update,
    };

    // Test fixture for in-memory SQLite with shared connection
    private sealed class DbContextFactory : IDbContextFactory<AuditDbContext>, IAsyncDisposable
    {
        private SqliteConnection? _connection;

        public AuditDbContext CreateDbContext()
        {
            _connection ??= new SqliteConnection("Data Source=:memory:");
            if (_connection.State == System.Data.ConnectionState.Closed)
            {
                _connection.Open();
            }

            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new AuditDbContext(options);
        }

        public async Task<AuditDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var ctx = CreateDbContext();
            await ctx.Database.EnsureCreatedAsync(cancellationToken);
            return ctx;
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }
    }
}
