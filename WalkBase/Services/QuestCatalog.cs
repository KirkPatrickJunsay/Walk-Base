using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>The code-defined set of milestone quests (ordered easy → hard).</summary>
public static class QuestCatalog
{
    public static readonly IReadOnlyList<Quest> All = new List<Quest>
    {
        // --- Getting started ---
        new() { Id = "first_steps", Title = "First Steps", Description = "Walk 5,000 steps in total",
                Metric = QuestMetric.LifetimeSteps, Target = 5_000, Reward = 200 },

        new() { Id = "founder", Title = "Founder", Description = "Place 3 buildings in your town",
                Metric = QuestMetric.BuildingCount, Target = 3, Reward = 300 },

        new() { Id = "consistent", Title = "Consistent", Description = "Reach a 3-day goal streak",
                Metric = QuestMetric.BestStreak, Target = 3, Reward = 500 },

        new() { Id = "stroller", Title = "Stroller", Description = "Walk 25,000 steps in total",
                Metric = QuestMetric.LifetimeSteps, Target = 25_000, Reward = 600 },

        new() { Id = "regular_walker", Title = "Regular Walker", Description = "Walk on 7 different days",
                Metric = QuestMetric.DaysWalked, Target = 7, Reward = 700 },

        // --- Finding your stride ---
        new() { Id = "town_planner", Title = "Town Planner", Description = "Build 5 different kinds of building",
                Metric = QuestMetric.DistinctBuildingTypes, Target = 5, Reward = 1_000 },

        new() { Id = "land_baron", Title = "Land Baron", Description = "Expand your town to 8 × 8",
                Metric = QuestMetric.BaseSize, Target = 8, Reward = 800 },

        new() { Id = "marathoner", Title = "Marathoner", Description = "Walk 50,000 steps in total",
                Metric = QuestMetric.LifetimeSteps, Target = 50_000, Reward = 1_000 },

        new() { Id = "architect", Title = "Architect", Description = "Place 8 buildings in your town",
                Metric = QuestMetric.BuildingCount, Target = 8, Reward = 1_500 },

        new() { Id = "dedicated", Title = "Dedicated", Description = "Reach a 7-day goal streak",
                Metric = QuestMetric.BestStreak, Target = 7, Reward = 1_500 },

        // --- Hitting your stride ---
        new() { Id = "explorer", Title = "Explorer", Description = "Walk 100,000 steps in total",
                Metric = QuestMetric.LifetimeSteps, Target = 100_000, Reward = 2_500 },

        new() { Id = "master_builder", Title = "Master Builder", Description = "Build 8 different kinds of building",
                Metric = QuestMetric.DistinctBuildingTypes, Target = 8, Reward = 2_500 },

        new() { Id = "fortnight", Title = "Fortnight", Description = "Reach a 14-day goal streak",
                Metric = QuestMetric.BestStreak, Target = 14, Reward = 3_000 },

        new() { Id = "developer", Title = "Developer", Description = "Place 15 buildings in your town",
                Metric = QuestMetric.BuildingCount, Target = 15, Reward = 3_000 },

        new() { Id = "monthly_mover", Title = "Monthly Mover", Description = "Walk on 30 different days",
                Metric = QuestMetric.DaysWalked, Target = 30, Reward = 3_000 },

        new() { Id = "empire_builder", Title = "Empire Builder", Description = "Expand your town to 10 × 10",
                Metric = QuestMetric.BaseSize, Target = 10, Reward = 4_000 },

        // --- The long road ---
        new() { Id = "trailblazer", Title = "Trailblazer", Description = "Walk 250,000 steps in total",
                Metric = QuestMetric.LifetimeSteps, Target = 250_000, Reward = 5_000 },

        new() { Id = "collector", Title = "Collector", Description = "Build 12 different kinds of building",
                Metric = QuestMetric.DistinctBuildingTypes, Target = 12, Reward = 5_000 },

        new() { Id = "metropolis", Title = "Metropolis", Description = "Place 25 buildings in your town",
                Metric = QuestMetric.BuildingCount, Target = 25, Reward = 6_000 },

        new() { Id = "unstoppable", Title = "Unstoppable", Description = "Reach a 30-day goal streak",
                Metric = QuestMetric.BestStreak, Target = 30, Reward = 7_000 },

        new() { Id = "long_hauler", Title = "Long Hauler", Description = "Walk 500,000 steps in total",
                Metric = QuestMetric.LifetimeSteps, Target = 500_000, Reward = 10_000 },

        new() { Id = "centennial", Title = "Centennial", Description = "Walk on 100 different days",
                Metric = QuestMetric.DaysWalked, Target = 100, Reward = 10_000 },

        new() { Id = "millionaire", Title = "Million Steps", Description = "Walk 1,000,000 steps in total",
                Metric = QuestMetric.LifetimeSteps, Target = 1_000_000, Reward = 20_000 },
    };
}
