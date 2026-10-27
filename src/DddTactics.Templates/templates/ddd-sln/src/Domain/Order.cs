using DddTactics.Core;

namespace Domain;

/// <summary>
/// 由 dotnet new ddd-sln 生成的示例聚合根。
/// </summary>
public sealed class Order : AggregateRoot<Guid>
{
    public string Number { get; private set; } = string.Empty;

    private Order()
    {
    }

    public Order(Guid id, string number) : base(id) => Number = number;

    public void Place() => AddDomainEvent(new OrderPlaced(Id));
}

public sealed record OrderPlaced(Guid OrderId) : IDomainEvent;
