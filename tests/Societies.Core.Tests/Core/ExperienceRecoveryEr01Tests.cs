using Godot;
using Societies.Simulation;
using Societies.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    /// <summary>
    /// ER-01 proves that the player-facing starts remain catalog/seed driven and that the
    /// visible loop continues to use the existing runtime commands rather than UI state.
    /// </summary>
    public sealed class ExperienceRecoveryEr01Tests
    {
        [Fact]
        public void CuratedProfiles_AreExactlyTwoAndExposeThreeMeaningfulContrasts()
        {
            PrototypeScenarioCatalog catalog = LoadCatalogs().Scenarios;
            PrototypeScenarioDefinition[] profiles = catalog.GetExperienceProfiles().ToArray();
            IReadOnlyList<PrototypeExperienceProfileOption> options = catalog.GetExperienceProfileOptions();

            Assert.Equal(2, profiles.Length);
            Assert.Equal(new[] { "wetland_builder", "empty_stores" }, options.Select(option => option.ScenarioId));
            IList<PrototypeExperienceProfileOption> optionList = Assert.IsAssignableFrom<IList<PrototypeExperienceProfileOption>>(options);
            Assert.True(optionList.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => optionList[0] = optionList[1]);
            Assert.All(typeof(PrototypeExperienceProfileOption).GetProperties(), property =>
                Assert.True(property.PropertyType == typeof(string) || property.PropertyType == typeof(int)));
            Assert.Equal(new[] { "wetland_builder", "empty_stores" }, profiles.Select(profile => profile.Id));
            Assert.All(profiles, profile => Assert.NotNull(profile.ExperienceProfile));
            Assert.NotEqual(profiles[0].SimulationSeed, profiles[1].SimulationSeed);
            Assert.NotEqual(profiles[0].ExperienceProfile!.ResourceApproach, profiles[1].ExperienceProfile!.ResourceApproach);
            Assert.NotEqual(profiles[0].ExperienceProfile!.ImmediatePressure, profiles[1].ExperienceProfile!.ImmediatePressure);
            Assert.NotEqual(profiles[0].ExperienceProfile!.WorldCue, profiles[1].ExperienceProfile!.WorldCue);

            PrototypeScenarioDefinition marsh = profiles[0];
            PrototypeScenarioDefinition lean = profiles[1];
            PrototypeExperienceProfileDefinition marshProfile = Assert.IsType<PrototypeExperienceProfileDefinition>(marsh.ExperienceProfile);
            PrototypeExperienceProfileDefinition leanProfile = Assert.IsType<PrototypeExperienceProfileDefinition>(lean.ExperienceProfile);
            WorldGenerationResult marshWorld = PrototypeWorldGenerator.Generate(marsh);
            WorldGenerationResult leanWorld = PrototypeWorldGenerator.Generate(lean);
            Assert.True(marsh.WorldGen.WetnessBias > lean.WorldGen.WetnessBias);
            Assert.True(marshWorld.WorldMap.Cells.Average(cell => cell.Wetness) >
                leanWorld.WorldMap.Cells.Average(cell => cell.Wetness));
            Assert.True(marshWorld.WorldMap.Cells.Count(cell => cell.Biome == BiomeType.Wetland) >
                leanWorld.WorldMap.Cells.Count(cell => cell.Biome == BiomeType.Wetland));
            Assert.True(marshWorld.ResourceSpawns.Count(spawn => spawn.ResourceId == "reeds") >
                leanWorld.ResourceSpawns.Count(spawn => spawn.ResourceId == "reeds"));
            Assert.True(marsh.StartingStock["firewood"] > lean.StartingStock["firewood"]);
            Assert.True(marsh.StartingStock["meals"] > lean.StartingStock["meals"]);
            Assert.True(marsh.InitialHearthFuel > lean.InitialHearthFuel);
            Assert.Contains("wetter", marshProfile.WorldCue, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dense reed", marshProfile.WorldCue, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("drier", leanProfile.WorldCue, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sparse reed", leanProfile.WorldCue, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("wetland_builder", "reeds", PrototypeCivicPolicy.ProtectWetland)]
        [InlineData("empty_stores", "berries", PrototypeCivicPolicy.DrawDownWetland)]
        public void ProfileCommandTrace_ReplaysThroughExistingHarvestContributionAndCivicCommands(
            string scenarioId,
            string harvestResourceId,
            PrototypeCivicPolicy policy)
        {
            (string snapshot, string events, int contributed) first = RunLoopTrace(scenarioId, harvestResourceId, policy);
            (string snapshot, string events, int contributed) second = RunLoopTrace(scenarioId, harvestResourceId, policy);

            Assert.Equal(first.snapshot, second.snapshot);
            Assert.Equal(first.events, second.events);
            Assert.True(first.contributed > 0);
        }

        [Fact]
        public void GoalAndFeedbackHierarchy_StaysActionableAndDistinguishesSuccessRejectionAndDepletion()
        {
            PrototypeScenarioDefinition profile = LoadCatalogs().Scenarios.Resolve("wetland_builder");
            PrototypeExperienceProfileDefinition experienceProfile = Assert.IsType<PrototypeExperienceProfileDefinition>(profile.ExperienceProfile);
            PrototypeScenarioDefinition leanDefinition = LoadCatalogs().Scenarios.Resolve("empty_stores");
            PrototypeExperienceProfileDefinition leanExperienceProfile = Assert.IsType<PrototypeExperienceProfileDefinition>(leanDefinition.ExperienceProfile);
            PrototypeRuntimeSession session = new(
                profile,
                LoadCatalogs().RoleQuotas.Roles,
                resourceDefinitions: LoadCatalogs().Resources.Resources);
            session.Initialize(8.0f);
            PrototypeWorkerState citizen = session.Workers[0];
            PrototypeCitizenInterest interest = PrototypeCitizenInterestEvaluator.Evaluate(
                citizen,
                PrototypeCivicPolicy.Neutral);
            string goal = PrototypeHudTextBuilder.BuildExperienceGoalText(
                experienceProfile,
                PrototypeCivicPolicy.Neutral,
                new PrototypeWetlandSnapshot
                {
                    WetlandHealth = 60,
                    WetlandHealthBand = "strained"
                },
                citizen,
                interest);

            Assert.Contains("Marsh Recovery", goal);
            Assert.Contains("Next:", goal);
            Assert.Contains("World: wetter ground; dense reeds", goal, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(citizen.DisplayName, goal);
            Assert.Contains("Citizen 1:", goal);
            Assert.DoesNotContain("Choice:", goal, StringComparison.OrdinalIgnoreCase);

            PrototypeCitizenInterest protectResponse = PrototypeCitizenInterestEvaluator.Evaluate(
                citizen,
                PrototypeCivicPolicy.ProtectWetland);
            string selectedGoal = PrototypeHudTextBuilder.BuildExperienceGoalText(
                experienceProfile,
                PrototypeCivicPolicy.ProtectWetland,
                new PrototypeWetlandSnapshot
                {
                    PolicyId = "protect_wetland",
                    WetlandHealth = 75,
                    WetlandHealthBand = "healthy"
                },
                citizen,
                protectResponse);
            Assert.Contains(
                protectResponse.Position == PrototypeCitizenInterestPosition.Supports ? "supports Protect" : "opposes Protect",
                selectedGoal);
            Assert.Contains("Wetland: healthy 75/100", selectedGoal);

            PrototypeHudLayout layout = PrototypeHudLayout.Calculate(1280.0f, 720.0f);
            Assert.False(layout.HasOverlaps());
            Assert.False(layout.HasNormalControlOverlaps());
            Assert.All(layout.NormalControlBounds.Values, bounds => Assert.True(bounds.FitsWithin(1280.0f, 720.0f)));
            PrototypeHudTextBudget neutralBudget = layout.GetNormalControlTextBudget(PrototypeHudLayout.Goal, goal, 17);
            PrototypeHudTextBudget selectedBudget = layout.GetNormalControlTextBudget(PrototypeHudLayout.Goal, selectedGoal, 17);
            Assert.True(neutralBudget.Fits, $"Neutral goal needs {neutralBudget.EstimatedRenderedLines}/{neutralBudget.AvailableLines} lines.");
            Assert.True(selectedBudget.Fits, $"Selected goal needs {selectedBudget.EstimatedRenderedLines}/{selectedBudget.AvailableLines} lines.");
            Assert.True(layout.GetNormalControlTextBudget(PrototypeHudLayout.ProtectChoice, "[4] Protect wetland", 16).Fits);
            Assert.True(layout.GetNormalControlTextBudget(PrototypeHudLayout.DrawDownChoice, "[5] Draw down wetland", 16).Fits);
            Assert.True(layout.GetNormalControlTextBudget(
                PrototypeHudLayout.DecisionRail,
                "GATHER ✓ / DEPOT ✓ / DECIDE — click or [4]/[5]",
                13).Fits);
            Assert.True(layout.GetNormalControlTextBudget(
                PrototypeHudLayout.MarshProfileChoice,
                $"{experienceProfile.Title}: {experienceProfile.ResourceApproach}",
                16).Fits);
            Assert.True(layout.GetNormalControlTextBudget(
                PrototypeHudLayout.LeanProfileChoice,
                $"{leanExperienceProfile.Title}: {leanExperienceProfile.ResourceApproach}",
                16).Fits);
            Assert.Equal(
                PrototypeHudCue.ContributionSuccess,
                PrototypeHudPresentationState.Create(
                    PrototypeSettlementDirective.Neutral,
                    PrototypeSettlementClassification.Strained,
                    null,
                    "Contributed reeds x1",
                    string.Empty).StatusCue);
            Assert.Equal(
                PrototypeHudCue.BlockedInteraction,
                PrototypeHudPresentationState.Create(
                    PrototypeSettlementDirective.Neutral,
                    PrototypeSettlementClassification.Strained,
                    null,
                    "Harvest rejected: resource unavailable",
                    string.Empty).StatusCue);
            Assert.Equal(
                PrototypeHudCue.DepletedInteraction,
                PrototypeHudPresentationState.Create(
                    PrototypeSettlementDirective.Neutral,
                    PrototypeSettlementClassification.Strained,
                    null,
                    "Harvested reeds x1; site depleted",
                    string.Empty).StatusCue);
        }

        private static (string snapshot, string events, int contributed) RunLoopTrace(
            string scenarioId,
            string harvestResourceId,
            PrototypeCivicPolicy policy)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(
                bundle.Scenarios.Resolve(scenarioId),
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);

            PrototypeResourceSnapshot resource = session.ResourceSnapshots.First(snapshot =>
                snapshot.ResourceId == harvestResourceId && snapshot.UnitsRemaining > 0);
            Assert.True(session.HarvestForPlayer(resource.SiteId, 1).Succeeded);

            PrototypeContributionBatchResult contribution = new PrototypeContributionInteraction().Execute(
                session,
                session.CentralDepotPosition,
                interactionRangeMeters: 4.5f,
                inputFrame: 41);
            Assert.True(contribution.Succeeded);
            Assert.True(session.SelectCivicPolicy(new(policy, ExpectedVersion: 0, IssuedTick: 0)).Succeeded);

            return (
                PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)),
                PrototypePersistenceService.SerializeEventLog(session.EventLog),
                contribution.Results.Sum(result => result.AppliedQuantity));
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return PrototypeCatalogLoader.LoadFromDirectory(candidate);
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }
    }
}
