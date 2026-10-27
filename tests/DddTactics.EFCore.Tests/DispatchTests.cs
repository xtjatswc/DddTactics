using DddTactics.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DddTactics.EFCore.Tests;

public class DispatchTests
{
    private static IServiceProvider CreateProvider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddSingleton<NotificationLog>();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<TestDbContext>((sp, o) =>
            o.UseInMemoryDatabase(databaseName)
             .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));
        services.AddDddTacticsEfCore(ServiceLifetime.Scoped);
        register(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task BeforeSave_RollsBack_WhenHandlerThrows()
    {
        var provider = CreateProvider(s =>
            s.AddScoped<IDomainEventHandler<OrderPaid>, OrderPaidBeforeThrowHandler>());

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var order = new Order(Guid.NewGuid(), "ORD-1");
        order.MarkPaid();
        db.Orders.Add(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        using var verifyScope = provider.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.False(await verify.Orders.AnyAsync());
    }

    [Fact]
    public async Task BeforeSave_Commits_WhenHandlerSucceeds()
    {
        var provider = CreateProvider(s =>
            s.AddScoped<IDomainEventHandler<OrderPaid>, OrderPaidBeforeSucceedHandler>());

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var order = new Order(Guid.NewGuid(), "ORD-2");
        order.MarkPaid();
        db.Orders.Add(order);

        await db.SaveChangesAsync();

        using var verifyScope = provider.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.True(await verify.Orders.AnyAsync(o => o.Id == order.Id));
    }

    [Fact]
    public async Task AfterSave_DoesNotRollBack_WhenHandlerThrows()
    {
        var provider = CreateProvider(s =>
            s.AddScoped<IDomainEventHandler<OrderPaid>, OrderPaidAfterThrowHandler>());

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var order = new Order(Guid.NewGuid(), "ORD-3");
        order.MarkPaid();
        db.Orders.Add(order);

        // AfterSave 处理器失败，但主事务已提交，订单已持久化。
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        using var verifyScope = provider.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.True(await verify.Orders.AnyAsync(o => o.Id == order.Id));
    }

    [Fact]
    public async Task AfterSave_Commits_WhenHandlerSucceeds()
    {
        var provider = CreateProvider(s =>
            s.AddScoped<IDomainEventHandler<OrderPaid>, OrderPaidAfterSucceedHandler>());

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var order = new Order(Guid.NewGuid(), "ORD-4");
        order.MarkPaid();
        db.Orders.Add(order);

        await db.SaveChangesAsync();

        using var verifyScope = provider.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.True(await verify.Orders.AnyAsync(o => o.Id == order.Id));
    }

    [Fact]
    public async Task SameEvent_TwoHandlers_DifferentStrategies()
    {
        var provider = CreateProvider(s =>
        {
            s.AddScoped<IDomainEventHandler<OrderShipped>, OrderShippedBeforeHandler>();
            s.AddScoped<IDomainEventHandler<OrderShipped>, OrderShippedAfterHandler>();
        });

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var member = new Member(Guid.NewGuid());
        db.Members.Add(member);
        var order = new Order(Guid.NewGuid(), "ORD-5");
        order.Ship();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // BeforeSave 处理器更新会员；AfterSave 处理器记录通知；两者互不阻塞。
        var loaded = await db.Members.FirstAsync();
        Assert.Equal(order.Id, loaded.LastShippedOrderId);

        var log = scope.ServiceProvider.GetRequiredService<NotificationLog>();
        Assert.Contains(order.Id, log.Notified);
    }

    [Fact]
    public async Task BeforeSave_HandlerCannotCallSaveChanges()
    {
        var provider = CreateProvider(s =>
        {
            s.AddScoped<OrderPaidReentrantHandler>();
            s.AddScoped<IDomainEventHandler<OrderPaid>>(sp => sp.GetRequiredService<OrderPaidReentrantHandler>());
        });

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var order = new Order(Guid.NewGuid(), "ORD-6");
        order.MarkPaid();
        db.Orders.Add(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task BeforeSave_AsyncLocal_IsTrueDuringDispatch_AndFlowsAcrossThreads()
    {
        var provider = CreateProvider(s =>
        {
            s.AddScoped<OrderPaidCaptureHandler>();
            s.AddScoped<IDomainEventHandler<OrderPaid>>(sp => sp.GetRequiredService<OrderPaidCaptureHandler>());
        });

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var order = new Order(Guid.NewGuid(), "ORD-7");
        order.MarkPaid();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<OrderPaidCaptureHandler>();
        Assert.True(handler.CapturedDuringDispatch);
    }
}
