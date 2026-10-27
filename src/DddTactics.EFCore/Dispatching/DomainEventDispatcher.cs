namespace DddTactics.EFCore;

/// <summary>
/// 默认领域事件分发器。按事件类型独立选择 <see cref="DddTactics.Core.DomainEventDispatchMode"/>，通过 DI 解析 <c>IDomainEventHandler&lt;TEvent&gt;</c>。
/// <para>同一事件可同时拥有 BeforeSave 与 AfterSave 处理器（场景一）。未标注 <see cref="DddTactics.Core.DispatchModeAttribute"/> 的处理器会被跳过。</para>
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _services;
    private readonly IDispatchModeResolver _modeResolver;

    // AsyncLocal 的值按执行流（ExecutionContext）隔离，因此静态字段在并发流之间不会串扰，
    // 同时保证同一流内的处理器（无论注入到哪个分发器实例）都能观察到 before 分发状态。
    private static readonly AsyncLocal<bool> _inBeforeSave = new();

    // 为每个处理器接口类型缓存一个表达式树编译的委托，避免反射 Invoke 包装异常、且零运行时反射开销。
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task>> HandleDelegates = new();

    public DomainEventDispatcher(IServiceProvider services, IDispatchModeResolver? modeResolver = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _modeResolver = modeResolver ?? new AttributeDispatchModeResolver();
    }

    public bool IsDispatchingBeforeSave => _inBeforeSave.Value;

    public async Task DispatchBeforeSaveAsync(IEnumerable<DddTactics.Core.IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        _inBeforeSave.Value = true;
        try
        {
            await DispatchByModeAsync(events, DddTactics.Core.DomainEventDispatchMode.BeforeSave, cancellationToken);
        }
        finally
        {
            _inBeforeSave.Value = false;
        }
    }

    public Task DispatchAfterSaveAsync(IEnumerable<DddTactics.Core.IDomainEvent> events, CancellationToken cancellationToken = default)
        => DispatchByModeAsync(events, DddTactics.Core.DomainEventDispatchMode.AfterSave, cancellationToken);

    private async Task DispatchByModeAsync(IEnumerable<DddTactics.Core.IDomainEvent> events, DddTactics.Core.DomainEventDispatchMode mode, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var eventType = domainEvent.GetType();
            var handlerInterface = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handlers = _services.GetServices(handlerInterface);

            foreach (var handler in handlers)
            {
                if (handler is null)
                {
                    continue;
                }

                var resolvedMode = _modeResolver.Resolve(handler.GetType(), eventType);
                if (resolvedMode != mode)
                {
                    continue;
                }

                var invoke = HandleDelegates.GetOrAdd(handlerInterface, static iface =>
                {
                    var eventArg = iface.GetGenericArguments()[0];
                    var method = iface.GetMethod("HandleAsync", new[] { eventArg, typeof(CancellationToken) })
                        ?? throw new InvalidOperationException($"找不到 {iface} 上的 HandleAsync 方法。");
                    var handlerParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "handler");
                    var eventParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "domainEvent");
                    var ctParam = System.Linq.Expressions.Expression.Parameter(typeof(CancellationToken), "ct");
                    var call = System.Linq.Expressions.Expression.Call(
                        System.Linq.Expressions.Expression.Convert(handlerParam, iface),
                        method,
                        System.Linq.Expressions.Expression.Convert(eventParam, eventArg),
                        ctParam);
                    return System.Linq.Expressions.Expression
                        .Lambda<Func<object, object, CancellationToken, Task>>(call, handlerParam, eventParam, ctParam)
                        .Compile();
                });

                await invoke(handler, domainEvent, cancellationToken);
            }
        }
    }
}
