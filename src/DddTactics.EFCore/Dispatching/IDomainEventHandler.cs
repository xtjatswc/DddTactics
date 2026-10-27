namespace DddTactics.EFCore;

/// <summary>
/// 领域事件处理器。一个事件类型可拥有多个处理器，每个处理器通过 <see cref="DddTactics.Core.DispatchModeAttribute"/> 独立声明分发时机。
/// 除 BeforeSave 处理器禁止调用 <c>SaveChanges</c> 外，对处理器职责无约束（不强制幂等、不限制发出命令数量）。
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : DddTactics.Core.IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
