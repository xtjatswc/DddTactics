namespace DddTactics.Core;

/// <summary>
/// 指示聚合根持有尚未分发的领域事件。拓扑识别只需此契约，无需额外结构标注。
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
