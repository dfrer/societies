using Godot;
using Societies.Simulation;
using Societies.UI;
using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeHudTextBuilderTests
    {
        [Fact]
        public void BuildHelpText_IncludesSnapshotControls()
        {
            string helpText = PrototypeHudTextBuilder.BuildHelpText();

            Assert.Contains("F6 save snapshot", helpText);
            Assert.Contains("F7 reset run", helpText);
            Assert.Contains("F8 observer", helpText);
            Assert.Contains("F9 load snapshot", helpText);
            Assert.Contains("F10 overlays", helpText);
            Assert.Contains("F11 next build", helpText);
            Assert.Contains("F12 pause build", helpText);
            Assert.Contains("2 Food & Fuel", helpText);
            Assert.Contains("3 Shelter", helpText);
        }

        [Fact]
        public void BuildDebugText_IncludesWorldCameraAndOverlay()
        {
            string debugText = PrototypeHudTextBuilder.BuildDebugText(
                60,
                12,
                "08:30",
                "Clear",
                "Local",
                45,
                "balanced_basin",
                777,
                CameraMode.Observer,
                TerrainOverlayMode.Buildability);

            Assert.Contains("Mode: Local", debugText);
            Assert.Contains("Scenario: balanced_basin", debugText);
            Assert.Contains("World Seed: 777", debugText);
            Assert.Contains("Camera: Observer", debugText);
            Assert.Contains("Overlay: Buildability", debugText);
            Assert.Contains("Tick: 45", debugText);
        }

        [Fact]
        public void BuildWorldText_IncludesTerrainAndBiomeSummary()
        {
            PrototypeWorldSummary summary = new()
            {
                TerrainMode = "heightfield_v1",
                BuildableCellRatio = 0.58f,
                BiomeCellCounts = new Dictionary<string, int>
                {
                    ["Forest"] = 12,
                    ["Meadow"] = 18
                }
            };

            string worldText = PrototypeHudTextBuilder.BuildWorldText(
                "balanced_basin",
                777,
                CameraMode.Player,
                TerrainOverlayMode.None,
                summary,
                24.5f,
                0.12f);

            Assert.Contains("World", worldText);
            Assert.Contains("Terrain: heightfield_v1", worldText);
            Assert.Contains("Buildable: 58 %", worldText);
            Assert.Contains("Avg Route: 24.5 m", worldText);
            Assert.Contains("Path Cover: 12%", worldText);
            Assert.Contains("Forest 12", worldText);
            Assert.Contains("Meadow 18", worldText);
        }

        [Fact]
        public void BuildSettlementText_IncludesEconomyAndCitizenStates()
        {
            string settlementText = PrototypeHudTextBuilder.BuildSettlementText(
                new Dictionary<string, int>
                {
                    ["logs"] = 3,
                    ["firewood"] = 2,
                    ["meals"] = 1
                },
                new[]
                {
                    new PrototypeWorkerState
                    {
                        DisplayName = "Citizen 1",
                        Role = PrototypeCitizenRole.Hauler,
                        Phase = PrototypeWorkerPhase.Harvesting,
                        ActivityText = "Hauling to depot",
                        TargetLabel = "Tree",
                        PhaseDurationTicks = 10,
                        TicksRemaining = 4,
                        CarryItemId = "logs",
                        CarryAmount = 1,
                        Position = new Vector3(1.0f, 0.0f, 1.0f),
                        Needs = new PrototypeNeedState
                        {
                            Nutrition = 78.0f,
                            Fatigue = 24.0f
                        }
                    }
                },
                PrototypeSettlementClassification.Stable,
                "Build Queue Focus: Hut (active)",
                50,
                25,
                3,
                new[]
                {
                    new PrototypeStructureState
                    {
                        StructureKindId = "hut",
                        IsBuilt = true
                    }
                },
                1.10f,
                new Dictionary<string, int> { ["haultodepot"] = 4 },
                PrototypeSettlementDirective.Shelter);

            Assert.Contains("Settlement", settlementText);
            Assert.Contains("State: Stable", settlementText);
            Assert.Contains("Directive: Shelter", settlementText);
            Assert.Contains("Build Queue Focus: Hut (active)", settlementText);
            Assert.Contains("Travel/Work: 1.10", settlementText);
            Assert.Contains("meals x1", settlementText);
            Assert.Contains("hut 1/1", settlementText);
            Assert.Contains("Citizen 1 [Hauler", settlementText);
            Assert.Contains("-> Tree", settlementText);
            Assert.Contains("[logs x1]", settlementText);
        }

        [Fact]
        public void BuildCrisisText_ShowsContractProgressContributionsAndTerminalCauseDeterministically()
        {
            PrototypeCrisisDefinition definition = new()
            {
                Id = "hud_test",
                DisplayName = "HUD Test",
                TicksPerSecond = 20,
                DeadlineTicks = 20,
                RequiredCapableCitizens = 2,
                RequiredMeals = 3,
                RequiredHearthFuel = 4,
                RequiredBedCoveragePercent = 50,
                StableHoldTicks = 2,
                CollapseIncapacitatedCitizens = 9,
                CollapseHoldTicks = 3,
                CitizenNeedRateMultiplier = 1.0f
            };
            PrototypeCrisisState crisis = new(definition);
            crisis.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            string active = PrototypeHudTextBuilder.BuildCrisisText(
                crisis,
                PrototypeSettlementDirective.FoodAndFuel,
                new Dictionary<string, long> { ["logs"] = 2, ["berries"] = 3 });
            crisis.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            string terminal = PrototypeHudTextBuilder.BuildCrisisText(
                crisis,
                PrototypeSettlementDirective.FoodAndFuel,
                new Dictionary<string, long> { ["berries"] = 3, ["logs"] = 2 });

            Assert.Contains("Crisis: HUD Test", active);
            Assert.Contains("Time: 19/20 ticks remaining", active);
            Assert.Contains("Directive: Food & Fuel", active);
            Assert.Contains("Contributed: 5 (berries x3, logs x2)", active);
            Assert.Contains("capable 2/2 ok", active);
            Assert.Contains("meals 3/3 ok", active);
            Assert.Contains("fuel 4/4 ok", active);
            Assert.Contains("beds 50/50% ok", active);
            Assert.Contains("Hold: stable 1/2", active);
            Assert.Contains("Outcome: Stable: all conditions held 2/2 ticks", terminal);
            Assert.Equal("Crisis: none", PrototypeHudTextBuilder.BuildCrisisText(null, PrototypeSettlementDirective.Neutral, null));
        }

        [Fact]
        public void BuildInspectorText_IncludesCitizenAndStructureDetails()
        {
            string inspectorText = PrototypeHudTextBuilder.BuildInspectorText(
                new PrototypeWorkerState
                {
                    DisplayName = "Citizen 2",
                    Role = PrototypeCitizenRole.Builder,
                    CurrentOrderKind = PrototypeWorkOrderKind.Build,
                    CurrentOrderReason = "construction ready | Why: Shelter — hut construction",
                    CarryItemId = "timber",
                    CarryAmount = 2,
                    Needs = new PrototypeNeedState
                    {
                        Nutrition = 64.0f,
                        Fatigue = 40.0f
                    },
                    Navigation = new PrototypeCitizenNavigationState
                    {
                        CurrentRouteLengthMeters = 12.5f,
                        CurrentRouteTravelTicks = 16
                    },
                    TravelTicksAccumulated = 10,
                    WorkTicksAccumulated = 8
                },
                new PrototypeStructureState
                {
                    DisplayName = "Hut",
                    IsBuilt = false,
                    InputStore = new PrototypeResourceStoreState(),
                    OutputStore = new PrototypeResourceStoreState()
                });

            Assert.Contains("Inspector", inspectorText);
            Assert.Contains("Citizen: Citizen 2 [Builder]", inspectorText);
            Assert.Contains("Structure: Hut", inspectorText);
            Assert.Contains("Carry: timber x2", inspectorText);
            Assert.Contains("Route: 12.5 m", inspectorText);
            Assert.Contains("Why: Shelter — hut construction", inspectorText);
        }

        [Fact]
        public void CompactBuilders_PreserveRequiredStateAndAggregateCitizenRows()
        {
            List<PrototypeWorkerState> workers = new();
            for (int index = 0; index < 12; index++)
            {
                workers.Add(new PrototypeWorkerState
                {
                    DisplayName = $"Citizen {index + 1}",
                    Phase = index == 0 ? PrototypeWorkerPhase.Harvesting : PrototypeWorkerPhase.Idle,
                    TargetLabel = index == 0 ? "Tree" : string.Empty
                });
            }

            string settlement = PrototypeHudTextBuilder.BuildCompactSettlementText(
                new Dictionary<string, int> { ["berries"] = 3, ["logs"] = 2 },
                workers,
                PrototypeSettlementClassification.Strained,
                "Hut active",
                25,
                50,
                1,
                new[]
                {
                    new PrototypeStructureState { IsBuilt = true },
                    new PrototypeStructureState { IsBlocked = true }
                },
                PrototypeSettlementDirective.FoodAndFuel);
            string world = PrototypeHudTextBuilder.BuildCompactWorldText(
                "empty_stores", 1701, CameraMode.Player, TerrainOverlayMode.None);
            string inspector = PrototypeHudTextBuilder.BuildCompactInspectorText(
                new PrototypeWorkerState
                {
                    DisplayName = "Citizen 1",
                    Role = PrototypeCitizenRole.Hauler,
                    Needs = new PrototypeNeedState { Nutrition = 78.0f, Fatigue = 24.0f },
                    Navigation = new PrototypeCitizenNavigationState
                    {
                        CurrentRouteLengthMeters = 12.5f,
                        CurrentRouteTravelTicks = 16
                    }
                },
                null);

            Assert.Contains("Citizens: 1/12 active (details: F3)", settlement);
            Assert.Contains("Target: Citizen 1 -> Tree", settlement);
            Assert.DoesNotContain("Citizen 1 [", settlement);
            Assert.Contains("Food & Fuel", settlement);
            Assert.Contains("Directive: Food & Fuel", settlement);
            Assert.Contains("Needs: meals 25% | beds 50% | fuel 1", settlement);
            Assert.Contains("Stockpile: berries x3, logs x2", settlement);
            Assert.Contains("Structures: 1/2 built | 1 blocked", settlement);
            Assert.DoesNotContain("?", settlement);
            Assert.True(
                PrototypeHudLayout.Calculate(1280.0f, 720.0f)
                    .GetTextBudget(PrototypeHudLayout.Settlement, settlement, 15)
                    .Fits,
                "Compact settlement text must fit the 1280x720 card after citizen aggregation.");
            Assert.Equal(2, world.Split('\n').Length);
            Assert.Equal("World: empty_stores | seed 1701\nPlayer | None", world);
            Assert.DoesNotContain("?", world);
            Assert.Contains("Needs: nutrition 78 | fatigue 24", inspector);
            Assert.Contains("Route: 12.5 m | 16 ticks", inspector);
            Assert.DoesNotContain("?", inspector);
            Assert.Equal(2, PrototypeHudTextBuilder.BuildCompactHelpText().Split('\n').Length);
            Assert.Contains("F11 next build", PrototypeHudTextBuilder.BuildCompactHelpText());
            Assert.Contains("F12 pause build", PrototypeHudTextBuilder.BuildCompactHelpText());
        }
    }
}
