using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Integration.Tests.Stores;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.Context;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.EFCore.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace SensitiveFlow.Integration.Tests;

/// <summary>
/// End-to-end integration: EF Core interceptor + InMemoryAuditStore wired via DI.
/// </summary>
public sealed class EFCoreAuditIntegrationTests
{
    private sealed class OrderEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "customer-1";

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "order@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Health)]
        public string CardToken { get; set; } = "tok_xxx";

        public string OrderNumber { get; set; } = "ORD-001";
    }

    private sealed class OrderDbContext : DbContext
    {
        private readonly SensitiveDataAuditInterceptor _interceptor;

        public OrderDbContext(SensitiveDataAuditInterceptor interceptor)
            : base(new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options)
        {
            _interceptor = interceptor;
        }

        public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    }

    private static (IServiceProvider, OrderDbContext) BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();
        services.AddSensitiveFlowEFCore();

        var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IAuditStore>();
        var context = provider.GetRequiredService<IAuditContext>();
        var interceptor = new SensitiveDataAuditInterceptor(store, context);
        var db = new OrderDbContext(interceptor);
        db.Database.EnsureCreated();

        return (provider, db);
    }

    [Fact]
    public async Task AddOrder_EmitsAuditRecords_ForSensitiveFields()
    {
        var (provider, db) = BuildServices();
        var store = (InMemoryAuditStore)provider.GetRequiredService<IAuditStore>();

        db.Orders.Add(new OrderEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().HaveCount(2);
        records.Select(r => r.Field).Should().BeEquivalentTo(["Email", "CardToken"]);
    }

    [Fact]
    public async Task AddOrder_PublicFields_NotAudited()
    {
        var (provider, db) = BuildServices();
        var store = (InMemoryAuditStore)provider.GetRequiredService<IAuditStore>();

        db.Orders.Add(new OrderEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().NotContain(r => r.Field == "OrderNumber");
    }

    [Fact]
    public async Task UpdateSensitiveField_EmitsUpdateOperation()
    {
        var (provider, db) = BuildServices();
        var store = (InMemoryAuditStore)provider.GetRequiredService<IAuditStore>();

        var order = new OrderEntity();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        order.Email = "new@example.com";
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().Contain(r => r.Operation == AuditOperation.Update && r.Field == "Email");
    }

    [Fact]
    public async Task DeleteOrder_EmitsDeleteAuditRecords()
    {
        var (provider, db) = BuildServices();
        var store = (InMemoryAuditStore)provider.GetRequiredService<IAuditStore>();

        var order = new OrderEntity();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.Orders.Remove(order);
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().Contain(r => r.Operation == AuditOperation.Delete);
    }

    [Fact]
    public async Task QueryByDataSubject_ReturnsOnlyMatchingRecords()
    {
        var (provider, db) = BuildServices();
        var store = (InMemoryAuditStore)provider.GetRequiredService<IAuditStore>();

        db.Orders.Add(new OrderEntity { DataSubjectId = "alice" });
        db.Orders.Add(new OrderEntity { DataSubjectId = "bob" });
        await db.SaveChangesAsync();

        var aliceRecords = await store.QueryByDataSubjectAsync("alice");
        aliceRecords.Should().OnlyContain(r => r.DataSubjectId == "alice");
    }

    [Fact]
    public async Task NullAuditContext_ActorId_IsNullInRecord()
    {
        var (provider, db) = BuildServices();
        var store = (InMemoryAuditStore)provider.GetRequiredService<IAuditStore>();

        db.Orders.Add(new OrderEntity());
        await db.SaveChangesAsync();

        var records = await store.QueryAsync();
        records.Should().OnlyContain(r => r.ActorId == null);
    }
}
