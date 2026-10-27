namespace DddTactics.EFCore;

/// <summary>
/// DI 注册扩展。注册分发器、默认时机解析器与拦截器单例。
/// 调用方仍需在 <c>DbContext</c> 的 <c>options.AddInterceptors(sp.GetRequiredService&lt;DomainEventDispatchInterceptor&gt;())</c> 中接入拦截器。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDddTacticsEfCore(
        this IServiceCollection services,
        ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
    {
        if (!services.Any(d => d.ServiceType == typeof(IDomainEventDispatcher)))
        {
            services.Add(new ServiceDescriptor(typeof(IDomainEventDispatcher), typeof(DomainEventDispatcher), dispatcherLifetime));
        }

        if (!services.Any(d => d.ServiceType == typeof(IDispatchModeResolver)))
        {
            services.Add(new ServiceDescriptor(typeof(IDispatchModeResolver), typeof(AttributeDispatchModeResolver), ServiceLifetime.Singleton));
        }

        if (!services.Any(d => d.ServiceType == typeof(DomainEventDispatchInterceptor)))
        {
            services.Add(new ServiceDescriptor(typeof(DomainEventDispatchInterceptor), typeof(DomainEventDispatchInterceptor), ServiceLifetime.Singleton));
        }

        return services;
    }
}
