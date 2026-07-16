using Societies.Simulation;
using Societies.UI;
using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeHudLayoutTests
    {
        [Theory]
        [InlineData(1920.0f, 1080.0f)]
        [InlineData(1280.0f, 720.0f)]
        public void Calculate_TargetResolutionsFitWithoutCardOverlaps(float width, float height)
        {
            PrototypeHudLayout layout = PrototypeHudLayout.Calculate(width, height);

            Assert.False(layout.HasOverlaps());
            foreach (PrototypeHudBounds bounds in layout.Bounds.Values)
            {
                Assert.True(bounds.FitsWithin(width, height));
            }
            Assert.True(layout[PrototypeHudLayout.Help].Width > layout[PrototypeHudLayout.Crisis].Width);
            Assert.True(layout[PrototypeHudLayout.Interaction].Y > layout[PrototypeHudLayout.World].Bottom);
            if (width == 1280.0f && height == 720.0f)
            {
                Assert.Equal(70.0f, layout[PrototypeHudLayout.World].Height);
                Assert.Equal(46.0f, layout[PrototypeHudLayout.Interaction].Height);
                Assert.Equal(46.0f, layout[PrototypeHudLayout.Status].Height);
                Assert.True(layout[PrototypeHudLayout.Interaction].Y - layout[PrototypeHudLayout.World].Bottom > 0.0f);
            }
        }

        [Fact]
        public void PresentationState_MapsDirectiveOutcomeAndInteractionFeedbackToVisibleCues()
        {
            PrototypeHudPresentationState blocked = PrototypeHudPresentationState.Create(
                PrototypeSettlementDirective.FoodAndFuel,
                PrototypeSettlementClassification.Strained,
                null,
                "Move closer to the central depot",
                "Look at a resource node and press E");
            PrototypeHudPresentationState contributed = PrototypeHudPresentationState.Create(
                PrototypeSettlementDirective.Shelter,
                PrototypeSettlementClassification.Stable,
                null,
                "Contributed logs x3",
                string.Empty);

            Assert.Equal(PrototypeHudCue.FoodAndFuel, blocked.DirectiveCue);
            Assert.Equal(PrototypeHudCue.FoodAndFuel, blocked.SettlementCue);
            Assert.Equal(PrototypeHudCue.BlockedInteraction, blocked.InteractionCue);
            Assert.Equal(PrototypeHudCue.Shelter, contributed.DirectiveCue);
            Assert.Equal(PrototypeHudCue.Stable, contributed.SettlementCue);
            Assert.Equal(PrototypeHudCue.ContributionSuccess, contributed.InteractionCue);

            PrototypeHudPresentationState noResources = PrototypeHudPresentationState.Create(
                PrototypeSettlementDirective.Neutral,
                PrototypeSettlementClassification.Strained,
                null,
                "No resources to contribute. Harvest raw resources first.",
                string.Empty);
            Assert.Equal(PrototypeHudCue.BlockedInteraction, noResources.InteractionCue);
        }

        [Theory]
        [InlineData(1920.0f, 1080.0f)]
        [InlineData(1280.0f, 720.0f)]
        public void CompactNormalPlayText_RequiredReadingsFitTheirCardBudgets(float width, float height)
        {
            PrototypeCrisisState active = CreateCrisis();
            active.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            PrototypeCrisisState terminal = CreateCrisis();
            terminal.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            terminal.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            string activeText = PrototypeHudTextBuilder.BuildCompactCrisisText(
                active,
                PrototypeSettlementDirective.FoodAndFuel,
                new Dictionary<string, long> { ["berries"] = 3, ["logs"] = 2 });
            string terminalText = PrototypeHudTextBuilder.BuildCompactCrisisText(
                terminal,
                PrototypeSettlementDirective.FoodAndFuel,
                new Dictionary<string, long> { ["berries"] = 3, ["logs"] = 2 });
            string inspectorText = PrototypeHudTextBuilder.BuildCompactInspectorText(
                new PrototypeWorkerState
                {
                    DisplayName = "Citizen 2",
                    Role = PrototypeCitizenRole.Builder,
                    CurrentOrderKind = PrototypeWorkOrderKind.Build,
                    CurrentOrderReason = "Shelter hut construction",
                    Needs = new PrototypeNeedState { Nutrition = 64.0f, Fatigue = 40.0f }
                },
                new PrototypeStructureState { DisplayName = "Hut", IsBuilt = false });
            PrototypeHudLayout layout = PrototypeHudLayout.Calculate(width, height);

            Assert.Contains("Time:", activeText);
            Assert.Contains("Directive: Food & Fuel", activeText);
            Assert.Contains("Contributed: 5", activeText);
            Assert.Contains("Hold: stable 1/2 | collapse 0/3", activeText);
            Assert.DoesNotContain("?", activeText);
            Assert.Contains("Outcome: Stable: all conditions held 2/2 ticks", terminalText);
            Assert.Contains("Why: Shelter hut construction", inspectorText);
            Assert.Contains("Needs: nutrition 64 | fatigue 40", inspectorText);
            Assert.Contains("Route: 0.0 m | 0 ticks", inspectorText);
            Assert.DoesNotContain("?", inspectorText);
            Assert.Contains("Structure: Hut (planned)", inspectorText);
            PrototypeHudTextBudget activeBudget = layout.GetTextBudget(PrototypeHudLayout.Crisis, activeText, 16);
            PrototypeHudTextBudget terminalBudget = layout.GetTextBudget(PrototypeHudLayout.Crisis, terminalText, 16);
            PrototypeHudTextBudget inspectorBudget = layout.GetTextBudget(PrototypeHudLayout.Inspector, inspectorText, 16);
            PrototypeHudTextBudget contributionBudget = layout.GetTextBudget(
                PrototypeHudLayout.Status,
                "Contributed logs x3",
                18);
            PrototypeHudTextBudget interactionBudget = layout.GetTextBudget(
                PrototypeHudLayout.Interaction,
                "Look at a resource node and press E",
                18);
            Assert.True(activeBudget.Fits, $"Active crisis budget: {activeBudget}");
            Assert.True(terminalBudget.Fits, $"Terminal crisis budget: {terminalBudget}");
            Assert.True(inspectorBudget.Fits, $"Inspector budget: {inspectorBudget}");
            Assert.True(contributionBudget.Fits, $"Contribution feedback budget: {contributionBudget}");
            Assert.True(interactionBudget.Fits, $"Interaction prompt budget: {interactionBudget}");
            if (width == 1280.0f && height == 720.0f)
            {
                Assert.Equal(7, inspectorBudget.EstimatedRenderedLines);
                Assert.Equal(7, inspectorBudget.AvailableLines);
                Assert.Equal(1, contributionBudget.EstimatedRenderedLines);
                Assert.Equal(1, contributionBudget.AvailableLines);
                Assert.Equal(1, interactionBudget.EstimatedRenderedLines);
                Assert.Equal(1, interactionBudget.AvailableLines);
            }
        }

        private static PrototypeCrisisState CreateCrisis()
        {
            return new PrototypeCrisisState(new PrototypeCrisisDefinition
            {
                Id = "hud_budget",
                DisplayName = "HUD Budget",
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
            });
        }
    }
}
