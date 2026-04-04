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
            Assert.Contains("F9 load snapshot", helpText);
        }

        [Fact]
        public void BuildDebugText_IncludesSessionModeAndTick()
        {
            string debugText = PrototypeHudTextBuilder.BuildDebugText(60, 12, "08:30", "Clear", "Local", 45);

            Assert.Contains("Mode: Local", debugText);
            Assert.Contains("Tick: 45", debugText);
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
                        CarryItemId = "wood",
                        CarryAmount = 1,
                        Position = new Vector3(1.0f, 0.0f, 1.0f)
                    }
                });

            Assert.Contains("Settlement", settlementText);
            Assert.Contains("campfire x1", settlementText);
            Assert.Contains("Worker 1: Harvesting carrying wood x1", settlementText);
        }
    }
}
