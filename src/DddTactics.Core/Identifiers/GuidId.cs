namespace DddTactics.Core;

/// <summary>
/// 基于 <see cref="Guid"/> 的强类型 ID 范式实现。
/// </summary>
public readonly record struct GuidId(Guid Value) : IStrongId<GuidId, Guid>
{
    public static GuidId New() => new(Guid.NewGuid());

    public static GuidId From(Guid value) => new(value);

    public static GuidId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString();
}
