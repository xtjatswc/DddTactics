namespace DddTactics.EFCore;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/>，作为领域事件分发的触发器：
/// <list type="bullet">
///   <item><description><c>SavingChanges</c>：提取事件并同步分发 BeforeSave 处理器（与聚合变更同事务）；AfterSave 事件暂存于 <see cref="ConditionalWeakTable{TKey,TValue}"/>。</description></item>
///   <item><description><c>SavedChanges</c>：提交后分发 AfterSave 处理器。</description></item>
///   <item><description><c>SaveChangesFailed</c>：丢弃暂存的 AfterSave 事件。</description></item>
/// </list>
/// 分发器从 <see cref="DbContext"/> 的 DI 容器中按作用域解析（拦截器本身可为单例）。
/// BeforeSave 处理器若再次触发 <c>SaveChanges</c>（即调用 <c>SaveChanges</c>），将抛出 <see cref="InvalidOperationException"/>。
/// </summary>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<DddTactics.Core.IDomainEvent>> _afterSaveEvents = new();

    private static List<DddTactics.Core.IDomainEvent> CollectEvents(DbContext context)
    {
        var events = new List<DddTactics.Core.IDomainEvent>();
        foreach (var entry in context.ChangeTracker.Entries<DddTactics.Core.IHasDomainEvents>())
        {
            if (entry.Entity.DomainEvents.Count > 0)
            {
                events.AddRange(entry.Entity.DomainEvents);
            }
        }

        return events;
    }

    private static void ClearEvents(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<DddTactics.Core.IHasDomainEvents>())
        {
            entry.Entity.ClearDomainEvents();
        }
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            var dispatcher = context.GetService<IDomainEventDispatcher>();
            if (dispatcher.IsDispatchingBeforeSave)
            {
                throw new InvalidOperationException("在 BeforeSave 领域事件处理器中调用 SaveChanges 是被禁止的。");
            }

            DispatchBeforeSave(context, dispatcher).GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var dispatcher = context.GetService<IDomainEventDispatcher>();
            if (dispatcher.IsDispatchingBeforeSave)
            {
                throw new InvalidOperationException("在 BeforeSave 领域事件处理器中调用 SaveChanges 是被禁止的。");
            }

            await DispatchBeforeSave(context, dispatcher, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchBeforeSave(DbContext context, IDomainEventDispatcher dispatcher, CancellationToken cancellationToken = default)
    {
        var events = CollectEvents(context);
        if (events.Count == 0)
        {
            return;
        }

        await dispatcher.DispatchBeforeSaveAsync(events, cancellationToken);

        _afterSaveEvents.Remove(context, out _);
        _afterSaveEvents.Add(context, events);
        ClearEvents(context);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context)
        {
            DispatchAfterSave(context).GetAwaiter().GetResult();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await DispatchAfterSave(context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context)
        {
            _afterSaveEvents.Remove(context, out _);
        }

        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            _afterSaveEvents.Remove(context, out _);
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private async Task DispatchAfterSave(DbContext context, CancellationToken cancellationToken = default)
    {
        if (_afterSaveEvents.TryGetValue(context, out var events) && events is { Count: > 0 })
        {
            _afterSaveEvents.Remove(context, out _);
            var dispatcher = context.GetService<IDomainEventDispatcher>();
            await dispatcher.DispatchAfterSaveAsync(events, cancellationToken);
        }
    }
}
