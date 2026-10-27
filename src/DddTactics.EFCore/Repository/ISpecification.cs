namespace DddTactics.EFCore;

/// <summary>
/// 最小规格模式：将业务规则封装为可组合的谓词。
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>> ToExpression();

    bool IsSatisfiedBy(T entity);
}
