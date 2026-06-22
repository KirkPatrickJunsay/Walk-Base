using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>Pure steps↔Bricks conversion and balance math (spec §8). No I/O.</summary>
public interface ICurrencyService
{
    /// <summary>Steps required to earn one Brick.</summary>
    int StepsPerBrick { get; }

    /// <summary>Total Bricks ever earned from the given lifetime step count.</summary>
    long TotalBricksEarned(long lifetimeSteps);

    /// <summary>Current spendable balance for a player state.</summary>
    long Balance(PlayerState state);
}
