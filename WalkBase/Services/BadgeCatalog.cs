using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>The code-defined achievement badges, grouped by metric and ordered by threshold.</summary>
public static class BadgeCatalog
{
    public static readonly IReadOnlyList<Badge> All = new List<Badge>
    {
        // Lifetime steps
        new() { Id = "steps_10k",  Title = "Wanderer",   Description = "Walk 10,000 steps",     Emoji = "🥾", Metric = QuestMetric.LifetimeSteps, Threshold = 10_000 },
        new() { Id = "steps_100k", Title = "Pacer",      Description = "Walk 100,000 steps",    Emoji = "🏃", Metric = QuestMetric.LifetimeSteps, Threshold = 100_000 },
        new() { Id = "steps_500k", Title = "Trailblazer",Description = "Walk 500,000 steps",    Emoji = "🏅", Metric = QuestMetric.LifetimeSteps, Threshold = 500_000 },
        new() { Id = "steps_1m",   Title = "Legend",     Description = "Walk 1,000,000 steps",  Emoji = "🌟", Metric = QuestMetric.LifetimeSteps, Threshold = 1_000_000 },

        // Buildings placed
        new() { Id = "build_1",  Title = "Settler",   Description = "Place your first building", Emoji = "🏠", Metric = QuestMetric.BuildingCount, Threshold = 1 },
        new() { Id = "build_5",  Title = "Builder",   Description = "Place 5 buildings",         Emoji = "🏘️", Metric = QuestMetric.BuildingCount, Threshold = 5 },
        new() { Id = "build_12", Title = "Developer", Description = "Place 12 buildings",        Emoji = "🏗️", Metric = QuestMetric.BuildingCount, Threshold = 12 },
        new() { Id = "build_20", Title = "Tycoon",    Description = "Place 20 buildings",        Emoji = "🏙️", Metric = QuestMetric.BuildingCount, Threshold = 20 },

        // Variety
        new() { Id = "var_4", Title = "Curator",          Description = "Build 4 kinds of building", Emoji = "🎨", Metric = QuestMetric.DistinctBuildingTypes, Threshold = 4 },
        new() { Id = "var_8", Title = "Master Architect", Description = "Build 8 kinds of building", Emoji = "🏛️", Metric = QuestMetric.DistinctBuildingTypes, Threshold = 8 },

        // Streaks
        new() { Id = "streak_3",  Title = "Spark",       Description = "Reach a 3-day streak",  Emoji = "✨", Metric = QuestMetric.BestStreak, Threshold = 3 },
        new() { Id = "streak_7",  Title = "Committed",   Description = "Reach a 7-day streak",  Emoji = "🔥", Metric = QuestMetric.BestStreak, Threshold = 7 },
        new() { Id = "streak_30", Title = "Iron Will",   Description = "Reach a 30-day streak", Emoji = "💎", Metric = QuestMetric.BestStreak, Threshold = 30 },

        // Days walked
        new() { Id = "days_7",   Title = "Regular",   Description = "Walk on 7 days",   Emoji = "📅", Metric = QuestMetric.DaysWalked, Threshold = 7 },
        new() { Id = "days_30",  Title = "Devoted",   Description = "Walk on 30 days",  Emoji = "🗓️", Metric = QuestMetric.DaysWalked, Threshold = 30 },
        new() { Id = "days_100", Title = "Centurion", Description = "Walk on 100 days", Emoji = "💯", Metric = QuestMetric.DaysWalked, Threshold = 100 },

        // Town size
        new() { Id = "land_8",  Title = "Landowner", Description = "Grow your town to 8×8",  Emoji = "🗺️", Metric = QuestMetric.BaseSize, Threshold = 8 },
        new() { Id = "land_10", Title = "Empire",    Description = "Grow your town to 10×10", Emoji = "🏰", Metric = QuestMetric.BaseSize, Threshold = 10 },
    };
}
