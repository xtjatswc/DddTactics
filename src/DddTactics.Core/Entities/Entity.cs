namespace DddTactics.Core;

/// <summary>
/// 实体基类。标识基于 <typeparamref name="TId"/> 的值相等（同一类型且 Id 相等即视为同一实体）。
/// 允许裸 <see cref="Guid"/> / <see cref="int"/> 作为标识（无需强类型 ID）。
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct, IEquatable<TId>
{
    public TId Id { get; protected set; }

    protected Entity()
    {
    }

    protected Entity(TId id) => Id = id;

    public bool Equals(Entity<TId>? other) => other is not null && Id.Equals(other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity<TId>? a, Entity<TId>? b) => Equals(a, b);

    public static bool operator !=(Entity<TId>? a, Entity<TId>? b) => !Equals(a, b);
}
