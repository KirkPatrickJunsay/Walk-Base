using WalkBase.Models;

namespace WalkBase.Services;

public sealed class CurrencyService : ICurrencyService
{
    /// <summary>Tunable conversion rate (spec §8): 1 Brick per 10 steps.</summary>
    public const int STEPS_PER_BRICK = 10;

    public int StepsPerBrick => STEPS_PER_BRICK;

    public long TotalBricksEarned(long lifetimeSteps) =>
        lifetimeSteps < 0 ? 0 : lifetimeSteps / STEPS_PER_BRICK;

    public long Balance(PlayerState state)
    {
        var balance = TotalBricksEarned(state.LifetimeSteps) + state.BonusBricks - state.TotalBricksSpent;
        return balance < 0 ? 0 : balance;
    }
}
