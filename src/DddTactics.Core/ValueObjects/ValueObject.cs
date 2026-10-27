namespace DddTactics.Core;

/// <summary>
/// 值对象基类。相等性基于 <see cref="GetEqualityComponents"/> 返回的各分量序列相等。
/// 子类应为不可变；TACT003 会在发现可写 <c>set</c> 时告警。
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? a, ValueObject? b) => Equals(a, b);

    public static bool operator !=(ValueObject? a, ValueObject? b) => !Equals(a, b);
}
