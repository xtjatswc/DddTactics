namespace DddTactics.Topology;

/// <summary>
/// 一条事件→处理器拓扑边。
/// </summary>
/// <param name="EventType">领域事件类型全名。</param>
/// <param name="HandlerType">处理器类型全名。</param>
/// <param name="DispatchMode">分发时机名称（<c>BeforeSave</c> / <c>AfterSave</c> / <c>None</c>）。</param>
public sealed record HandlerTopology(string EventType, string HandlerType, string DispatchMode);
