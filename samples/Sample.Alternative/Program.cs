using DddTactics.Core;
using DddTactics.EFCore;
using DddTactics.Topology;

namespace Sample.Alternative;

/// <summary>
/// 退出机制演示：不继承 AggregateRoot 基类，而是手动实现 IHasDomainEvents。
/// 拓扑识别仍只依赖 IHasDomainEvents 契约，无需任何基类。
/// </summary>
public sealed class ManualCart : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = new();

    public IReadOnlyList<IDomainEvent> DomainEvents => _events.AsReadOnly();

    public void ClearDomainEvents() => _events.Clear();

    public void Checkout() => _events.Add(new CartCheckedOut());
}

public sealed record CartCheckedOut() : IDomainEvent;

[DispatchMode(DomainEventDispatchMode.AfterSave)]
public sealed class CartCheckedOutHandler : IDomainEventHandler<CartCheckedOut>
{
    public Task HandleAsync(CartCheckedOut domainEvent, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[handler] 购物车已结算（AfterSave）");
        return Task.CompletedTask;
    }
}

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Sample.Alternative：手动 IHasDomainEvents + 源生成器回退 ===\n");

        var cart = new ManualCart();
        cart.Checkout();
        Console.WriteLine($"挂起事件数: {cart.DomainEvents.Count}");

        var report = new TopologyReporter().Build();
        Console.WriteLine("\n--- Mermaid (Dagre 布局) ---");
        Console.WriteLine(report.ToMermaid(useElk: false));
        Console.WriteLine("\n--- 文本树 ---");
        Console.WriteLine(report.ToTextTree());

        Console.WriteLine("\n提示：将 csproj 中的 EnableDddTacticsTopology 设为 false 可禁用源生成器（手动维护拓扑时）。");
    }
}
