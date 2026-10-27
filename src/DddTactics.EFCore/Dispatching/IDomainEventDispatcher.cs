namespace DddTactics.EFCore;

/// <summary>
/// 领域事件分发器。由 <see cref="DomainEventDispatchInterceptor"/> 在 SaveChanges 生命周期中调用。
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>分发所有 BeforeSave 处理器。期间 <see cref="IsDispatchingBeforeSave"/> 为 <see langword="true"/>。</summary>
    Task DispatchBeforeSaveAsync(IEnumerable<DddTactics.Core.IDomainEvent> events, CancellationToken cancellationToken = default);

    /// <summary>分发所有 AfterSave 处理器（事务已提交；异常不回滚主事务）。</summary>
    Task DispatchAfterSaveAsync(IEnumerable<DddTactics.Core.IDomainEvent> events, CancellationToken cancellationToken = default);

    /// <summary>当前是否正处于 BeforeSave 分发阶段（用于拦截 BeforeSave 处理器中的 SaveChanges 调用）。</summary>
    bool IsDispatchingBeforeSave { get; }
}
