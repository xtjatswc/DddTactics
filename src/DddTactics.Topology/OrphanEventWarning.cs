namespace DddTactics.Topology;

/// <summary>
/// 孤儿事件告警：某 <see cref="DddTactics.Core.IDomainEvent"/> 类型存在（被某聚合发出），但没有任何标注了 <see cref="DddTactics.Core.DispatchModeAttribute"/> 的处理器。
/// </summary>
/// <param name="EventType">事件类型全名。</param>
/// <param name="AggregateType">发出该事件的聚合类型；无法在编译期静态确定时为空。</param>
public sealed record OrphanEventWarning(string EventType, string? AggregateType)
{
    public string Message =>
        AggregateType is null
            ? $"孤儿事件：'{EventType}' 没有注册任何标注了 [DispatchMode] 的处理器。"
            : $"孤儿事件：'{EventType}'（由聚合 '{AggregateType}' 发出）没有注册任何标注了 [DispatchMode] 的处理器。";
}
