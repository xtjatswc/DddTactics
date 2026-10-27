using DddTactics.Core;
using Xunit;

namespace DddTactics.Core.Tests;

public readonly record struct LoyaltyProgramId(Guid Value) : IStrongId<LoyaltyProgramId, Guid>
{
    public static LoyaltyProgramId New() => new(Guid.NewGuid());

    public static LoyaltyProgramId From(Guid value) => new(value);
}

public sealed class LoyaltyProgram : AggregateRoot<LoyaltyProgramId>
{
    public bool IsPublished { get; private set; }

    public string Name { get; private set; } = string.Empty;

    private LoyaltyProgram()
    {
    }

    public LoyaltyProgram(LoyaltyProgramId id, string name) : base(id) => Name = name;

    public void Publish()
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("已发布，不可重复发布。");
        }

        IsPublished = true;
        AddDomainEvent(new LoyaltyProgramPublished(Id));
    }

    public void Rename(string name)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("发布后不可编辑。");
        }

        Name = name;
    }
}

public sealed record LoyaltyProgramPublished(LoyaltyProgramId Id) : IDomainEvent;

public class CoreTests
{
    [Fact]
    public void Published_loyalty_program_cannot_be_edited()
    {
        var program = new LoyaltyProgram(LoyaltyProgramId.New(), "基础权益");
        program.Publish();

        Assert.True(program.IsPublished);
        Assert.Throws<InvalidOperationException>(() => program.Rename("高级权益"));
    }

    [Fact]
    public void Publish_emits_domain_event()
    {
        var program = new LoyaltyProgram(LoyaltyProgramId.New(), "基础权益");
        program.Publish();

        Assert.Single(program.DomainEvents);
        Assert.IsType<LoyaltyProgramPublished>(program.DomainEvents[0]);
    }

    [Fact]
    public void Strong_typed_id_distinct_values()
    {
        var a = LoyaltyProgramId.New();
        var b = LoyaltyProgramId.New();
        Assert.NotEqual(a, b);
        Assert.Equal(a, LoyaltyProgramId.From(a.Value));
    }

    [Fact]
    public void Entities_with_same_id_are_equal()
    {
        var id = LoyaltyProgramId.New();
        var x = new LoyaltyProgram(id, "x");
        var y = new LoyaltyProgram(id, "y");
        Assert.Equal(x, y);
    }
}
