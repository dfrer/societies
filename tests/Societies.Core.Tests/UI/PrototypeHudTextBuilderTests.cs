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
    }
}
