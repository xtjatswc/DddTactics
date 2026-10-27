namespace DddTactics.EFCore;

/// <summary>
/// 默认分发时机解析器：读取处理器类型上的 <see cref="DddTactics.Core.DispatchModeAttribute"/>。
/// </summary>
public sealed class AttributeDispatchModeResolver : IDispatchModeResolver
{
    public DddTactics.Core.DomainEventDispatchMode? Resolve(Type handlerType, Type eventType)
    {
        var attribute = handlerType.GetCustomAttribute<DddTactics.Core.DispatchModeAttribute>();
        return attribute?.Mode;
    }
}
