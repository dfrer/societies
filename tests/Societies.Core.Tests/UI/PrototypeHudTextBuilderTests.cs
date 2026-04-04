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
                summary);

            Assert.Contains("World", worldText);
            Assert.Contains("Terrain: heightfield_v1", worldText);
            Assert.Contains("Buildable: 58 %", worldText);
            Assert.Contains("Forest 12", worldText);
            Assert.Contains("Meadow 18", worldText);
        }

        [Fact]
        public void BuildSettlementText_IncludesStockpileAndWorkerStates()
        {
            string settlementText = PrototypeHudTextBuilder.BuildSettlementText(
                new Dictionary<string, int>
                {
                    ["wood"] = 3,
                    ["campfire"] = 1
                },
                new[]
                {
                    new PrototypeWorkerState
                    {
                        DisplayName = "Worker 1",
                        Phase = PrototypeWorkerPhase.Harvesting,
                        ActivityText = "Harvesting Tree",
                        TargetLabel = "Tree",
                        PhaseDurationTicks = 10,
                        TicksRemaining = 4,
                        CarryItemId = "wood",
                        CarryAmount = 1,
                        Position = new Vector3(1.0f, 0.0f, 1.0f)
                    }
                });

            Assert.Contains("Settlement", settlementText);
            Assert.Contains("campfire x1", settlementText);
            Assert.Contains("Worker 1: Harvesting Tree", settlementText);
            Assert.Contains("-> Tree", settlementText);
            Assert.Contains("[wood x1]", settlementText);
        }
    }
}
