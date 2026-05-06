using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using SensitiveFlow.EFCore.Tests.Stores;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
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
}
