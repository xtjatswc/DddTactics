using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace DddTactics.AppServices.Wolverine;

/// <summary>
/// Wolverine 验证中间件：在消息处理器执行前，对消息执行注册的 <see cref="IAppServiceValidator{TRequest}"/>。
/// Wolverine 通过消息处理管线发现并应用此开放泛型中间件（按消息类型 <c>Execute&lt;T&gt;</c>）。
/// <para>延续委托采用 <see cref="Func{TResult}"/>（<c>Task</c>）；<see cref="IServiceProvider"/> 由 Wolverine 注入为方法参数。</para>
/// </summary>
public static class ValidationMiddleware
{
    public static async Task Execute<T>(IMessageContext context, Func<Task> next, T command, IServiceProvider services)
        where T : class
    {
        var validator = services.GetService(typeof(IAppServiceValidator<T>)) as IAppServiceValidator<T>;
        if (validator is not null)
        {
            var result = await validator.ValidateAsync(command);
            if (!result.IsValid)
            {
                throw new AppServiceValidationException(result.Errors);
            }
        }

        await next();
    }
}
