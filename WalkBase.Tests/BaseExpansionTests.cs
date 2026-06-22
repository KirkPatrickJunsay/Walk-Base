using WalkBase.Services;

namespace WalkBase.Tests;

public class BaseExpansionTests
{
    [Fact]
    public void CanExpand_UntilMaxSize()
    {
        Assert.True(BaseExpansion.CanExpand(BaseExpansion.InitialSize));
        Assert.True(BaseExpansion.CanExpand(BaseExpansion.MaxSize - 1));
        Assert.False(BaseExpansion.CanExpand(BaseExpansion.MaxSize));
    }

    [Theory]
    [InlineData(6, 100)]
    [InlineData(7, 200)]
    [InlineData(8, 300)]
    [InlineData(9, 400)]
    public void CostFor_RisesWithSize(int size, long expected) =>
        Assert.Equal(expected, BaseExpansion.CostFor(size));
}
