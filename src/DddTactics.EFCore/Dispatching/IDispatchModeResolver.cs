namespace DddTactics.EFCore;

/// <summary>
/// 解析某个事件处理器应使用的分发时机。默认实现读取处理器类上的 <see cref="DddTactics.Core.DispatchModeAttribute"/>。
/// 可替换为基于运行时上下文（如租户 ID）动态选择策略的实现（场景三）。
/// </summary>
public interface IDispatchModeResolver
{
    /// <summary>返回处理器的分发时机；返回 <see langword="null"/> 表示该处理器未声明时机（将被跳过，由告警提示）。</summary>
    DddTactics.Core.DomainEventDispatchMode? Resolve(Type handlerType, Type eventType);
}
