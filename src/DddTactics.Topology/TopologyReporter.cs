namespace DddTactics.Topology;

/// <summary>
/// 拓扑报告器。消费源生成器生成的 <c>DddTacticsTopologyRegistration</c> 注册类，产出：
/// <list type="bullet">
///   <item><description>Mermaid 图（默认 ELK 布局，可切换 Dagre）；边标注 BeforeSave/AfterSave。</description></item>
///   <item><description>纯文本树。</description></item>
///   <item><description>孤儿事件告警。</description></item>
/// </list>
/// </summary>
public sealed class TopologyReporter
{
    private readonly List<HandlerTopology> _handlers = new();

    private sealed class CollectingSink : ITopologySink
    {
        private readonly List<HandlerTopology> _target;

        public CollectingSink(List<HandlerTopology> target) => _target = target;

        public void AddHandler(string eventType, string handlerType, string dispatchMode)
            => _target.Add(new HandlerTopology(eventType, handlerType, dispatchMode));
    }

    /// <summary>
    /// 从指定程序集（默认为入口程序集）构建拓扑报告。
    /// 程序集中必须包含源生成器生成的 <see cref="TopologyRegistrationAttribute"/> 标记注册类。
    /// </summary>
    public TopologyReport Build(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();

        var sink = new CollectingSink(_handlers);
        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<TopologyRegistrationAttribute>() is null)
            {
                continue;
            }

            var register = type.GetMethod("Register", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ITopologySink) }, null);
            register?.Invoke(null, new object[] { sink });
        }

        var orphans = DetectOrphans(assembly);
        return new TopologyReport(_handlers.ToList(), orphans);
    }

    private IReadOnlyList<OrphanEventWarning> DetectOrphans(Assembly assembly)
    {
        var handledEvents = new HashSet<string>(_handlers.Select(h => h.EventType), StringComparer.Ordinal);
        var orphans = new List<OrphanEventWarning>();

        foreach (var type in assembly.DefinedTypes)
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (typeof(DddTactics.Core.IDomainEvent).IsAssignableFrom(type) && type != typeof(DddTactics.Core.IDomainEvent))
            {
                var fullName = type.FullName ?? type.Name;
                if (!handledEvents.Contains(fullName))
                {
                    orphans.Add(new OrphanEventWarning(fullName, null));
                }
            }
        }

        return orphans;
    }
}

/// <summary>
/// 拓扑报告结果。
/// </summary>
public sealed class TopologyReport
{
    public IReadOnlyList<HandlerTopology> Handlers { get; }

    public IReadOnlyList<OrphanEventWarning> Orphans { get; }

    public TopologyReport(IReadOnlyList<HandlerTopology> handlers, IReadOnlyList<OrphanEventWarning> orphans)
    {
        Handlers = handlers;
        Orphans = orphans;
    }

    /// <summary>生成 Mermaid 流程图。<paramref name="useElk"/> 为 true（默认）使用 ELK 布局，否则使用 Dagre（默认渲染器）。</summary>
    public string ToMermaid(bool useElk = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine(useElk
            ? "%%{init: {\"flowchart\": {\"defaultRenderer\": \"elk\"}} }%%"
            : "%%{init: {\"flowchart\": {\"defaultRenderer\": \"dagre\"}} }%%");
        sb.AppendLine("flowchart LR");

        if (Handlers.Count == 0 && Orphans.Count == 0)
        {
            sb.AppendLine("    %% 未检测到任何领域事件处理器拓扑");
            return sb.ToString();
        }

        foreach (var handler in Handlers)
        {
            var eventId = Sanitize(handler.EventType);
            var handlerId = Sanitize(handler.HandlerType);
            var eventLabel = ShortName(handler.EventType);
            var handlerLabel = ShortName(handler.HandlerType);
            sb.AppendLine($"    {eventId}[\"{eventLabel}\"]");
            sb.AppendLine($"    {handlerId}[\"{handlerLabel}\"]");
            sb.AppendLine($"    {eventId} -->|{handler.DispatchMode}| {handlerId}");
        }

        foreach (var orphan in Orphans)
        {
            var id = Sanitize(orphan.EventType);
            sb.AppendLine($"    {id}[\"{ShortName(orphan.EventType)}\"]:::orphan");
        }

        if (Orphans.Count > 0)
        {
            sb.AppendLine("    classDef orphan stroke:#e03131,stroke-width:2px;");
        }

        return sb.ToString();
    }

    /// <summary>生成纯文本树。</summary>
    public string ToTextTree()
    {
        var sb = new StringBuilder();
        sb.AppendLine("DddTactics 领域事件拓扑");

        var grouped = Handlers
            .GroupBy(h => h.EventType, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        if (grouped.Count == 0)
        {
            sb.AppendLine("  （无处理器）");
        }

        foreach (var group in grouped)
        {
            sb.AppendLine($"  {ShortName(group.Key)} (IDomainEvent)");
            foreach (var handler in group.OrderBy(h => h.DispatchMode, StringComparer.Ordinal))
            {
                sb.AppendLine($"    └─ [{handler.DispatchMode}] {ShortName(handler.HandlerType)}");
            }
        }

        if (Orphans.Count > 0)
        {
            sb.AppendLine("  孤儿事件告警：");
            foreach (var orphan in Orphans.OrderBy(o => o.EventType, StringComparer.Ordinal))
            {
                sb.AppendLine($"    ⚠ {orphan.Message}");
            }
        }

        return sb.ToString();
    }

    private static string Sanitize(string fullName)
    {
        var sb = new StringBuilder(fullName.Length);
        foreach (var c in fullName)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return sb.ToString();
    }

    private static string ShortName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }
}
