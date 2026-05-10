using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.Context;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.EFCore.Tests.Stores;

namespace SensitiveFlow.EFCore.Tests;

public sealed class SensitiveDataAuditInterceptorSyncTests
{
    private sealed class OrderEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "subject-sync";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "sync@example.com";

        public string PublicField { get; set; } = "public";
    }

    private sealed class SyncDbContext : DbContext
    {
        public SyncDbContext(SensitiveDataAuditInterceptor interceptor)
            : base(new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options)
        {
        }

        public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    }

    private static (SyncDbContext db, InMemoryAuditStore store) Build()
    {
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
        var db = new SyncDbContext(interceptor);
        db.Database.EnsureCreated();
        return (db, store);
    }

    [Fact]
    public async Task SaveChanges_Synchronous_EmitsAuditRecords()
    {
        var (db, store) = Build();

        db.Orders.Add(new OrderEntity());
        db.SaveChanges();

        var records = await store.QueryAsync();
        records.Should().ContainSingle(r => r.Field == "Email" && r.Operation == AuditOperation.Create);
    }

    [Fact]
    public void SaveChanges_Synchronous_NoChanges_DoesNotThrow()
    {
        var (db, _) = Build();

        var act = () => db.SaveChanges();

        act.Should().NotThrow();
    }

    private sealed class UniqueEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "sync-unique-subject";
        public string UniqueValue { get; set; } = "same";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "sync-unique@example.com";
    }

    private sealed class FailingSyncDbContext : DbContext
    {
        private readonly SqliteConnection _connection;
        private readonly SensitiveDataAuditInterceptor _interceptor;

        public FailingSyncDbContext(SqliteConnection connection, SensitiveDataAuditInterceptor interceptor)
        {
            _connection = connection;
            _interceptor = interceptor;
        }

        public DbSet<UniqueEntity> Items => Set<UniqueEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(_connection).AddInterceptors(_interceptor);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UniqueEntity>().HasIndex(e => e.UniqueValue).IsUnique();
    }

    [Fact]
    public async Task SaveChangesFailed_Synchronous_RemovesPendingAuditRecords()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
        using var db = new FailingSyncDbContext(connection, interceptor);
        db.Database.EnsureCreated();

        db.Items.Add(new UniqueEntity { DataSubjectId = "one", UniqueValue = "duplicate" });
        db.SaveChanges();

        db.Items.Add(new UniqueEntity { DataSubjectId = "two", UniqueValue = "duplicate" });
        var act = () => db.SaveChanges();

        act.Should().Throw<DbUpdateException>();
        (await store.QueryByDataSubjectAsync("two")).Should().BeEmpty();
    }
}
