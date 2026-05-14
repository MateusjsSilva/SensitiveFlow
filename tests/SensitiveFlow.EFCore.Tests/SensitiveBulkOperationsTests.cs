using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.BulkOperations;
using SensitiveFlow.EFCore.Context;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.EFCore.Tests.Stores;

namespace SensitiveFlow.EFCore.Tests;

public sealed class SensitiveBulkOperationsTests
{
    private sealed class Customer
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Identification)]
        public string FullName { get; set; } = string.Empty;

        public string Status { get; set; } = "Active";
    }

    private sealed class NonSensitive
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    private sealed class BulkDbContext : DbContext
    {
        private readonly SqliteConnection _connection;
        private readonly SensitiveBulkOperationsGuardInterceptor? _guard;

        public BulkDbContext(SqliteConnection connection, SensitiveBulkOperationsGuardInterceptor? guard = null)
        {
            _connection = connection;
            _guard = guard;
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<NonSensitive> NonSensitives => Set<NonSensitive>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connection);
            if (_guard is not null)
            {
                optionsBuilder.AddInterceptors(_guard);
            }
        }
    }

    private static async Task<(SqliteConnection connection, BulkDbContext db, InMemoryAuditStore store)> BuildAsync(
        SensitiveBulkOperationsGuardInterceptor? guard = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new BulkDbContext(connection, guard);
        await db.Database.EnsureCreatedAsync();
        return (connection, db, new InMemoryAuditStore());
    }

    [Fact]
    public async Task ExecuteUpdateAuditedAsync_EmitsAuditRecord_PerSubjectAndAnnotatedField()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        db.Customers.AddRange(
            new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "Alice" },
            new Customer { DataSubjectId = "s2", Email = "b@x.com", FullName = "Bob" });
        await db.SaveChangesAsync();

        var affected = await db.Customers
            .Where(c => c.Status == "Active")
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(c => c.Email, "redacted@x.com"),
                store,
                NullAuditContext.Instance);

        affected.Should().Be(2);

        var records = await store.QueryAsync();
        records.Should().HaveCount(2);
        records.Should().AllSatisfy(r =>
        {
            r.Operation.Should().Be(AuditOperation.Update);
            r.Field.Should().Be("Email");
            r.Entity.Should().Be(nameof(Customer));
        });
        records.Select(r => r.DataSubjectId).Should().BeEquivalentTo(["s1", "s2"]);
    }

    [Fact]
    public async Task ExecuteUpdateAuditedAsync_DoesNotEmitForNonSensitiveSetter()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        db.Customers.Add(new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "Alice" });
        await db.SaveChangesAsync();

        await db.Customers
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(c => c.Status, "Inactive"),
                store,
                NullAuditContext.Instance);

        (await store.QueryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteUpdateAuditedAsync_OnNonAnnotatedEntity_Forwards()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        db.NonSensitives.Add(new NonSensitive { DataSubjectId = "s1", Label = "x" });
        await db.SaveChangesAsync();

        var affected = await db.NonSensitives
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(n => n.Label, "y"),
                store,
                NullAuditContext.Instance);

        affected.Should().Be(1);
        (await store.QueryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteUpdateAuditedAsync_OnlyAuditsAnnotatedFields_WhenMixedSetters()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        db.Customers.Add(new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "Alice" });
        await db.SaveChangesAsync();

        await db.Customers
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(c => c.Email, "new@x.com")
                      .SetProperty(c => c.Status, "Inactive"),
                store,
                NullAuditContext.Instance);

        var records = await store.QueryAsync();
        records.Should().HaveCount(1);
        records[0].Field.Should().Be("Email");
    }

    [Fact]
    public async Task ExecuteDeleteAuditedAsync_EmitsAuditPerSubjectAndAnnotatedField()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        db.Customers.AddRange(
            new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "Alice" },
            new Customer { DataSubjectId = "s2", Email = "b@x.com", FullName = "Bob" });
        await db.SaveChangesAsync();

        var affected = await db.Customers.ExecuteDeleteAuditedAsync(store, NullAuditContext.Instance);

        affected.Should().Be(2);

        var records = await store.QueryAsync();
        // 2 subjects × 2 annotated fields (Email, FullName)
        records.Should().HaveCount(4);
        records.Should().AllSatisfy(r => r.Operation.Should().Be(AuditOperation.Delete));
        records.Select(r => r.Field).Distinct().Should().BeEquivalentTo(["Email", "FullName"]);
    }

    [Fact]
    public async Task ExecuteUpdateAuditedAsync_ThrowsWhenAffectsMoreThanMaxAuditedRows()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        for (var i = 0; i < 5; i++)
        {
            db.Customers.Add(new Customer
            {
                DataSubjectId = $"subj-{i}",
                Email = $"u{i}@x.com",
                FullName = $"User {i}",
            });
        }
        await db.SaveChangesAsync();

        var options = new SensitiveBulkOperationsOptions { MaxAuditedRows = 2 };

        var act = async () => await db.Customers
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(c => c.Email, "x@x.com"),
                store,
                NullAuditContext.Instance,
                options);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*more than 2 subjects*");

        (await store.QueryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteUpdateAuditedAsync_ThrowsWhenSubjectIsEmpty()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        db.Customers.Add(new Customer { DataSubjectId = "valid", Email = "a@x.com", FullName = "A" });
        await db.SaveChangesAsync();
        // Insert directly to bypass DataSubjectId validation, which only kicks in on
        // SaveChanges paths.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Customers (DataSubjectId, Email, FullName, Status) VALUES ('', 'x@x.com', 'X', 'Active')");

        var act = async () => await db.Customers
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(c => c.Email, "y@x.com"),
                store,
                NullAuditContext.Instance);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*null or empty*");
    }

    [Fact]
    public async Task Guard_BlocksDirectExecuteUpdateOnAnnotatedEntity()
    {
        var options = new SensitiveBulkOperationsOptions();
        var guard = new SensitiveBulkOperationsGuardInterceptor(options);
        var (connection, db, _) = await BuildAsync(guard);
        await using var _2 = connection;

        db.Customers.Add(new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "A" });
        await db.SaveChangesAsync();

        var act = async () => await db.Customers
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Email, "x@x.com"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Customer*ExecuteUpdate*");
    }

    [Fact]
    public async Task Guard_AllowsExecuteUpdateOnNonAnnotatedEntity()
    {
        var options = new SensitiveBulkOperationsOptions();
        var guard = new SensitiveBulkOperationsGuardInterceptor(options);
        var (connection, db, _) = await BuildAsync(guard);
        await using var _2 = connection;

        db.NonSensitives.Add(new NonSensitive { DataSubjectId = "s1", Label = "x" });
        await db.SaveChangesAsync();

        var affected = await db.NonSensitives
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Label, "y"));

        affected.Should().Be(1);
    }

    [Fact]
    public async Task Guard_AllowsAuditedHelperOnAnnotatedEntity()
    {
        var options = new SensitiveBulkOperationsOptions();
        var guard = new SensitiveBulkOperationsGuardInterceptor(options);
        var (connection, db, store) = await BuildAsync(guard);
        await using var _ = connection;

        db.Customers.Add(new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "A" });
        await db.SaveChangesAsync();

        var affected = await db.Customers
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(c => c.Email, "new@x.com"),
                store,
                NullAuditContext.Instance);

        affected.Should().Be(1);
    }

    [Fact]
    public async Task Guard_CanBeDisabledViaOptions()
    {
        var options = new SensitiveBulkOperationsOptions { RequireExplicitAuditing = false };
        var guard = new SensitiveBulkOperationsGuardInterceptor(options);
        var (connection, db, _) = await BuildAsync(guard);
        await using var _2 = connection;

        db.Customers.Add(new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "A" });
        await db.SaveChangesAsync();

        var affected = await db.Customers
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Email, "x@x.com"));

        affected.Should().Be(1);
    }

    [Fact]
    public async Task Guard_BlocksDirectExecuteDeleteOnAnnotatedEntity()
    {
        var options = new SensitiveBulkOperationsOptions();
        var guard = new SensitiveBulkOperationsGuardInterceptor(options);
        var (connection, db, _) = await BuildAsync(guard);
        await using var _2 = connection;

        db.Customers.Add(new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "A" });
        await db.SaveChangesAsync();

        var act = async () => await db.Customers.ExecuteDeleteAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Customer*ExecuteDelete*");
    }

    [Fact]
    public void Guard_ThrowsOnNullOptions()
    {
        var act = () => new SensitiveBulkOperationsGuardInterceptor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteUpdateAuditedAsync_AuditsAllAnnotatedFields_RegardlessOfRedaction()
    {
        var (connection, db, store) = await BuildAsync();
        await using var _ = connection;

        db.Customers.AddRange(
            new Customer { DataSubjectId = "s1", Email = "a@x.com", FullName = "Alice" },
            new Customer { DataSubjectId = "s2", Email = "b@x.com", FullName = "Bob" });
        await db.SaveChangesAsync();

        var affected = await db.Customers
            .Where(c => c.Status == "Active")
            .ExecuteUpdateAuditedAsync(
                s => s.SetProperty(c => c.Email, "redacted@x.com"),
                store,
                NullAuditContext.Instance);

        affected.Should().Be(2);

        var records = await store.QueryAsync();
        // All annotated fields are audited, regardless of [Redaction] attributes
        records.Should().HaveCount(2);
        records.Should().AllSatisfy(r =>
        {
            r.Operation.Should().Be(AuditOperation.Update);
            r.Field.Should().Be("Email");
            r.Entity.Should().Be(nameof(Customer));
        });
    }
}
