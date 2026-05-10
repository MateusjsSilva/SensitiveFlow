using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using SensitiveFlow.EFCore.Tests.Stores;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.EFCore.Context;
using SensitiveFlow.EFCore.Interceptors;

namespace SensitiveFlow.EFCore.Tests;

public sealed class SensitiveDataAuditInterceptorTests
{
    private sealed class UserEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "subject-1";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "test@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Health)]
        public string HealthNote { get; set; } = "none";

        public string PublicField { get; set; } = "visible";
    }

    private sealed class TestDbContext : DbContext
    {
        private readonly SensitiveDataAuditInterceptor _interceptor;

        public TestDbContext(SensitiveDataAuditInterceptor interceptor)
            : base(BuildOptions(interceptor))
        {
            _interceptor = interceptor;
        }

        public DbSet<UserEntity> Users => Set<UserEntity>();

        private static DbContextOptions BuildOptions(SensitiveDataAuditInterceptor interceptor)
            => new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;
    }

    private static (TestDbContext, InMemoryAuditStore) BuildContext(IAuditContext? auditContext = null)
    {
        var store = new InMemoryAuditStore();
        var ctx = auditContext ?? NullAuditContext.Instance;
        var interceptor = new SensitiveDataAuditInterceptor(store, ctx);
        var db = new TestDbContext(interceptor);
        db.Database.EnsureCreated();
        return (db, store);
    }

    [Fact]
    public async Task AddEntity_EmitsAuditRecords_ForSensitiveFields()
    {
        var (db, store) = BuildContext();
        db.Users.Add(new UserEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().HaveCount(2);
        records.Select(r => r.Field).Should().BeEquivalentTo(["Email", "HealthNote"]);
    }

    [Fact]
    public async Task AddEntity_UsesBatchAppend_WhenStoreSupportsIt()
    {
        var store = Substitute.For<IBatchAuditStore>();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
        var db = new TestDbContext(interceptor);
        db.Database.EnsureCreated();

        db.Users.Add(new UserEntity());
        await db.SaveChangesAsync();

        await store.Received(1).AppendRangeAsync(
            Arg.Is<IReadOnlyCollection<AuditRecord>>(records => records.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddEntity_DoesNotEmit_ForNonSensitiveFields()
    {
        var (db, store) = BuildContext();
        db.Users.Add(new UserEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().NotContain(r => r.Field == "PublicField");
    }

    [Fact]
    public async Task AddEntity_AuditRecord_HasCreateOperation()
    {
        var (db, store) = BuildContext();
        db.Users.Add(new UserEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().OnlyContain(r => r.Operation == AuditOperation.Create);
    }

    [Fact]
    public async Task UpdateSensitiveField_EmitsUpdateRecord()
    {
        var (db, store) = BuildContext();
        var entity = new UserEntity();
        db.Users.Add(entity);
        await db.SaveChangesAsync();

        entity.Email = "changed@example.com";
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().Contain(r => r.Operation == AuditOperation.Update && r.Field == "Email");
    }

    [Fact]
    public async Task UpdateNonSensitiveField_DoesNotEmitRecord()
    {
        var (db, store) = BuildContext();
        var entity = new UserEntity();
        db.Users.Add(entity);
        await db.SaveChangesAsync();

        entity.PublicField = "updated";
        await db.SaveChangesAsync();

        var records = await store.QueryByDataSubjectAsync("subject-1");
        records.Should().NotContain(r => r.Operation == AuditOperation.Update && r.Field == "PublicField");
    }

    [Fact]
    public async Task DeleteEntity_EmitsDeleteRecords_ForSensitiveFields()
    {
        var (db, store) = BuildContext();
        var entity = new UserEntity();
        db.Users.Add(entity);
        await db.SaveChangesAsync();

        db.Users.Remove(entity);
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().Contain(r => r.Operation == AuditOperation.Delete);
    }

    [Fact]
    public async Task AuditRecord_UsesActorId_FromAuditContext()
    {
        var context = Substitute.For<IAuditContext>();
        context.ActorId.Returns("actor-99");
        context.IpAddressToken.Returns((string?)null);

        var (db, store) = BuildContext(context);
        db.Users.Add(new UserEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().OnlyContain(r => r.ActorId == "actor-99");
    }

    [Fact]
    public async Task AuditRecord_DataSubjectId_ResolvedFromEntity()
    {
        var (db, store) = BuildContext();
        db.Users.Add(new UserEntity { DataSubjectId = "subject-xyz" });
        await db.SaveChangesAsync();

        var records = await store.QueryByDataSubjectAsync("subject-xyz");
        records.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AuditRecord_EmptyDataSubjectId_ThrowsAtSaveChanges()
    {
        var (db, _) = BuildContext();
        db.Users.Add(new UserEntity { DataSubjectId = string.Empty });

        // Empty DataSubjectId on Add must fail loudly: an audit record without a stable
        // subject identifier corrupts the trail.
        await db.Invoking(c => c.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DataSubjectId*null or empty*");
    }

    private sealed class FallbackOnlyEntity
    {
        public int Id { get; set; }

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "fallback@example.com";
    }

    private sealed class FallbackOnlyDbContext : DbContext
    {
        public FallbackOnlyDbContext(SensitiveDataAuditInterceptor interceptor)
            : base(new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options) { }

        public DbSet<FallbackOnlyEntity> Items => Set<FallbackOnlyEntity>();
    }

    [Fact]
    public async Task AuditRecord_NoDataSubjectIdProperty_ThrowsAtSaveChanges()
    {
        // §4.1.1 (revised): falling back to 'Id' was unsafe because the EF provider can
        // assign auto-increment keys before SaveChangesAsync. The interceptor now
        // requires an explicit DataSubjectId (or UserId alias) and refuses to fall
        // back to the database-generated 'Id'.
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
        var db = new FallbackOnlyDbContext(interceptor);
        db.Database.EnsureCreated();

        db.Items.Add(new FallbackOnlyEntity());

        await db.Invoking(c => c.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no 'DataSubjectId'*");
    }

    private sealed class UserIdAliasEntity
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "user-alias-1";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "alias@example.com";
    }

    private sealed class UserIdAliasDbContext : DbContext
    {
        public UserIdAliasDbContext(SensitiveDataAuditInterceptor interceptor)
            : base(new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options) { }

        public DbSet<UserIdAliasEntity> Items => Set<UserIdAliasEntity>();
    }

    [Fact]
    public async Task AuditRecord_UsesUserId_AsLegacyAlias()
    {
        // UserId is supported as a legacy alias for DataSubjectId.
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
        var db = new UserIdAliasDbContext(interceptor);
        db.Database.EnsureCreated();

        db.Items.Add(new UserIdAliasEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryByDataSubjectAsync("user-alias-1");
        records.Should().NotBeEmpty();
    }

    private sealed class UniqueEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "unique-subject";
        public string UniqueValue { get; set; } = "same";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "unique@example.com";
    }

    private sealed class FailingDbContext : DbContext
    {
        private readonly SqliteConnection _connection;
        private readonly SensitiveDataAuditInterceptor _interceptor;

        public FailingDbContext(SqliteConnection connection, SensitiveDataAuditInterceptor interceptor)
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
    public async Task SaveChangesFailedAsync_RemovesPendingAuditRecords()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
        await using var db = new FailingDbContext(connection, interceptor);
        await db.Database.EnsureCreatedAsync();

        db.Items.Add(new UniqueEntity { DataSubjectId = "one", UniqueValue = "duplicate" });
        await db.SaveChangesAsync();

        db.Items.Add(new UniqueEntity { DataSubjectId = "two", UniqueValue = "duplicate" });
        await db.Invoking(c => c.SaveChangesAsync())
            .Should().ThrowAsync<DbUpdateException>();

        (await store.QueryByDataSubjectAsync("two")).Should().BeEmpty();
    }
}
