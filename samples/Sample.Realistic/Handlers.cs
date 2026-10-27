using DddTactics.Core;
using DddTactics.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Realistic;

/// <summary>
/// 同一事件 OrderShipped 的 BeforeSave 处理器：更新会员聚合（与主事务同提交）。
/// </summary>
[DispatchMode(DomainEventDispatchMode.BeforeSave)]
public sealed class OrderShippedBeforeHandler : IDomainEventHandler<OrderShipped>
{
    private readonly AppDbContext _db;

    public OrderShippedBeforeHandler(AppDbContext db) => _db = db;

    public async Task HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken = default)
    {
        var member = await Task.Run(() => _db.Members.Local.First());
        member.RecordShip(domainEvent.OrderId);
        Console.WriteLine($"[BeforeSave] 更新会员最近发货订单: {domainEvent.OrderId}");
    }
}

/// <summary>
/// 同一事件 OrderShipped 的 AfterSave 处理器：提交后通知（失败不影响主事务）。
/// </summary>
[DispatchMode(DomainEventDispatchMode.AfterSave)]
public sealed class OrderShippedAfterHandler : IDomainEventHandler<OrderShipped>
{
    private readonly NotificationLog _log;

    public OrderShippedAfterHandler(NotificationLog log) => _log = log;

    public Task HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken = default)
    {
        _log.Add(domainEvent.OrderId);
        Console.WriteLine($"[AfterSave] 发送发货通知: {domainEvent.OrderId}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// 演示 TACT001：聚合根直接引用另一聚合根（会被分析器告警，提示应通过 ID 引用）。
/// </summary>
public sealed class Customer : AggregateRoot<Guid>
{
    public Order? LastOrder { get; set; }

    public Customer(Guid id) : base(id)
    {
    }
}
