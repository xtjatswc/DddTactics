namespace DddTactics.AppServices;

/// <summary>
/// 将 FluentValidation 的 <c>IValidator&lt;TRequest&gt;</c> 适配为 <see cref="IAppServiceValidator{TRequest}"/>。
/// </summary>
public sealed class FluentValidationAdapter<TRequest> : IAppServiceValidator<TRequest>
    where TRequest : class
{
    private readonly FluentValidation.IValidator<TRequest> _validator;

    public FluentValidationAdapter(FluentValidation.IValidator<TRequest> validator) => _validator = validator;

    public async Task<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
        {
            return ValidationResult.Ok();
        }

        var errors = result.Errors
            .Select(f => new ValidationError(f.PropertyName, f.ErrorMessage))
            .ToArray();

        return ValidationResult.Fail(errors);
    }
}
