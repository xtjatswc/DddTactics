namespace DddTactics.AppServices;

/// <summary>
/// 单条字段级验证错误。
/// </summary>
public readonly record struct ValidationError(string PropertyName, string Message);

/// <summary>
/// 验证结果。无错误即视为通过。
/// </summary>
public sealed class ValidationResult
{
    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Ok() => new();

    public static ValidationResult Fail(IEnumerable<ValidationError> errors) => new() { Errors = errors.ToArray() };
}

/// <summary>
/// 验证失败异常，由 <see cref="AppService"/> 与验证管道在结果不合法时抛出。
/// </summary>
public sealed class AppServiceValidationException : Exception
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public AppServiceValidationException(IEnumerable<ValidationError> errors)
        : base("应用层验证失败。")
        => Errors = errors.ToArray();
}

/// <summary>
/// 原生验证器契约。可替换为 FluentValidation 适配或任意实现（<see cref="FluentValidationAdapter{TRequest}"/>）。
/// </summary>
public interface IAppServiceValidator<TRequest> where TRequest : class
{
    Task<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 应用服务基类。仅提供验证管道与执行模板方法；不绑定任何框架。
/// <code>
/// public class PlaceOrderAppService : AppService { ... }
/// await appService.ExecuteAsync(request, validator, async (r, ct) => { ... });
/// </code>
/// </summary>
public abstract class AppService
{
    protected async Task<TResult> ExecuteAsync<TRequest, TResult>(
        TRequest request,
        IAppServiceValidator<TRequest>? validator,
        Func<TRequest, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
        where TRequest : class
    {
        if (validator is not null)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            if (!result.IsValid)
            {
                throw new AppServiceValidationException(result.Errors);
            }
        }

        return await action(request, cancellationToken);
    }
}
