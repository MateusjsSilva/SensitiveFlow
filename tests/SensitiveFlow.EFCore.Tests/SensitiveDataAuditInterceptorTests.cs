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
        public DbSet<UserEntityWithIntDataSubjectId> UsersWithIntSubject => Set<UserEntityWithIntDataSubjectId>();
        public DbSet<UserEntityWithGuidDataSubjectId> UsersWithGuidSubject => Set<UserEntityWithGuidDataSubjectId>();

        private static DbContextOptions BuildOptions(SensitiveDataAuditInterceptor interceptor)
            => new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;
    }

    private sealed class SqliteAuditDbContext : DbContext
    {
        private readonly SqliteConnection _connection;
        private readonly SensitiveDataAuditInterceptor _interceptor;

        public SqliteAuditDbContext(SqliteConnection connection, SensitiveDataAuditInterceptor interceptor)
        {
            _connection = connection;
            _interceptor = interceptor;
        }

        public DbSet<UserEntity> Users => Set<UserEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(_connection).AddInterceptors(_interceptor);
    }

    private sealed class AuditRedactionEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "audit-redaction-subject";

        [PersonalData(Category = DataCategory.Contact)]
        [Redaction(Audit = OutputRedactionAction.Mask)]
        public string Email { get; set; } = "maria@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [Redaction(Audit = OutputRedactionAction.Redact)]
        public string TaxId { get; set; } = "12345678900";

        [PersonalData(Category = DataCategory.Other)]
        [Redaction(Audit = OutputRedactionAction.Omit)]
        public string InternalNote { get; set; } = "omit me";

        [PersonalData(Category = DataCategory.Identification)]
        [Redaction(Audit = OutputRedactionAction.Pseudonymize)]
        public string Name { get; set; } = "Maria";
    }

    private sealed class AuditRedactionDbContext : DbContext
    {
        private readonly SensitiveDataAuditInterceptor _interceptor;

        public AuditRedactionDbContext(SensitiveDataAuditInterceptor interceptor)
            : base(new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options)
        {
            _interceptor = interceptor;
        }

        public DbSet<AuditRedactionEntity> Items => Set<AuditRedactionEntity>();
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
    public async Task UpdateSensitiveField_EmitsUpdateRecord_WhenAutoDetectChangesIsDisabled()
    {
        var (db, store) = BuildContext();
        var entity = new UserEntity();
        db.Users.Add(entity);
        await db.SaveChangesAsync();

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        entity.Email = "changed@example.com";
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().Contain(r => r.Operation == AuditOperation.Update && r.Field == "Email");
    }

    [Fact]
    public async Task ExecuteUpdateAsync_DoesNotEmitAuditRecords_BecauseItBypassesSaveChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
        await using var db = new SqliteAuditDbContext(connection, interceptor);
        await db.Database.EnsureCreatedAsync();

        db.Users.Add(new UserEntity());
        await db.SaveChangesAsync();

        await db.Users.ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.Email, "bulk@example.com"));

        var records = await store.QueryAsync();
        records.Should().Contain(r => r.Operation == AuditOperation.Create && r.Field == "Email");
        records.Should().NotContain(r => r.Operation == AuditOperation.Update);
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

    [Fact]
    public async Task AuditRecord_RespectsContextualAuditRedaction()
    {
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(
            store,
            NullAuditContext.Instance,
            new FakePseudonymizer());
        var db = new AuditRedactionDbContext(interceptor);
        db.Database.EnsureCreated();

        db.Items.Add(new AuditRedactionEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryByDataSubjectAsync("audit-redaction-subject");
        // All sensitive fields are audited, regardless of [Redaction] attribute
        // Redaction attributes control HOW the value is stored in Details (Mask, Redact, Pseudonymize, None)
        // But Omit is no longer honored — all fields are always included in the audit trail
        records.Should().Contain(r => r.Field == "Email" && r.Details!.Contains("m****@example.com", StringComparison.Ordinal));
        records.Should().Contain(r => r.Field == "TaxId" && r.Details!.Contains("[REDACTED]", StringComparison.Ordinal));
        records.Should().Contain(r => r.Field == "Name" && r.Details!.Contains("token-Maria", StringComparison.Ordinal));
        // InternalNote is now audited with Omit action — Omit is no longer supported
        // It's included in audit as it was with other redaction actions
        records.Should().Contain(r => r.Field == "InternalNote" && r.Details!.Contains("Audit redaction action: Omit", StringComparison.Ordinal));
    }

    private sealed class UniqueEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "unique-subject";
        public string UniqueValue { get; set; } = "same";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "unique@example.com";
    }

    private sealed class FakePseudonymizer : IPseudonymizer
    {
        public string Pseudonymize(string value) => $"token-{value}";

        public Task<string> PseudonymizeAsync(string value, CancellationToken cancellationToken = default)
            => Task.FromResult(Pseudonymize(value));

        public string Reverse(string token) => token.Replace("token-", string.Empty, StringComparison.Ordinal);

        public Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(Reverse(token));

        public bool CanPseudonymize(string value) => true;
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

    [Fact]
    public async Task SaveChangesAsync_WithIntDataSubjectId_Throws()
    {
        // Entity with int DataSubjectId should throw at SaveChanges time
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);

        using var db = new TestDbContext(interceptor);

        var entity = new UserEntityWithIntDataSubjectId
        {
            Id = 1,
            DataSubjectId = 42,
            Email = "test@example.com",
            HealthNote = "test"
        };

        db.Add(entity);

        var action = async () => await db.SaveChangesAsync();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DataSubjectId must be 'string' or 'Guid'*");
    }

    [Fact]
    public async Task SaveChangesAsync_WithGuidDataSubjectId_Succeeds()
    {
        // Entity with Guid DataSubjectId should work fine
        var store = new InMemoryAuditStore();
        var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);

        using var db = new TestDbContext(interceptor);

        var subjectId = Guid.NewGuid();
        var entity = new UserEntityWithGuidDataSubjectId
        {
            Id = 1,
            DataSubjectId = subjectId,
            Email = "test@example.com",
            HealthNote = "test"
        };

        db.Add(entity);
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().HaveCount(2);  // Email + HealthNote
        records.Should().AllSatisfy(r => r.DataSubjectId.Should().Be(subjectId.ToString()));
    }

    // Test entities for DataSubjectId type validation
    private sealed class UserEntityWithIntDataSubjectId
    {
        public int Id { get; set; }
        public int DataSubjectId { get; set; }

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "test@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Health)]
        public string HealthNote { get; set; } = "none";
    }

    private sealed class UserEntityWithGuidDataSubjectId
    {
        public int Id { get; set; }
        public Guid DataSubjectId { get; set; }

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "test@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Health)]
        public string HealthNote { get; set; } = "none";
    }
}
