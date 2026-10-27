namespace DddTactics.Core;

/// <summary>
/// 领域事件分发时机。配置粒度是事件类型（通过处理器上的 <see cref="DispatchModeAttribute"/> 标注）。
/// <para><b>无默认值</b>：未显式标注的处理器在运行时不会被分发，并由分析器/拓扑告警提示。</para>
/// </summary>
public enum DomainEventDispatchMode
{
    /// <summary>在 <c>SaveChanges</c> 落库之前、与聚合变更同一事务中同步执行。处理器中禁止调用 <c>SaveChanges</c>。</summary>
    BeforeSave = 0,

    /// <summary>在 <c>SaveChanges</c> 成功提交之后执行。失败不影响主事务，不保证 Exactly-Once。</summary>
    AfterSave = 1
}
