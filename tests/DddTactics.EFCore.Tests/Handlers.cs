using DddTactics.Core;
using Microsoft.EntityFrameworkCore;

namespace DddTactics.EFCore.Tests;

[DispatchMode(DomainEventDispatchMode.BeforeSave)]
public sealed class OrderPaidBeforeSucceedHandler : IDomainEventHandler<OrderPaid>
{
    public Task HandleAsync(OrderPaid domainEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

[DispatchMode(DomainEventDispatchMode.BeforeSave)]
public sealed class OrderPaidBeforeThrowHandler : IDomainEventHandler<OrderPaid>
{
    public Task HandleAsync(OrderPaid domainEvent, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("BeforeSave 处理器失败");
}

[DispatchMode(DomainEventDispatchMode.AfterSave)]
public sealed class OrderPaidAfterSucceedHandler : IDomainEventHandler<OrderPaid>
{
    public Task HandleAsync(OrderPaid domainEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

[DispatchMode(DomainEventDispatchMode.AfterSave)]
public sealed class OrderPaidAfterThrowHandler : IDomainEventHandler<OrderPaid>
{
    public Task HandleAsync(OrderPaid domainEvent, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("AfterSave 处理器失败");
}

[DispatchMode(DomainEventDispatchMode.BeforeSave)]
public sealed class OrderShippedBeforeHandler : IDomainEventHandler<OrderShipped>
{
    private readonly TestDbContext _db;

    public OrderShippedBeforeHandler(TestDbContext db) => _db = db;

    public async Task HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken = default)
    {
        // 在 SavingChanges 阶段，实体尚未写入 InMemory 表，需从本地跟踪集合读取。
        var member = await Task.Run(() => _db.Members.Local.First());
        member.RecordShip(domainEvent.OrderId);
        // 注意：BeforeSave 处理器禁止调用 SaveChanges；变更随主事务一并提交。
    }
}

[DispatchMode(DomainEventDispatchMode.AfterSave)]
public sealed class OrderShippedAfterHandler : IDomainEventHandler<OrderShipped>
{
    private readonly NotificationLog _log;

    public OrderShippedAfterHandler(NotificationLog log) => _log = log;

    public Task HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken = default)
    {
        _log.Add(domainEvent.OrderId);
        return Task.CompletedTask;
    }
}

[DispatchMode(DomainEventDispatchMode.BeforeSave)]
public sealed class OrderPaidCaptureHandler : IDomainEventHandler<OrderPaid>
{
    private readonly IDomainEventDispatcher _dispatcher;

    public bool CapturedDuringDispatch { get; private set; }

    public OrderPaidCaptureHandler(IDomainEventDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task HandleAsync(OrderPaid domainEvent, CancellationToken cancellationToken = default)
    {
        var onOriginalFlow = _dispatcher.IsDispatchingBeforeSave;
        var onOtherThread = await Task.Run(() => _dispatcher.IsDispatchingBeforeSave);
        CapturedDuringDispatch = onOriginalFlow && onOtherThread;
    }
}

[DispatchMode(DomainEventDispatchMode.BeforeSave)]
public sealed class OrderPaidReentrantHandler : IDomainEventHandler<OrderPaid>
{
    private readonly TestDbContext _db;

    public OrderPaidReentrantHandler(TestDbContext db) => _db = db;

    public Task HandleAsync(OrderPaid domainEvent, CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken); // 应被拦截器禁止
}
