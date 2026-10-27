namespace DddTactics.Core;

/// <summary>
/// 强类型 ID 范式。使用静态抽象接口方法，使具体实现可提供 <c>New()</c> / <c>From()</c> 工厂。
/// <typeparamref name="TSelf"/> 必须是 <see langword="struct"/>（推荐 <c>readonly record struct</c>）。
/// </summary>
public interface IStrongId<TSelf, TValue>
    where TSelf : struct, IStrongId<TSelf, TValue>
    where TValue : struct
{
    TValue Value { get; }

    static abstract TSelf New();

    static abstract TSelf From(TValue value);
}
