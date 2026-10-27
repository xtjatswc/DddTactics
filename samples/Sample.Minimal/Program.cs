using DddTactics.Core;
using DddTactics.EFCore;
using DddTactics.Topology;

namespace Sample.Minimal;

/// <summary>
/// 强类型 ID：readonly record struct 实现 IStrongId。
/// </summary>
public readonly record struct LoyaltyProgramId(Guid Value) : IStrongId<LoyaltyProgramId, Guid>
{
    public static LoyaltyProgramId New() => new(Guid.NewGuid());

    public static LoyaltyProgramId From(Guid value) => new(value);
}

/// <summary>
/// 聚合根：发布后不可编辑的不变量。
/// </summary>
public sealed class LoyaltyProgram : AggregateRoot<LoyaltyProgramId>
{
    public bool IsPublished { get; private set; }

    public string Name { get; private set; } = string.Empty;

    private LoyaltyProgram()
    {
    }

    public LoyaltyProgram(LoyaltyProgramId id, string name) : base(id) => Name = name;

    public void Publish()
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("已发布，不可重复发布。");
        }

        IsPublished = true;
        AddDomainEvent(new LoyaltyProgramPublished(Id));
    }

    public void Rename(string name)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("发布后不可编辑。");
        }

        Name = name;
    }
}

public sealed record LoyaltyProgramPublished(LoyaltyProgramId Id) : IDomainEvent;

/// <summary>
/// 处理器通过 [DispatchMode] 显式声明策略；拓扑结构由源生成器自动产出（零额外标注）。
/// </summary>
[DispatchMode(DomainEventDispatchMode.AfterSave)]
public sealed class LoyaltyProgramPublishedHandler : IDomainEventHandler<LoyaltyProgramPublished>
{
    public Task HandleAsync(LoyaltyProgramPublished domainEvent, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[handler] 已发布权益 {domainEvent.Id}（AfterSave，可异步写读模型）");
        return Task.CompletedTask;
    }
}

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Sample.Minimal：纯领域层不变量 + 拓扑自动产出 ===\n");

        var program = new LoyaltyProgram(LoyaltyProgramId.New(), "基础权益");
        program.Publish();
        Console.WriteLine($"已发布: {program.IsPublished}；挂起事件数: {program.DomainEvents.Count}");

        try
        {
            program.Rename("高级权益");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"不变量拦截: {ex.Message}");
        }

        // 拓扑作为分发副产品自动产出（编译期源生成器，无需运行数据库/消息队列）。
        var report = new TopologyReporter().Build();
        Console.WriteLine("\n--- Mermaid (ELK 布局) ---");
        Console.WriteLine(report.ToMermaid(useElk: true));
        Console.WriteLine("\n--- 文本树 ---");
        Console.WriteLine(report.ToTextTree());
    }
}
