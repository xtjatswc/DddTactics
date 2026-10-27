namespace DddTactics.Topology;

/// <summary>
/// 源生成器向拓扑报告器输出处理器信息的接收器。
/// </summary>
public interface ITopologySink
{
    /// <summary>记录一条事件→处理器的边，<paramref name="dispatchMode"/> 为 <see cref="DddTactics.Core.DomainEventDispatchMode"/> 的名称。</summary>
    void AddHandler(string eventType, string handlerType, string dispatchMode);
}
