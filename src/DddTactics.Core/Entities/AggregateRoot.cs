namespace DddTactics.Core;

/// <summary>
/// 聚合根基类。约束 <typeparamref name="TId"/> 为 <see langword="struct"/>（引导使用强类型 ID，但不强制）。
/// 通过 <see cref="IHasDomainEvents"/> 持有领域事件；事件在 <c>AddDomainEvent</c> 时收集，由 EFCore 分发器在 SaveChanges 时提取。
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(TId id) : base(id)
    {
    }

    IReadOnlyList<IDomainEvent> IHasDomainEvents.DomainEvents => _domainEvents.AsReadOnly();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>向聚合添加一条待分发领域事件。</summary>
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>移除一条尚未分发的领域事件（极少数需要撤回时使用）。</summary>
    protected void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
