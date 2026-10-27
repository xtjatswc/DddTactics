namespace DddTactics.AppServices;

/// <summary>
/// MediatR 验证管道行为：在进入处理器前对请求执行所有已注册的 FluentValidation <c>IValidator&lt;TRequest&gt;</c>。
/// 注册：<c>services.AddTransient(typeof(IPipelineBehavior&lt;,&gt;), typeof(MediatRValidationBehavior&lt;,&gt;))</c>。
/// </summary>
public sealed class MediatRValidationBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<FluentValidation.IValidator<TRequest>> _validators;

    public MediatRValidationBehavior(IEnumerable<FluentValidation.IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new FluentValidation.ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToArray();

            if (failures.Length > 0)
            {
                var errors = failures.Select(f => new ValidationError(f.PropertyName, f.ErrorMessage)).ToArray();
                throw new AppServiceValidationException(errors);
            }
        }

        return await next();
    }
}
