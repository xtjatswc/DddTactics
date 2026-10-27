using DddTactics.Core;
using Microsoft.EntityFrameworkCore;

namespace DddTactics.EFCore.Tests;

/// <summary>测试用聚合：订单。</summary>
public sealed class Order : AggregateRoot<Guid>
{
    public string Number { get; private set; } = "";

    public bool IsPaid { get; private set; }

    public bool IsShipped { get; private set; }

    private Order()
    {
    }

    public Order(Guid id, string number) : base(id) => Number = number;

    public void MarkPaid()
    {
        IsPaid = true;
        AddDomainEvent(new OrderPaid(Id));
    }

    public void Ship()
    {
        IsShipped = true;
        AddDomainEvent(new OrderShipped(Id));
    }
}

/// <summary>测试用聚合：会员。</summary>
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

public sealed record OrderPaid(Guid OrderId) : IDomainEvent;

public sealed record OrderShipped(Guid OrderId) : IDomainEvent;

public sealed class NotificationLog
{
    public List<Guid> Notified { get; } = new();

    public void Add(Guid orderId) => Notified.Add(orderId);
}

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
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
