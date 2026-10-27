namespace DddTactics.Core;

/// <summary>
/// 标注在某个 <c>IDomainEventHandler&lt;TEvent&gt;</c> 实现类上，声明该处理器的分发时机。
/// <para>运行时分发策略在编译期不可见，因此需要显式标注；拓扑生成器据此为边打上策略标签。</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DispatchModeAttribute : Attribute
{
    public DomainEventDispatchMode Mode { get; }

    public DispatchModeAttribute(DomainEventDispatchMode mode) => Mode = mode;
}
