namespace DddTactics.EFCore;

/// <summary>
/// 仓储基类（最小实现）。不强制继承；退出机制允许直接操作 <see cref="DbContext"/>。
/// </summary>
public abstract class RepositoryBase<TAggregate, TId>
    where TAggregate : DddTactics.Core.AggregateRoot<TId>
    where TId : struct, IEquatable<TId>
{
    protected DbContext Context { get; }

    protected RepositoryBase(DbContext context) => Context = context;

    protected DbSet<TAggregate> Set => Context.Set<TAggregate>();

    public virtual void Add(TAggregate aggregate) => Set.Add(aggregate);

    public virtual void Remove(TAggregate aggregate) => Set.Remove(aggregate);

    public virtual Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => Set.FindAsync(new object[] { id }, cancellationToken).AsTask();

    public virtual Task<TAggregate?> SingleAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(specification.ToExpression(), cancellationToken);

    public virtual async Task<IReadOnlyList<TAggregate>> ListAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default)
        => await Set.Where(specification.ToExpression()).ToListAsync(cancellationToken);
}
