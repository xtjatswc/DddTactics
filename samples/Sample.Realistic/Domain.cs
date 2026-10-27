using DddTactics.Core;
using Microsoft.EntityFrameworkCore;

namespace Sample.Realistic;

public sealed class Order : AggregateRoot<Guid>
{
    public string Number { get; private set; } = string.Empty;

    public bool IsShipped { get; private set; }

    private Order()
    {
    }

    public Order(Guid id, string number) : base(id) => Number = number;

    public void Ship()
    {
        IsShipped = true;
        AddDomainEvent(new OrderShipped(Id));
        // 该事件没有注册任何处理器 → 拓扑报告会将其标记为孤儿事件。
        AddDomainEvent(new AuditLogged(Id));
    }
}

public sealed class Member : AggregateRoot<Guid>
{
    public Guid? LastShippedOrderId { get; private set; }

    private Member()
    {
    }

    public Member(Guid id) : base(id)
    {
    }

    public void RecordShip(Guid orderId) => LastShippedOrderId = orderId;
}

public sealed record OrderShipped(Guid OrderId) : IDomainEvent;

/// <summary>
/// 孤儿事件：存在（被发出）但没有注册任何处理器的领域事件，用于演示拓扑告警。
/// </summary>
public sealed record AuditLogged(Guid OrderId) : IDomainEvent;

public sealed class NotificationLog
{
    public System.Collections.Generic.List<Guid> Notified { get; } = new();

    public void Add(Guid orderId) => Notified.Add(orderId);
}

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Member> Members => Set<Member>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b => b.HasKey(o => o.Id));
        modelBuilder.Entity<Member>(b => b.HasKey(m => m.Id));
    }
}
