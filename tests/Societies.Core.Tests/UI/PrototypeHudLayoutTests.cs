using Societies.Core;
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
            PrototypeHudPresentationState harvested = PrototypeHudPresentationState.Create(
                PrototypeSettlementDirective.Neutral,
                PrototypeSettlementClassification.Strained,
                null,
                "Harvested reeds x1; site depleted",
                "TARGET: Reed Bed — depleted; find another node");

            Assert.Equal(PrototypeHudCue.FoodAndFuel, blocked.DirectiveCue);
            Assert.Equal(PrototypeHudCue.FoodAndFuel, blocked.SettlementCue);
            Assert.Equal(PrototypeHudCue.FoodAndFuel, blocked.InteractionCue);
            Assert.Equal(PrototypeHudCue.BlockedInteraction, blocked.StatusCue);
            Assert.Equal(PrototypeHudCue.Shelter, contributed.DirectiveCue);
            Assert.Equal(PrototypeHudCue.Stable, contributed.SettlementCue);
            Assert.Equal(PrototypeHudCue.ContributionSuccess, contributed.StatusCue);
            Assert.Equal(PrototypeHudCue.DepletedInteraction, harvested.InteractionCue);
            Assert.Equal(PrototypeHudCue.DepletedInteraction, harvested.StatusCue);

            PrototypeHudPresentationState noResources = PrototypeHudPresentationState.Create(
                PrototypeSettlementDirective.Neutral,
                PrototypeSettlementClassification.Strained,
                null,
                "No resources to contribute. Harvest raw resources first.",
                string.Empty);
            Assert.Equal(PrototypeHudCue.BlockedInteraction, noResources.StatusCue);

            PrototypeHudPresentationState mixed = PrototypeHudPresentationState.Create(
                PrototypeSettlementDirective.Neutral,
                PrototypeSettlementClassification.Strained,
                null,
                "Contributed reeds x1",
                "TARGET: Reed Bed  ·  move closer to harvest");
            Assert.Equal(PrototypeHudCue.BlockedInteraction, mixed.InteractionCue);
            Assert.Equal(PrototypeHudCue.ContributionSuccess, mixed.StatusCue);
        }

        [Fact]
        public void LoopProgress_UsesInventoryContributionAndPolicyProjectionsWithoutRegressing()
        {
            PrototypeHudLoopProgress preHarvest = PrototypeHudLoopProgress.Create(
                hasCarriedRawResource: false,
                totalContributedQuantity: 0,
                PrototypeCivicPolicy.Neutral);
            PrototypeHudLoopProgress carried = PrototypeHudLoopProgress.Create(
                hasCarriedRawResource: true,
                totalContributedQuantity: 0,
                PrototypeCivicPolicy.Neutral);
            PrototypeHudLoopProgress contributed = PrototypeHudLoopProgress.Create(
                hasCarriedRawResource: false,
                totalContributedQuantity: 1,
                PrototypeCivicPolicy.Neutral);
            PrototypeHudLoopProgress selected = PrototypeHudLoopProgress.Create(
                hasCarriedRawResource: false,
                totalContributedQuantity: 1,
                PrototypeCivicPolicy.ProtectWetland);
            PrototypeHudLoopProgress selectedWithoutContribution = PrototypeHudLoopProgress.Create(
                hasCarriedRawResource: false,
                totalContributedQuantity: 0,
                PrototypeCivicPolicy.ProtectWetland);
            PrototypeHudLoopProgress noLongerCarried = PrototypeHudLoopProgress.Create(
                hasCarriedRawResource: false,
                totalContributedQuantity: 0,
                PrototypeCivicPolicy.Neutral);

            Assert.Equal("GATHER / DEPOT / DECIDE", preHarvest.DecisionRailText);
            Assert.Equal("GATHER ✓ / DEPOT / DECIDE", carried.DecisionRailText);
            Assert.Equal("GATHER ✓ / DEPOT ✓ / DECIDE — click or [4]/[5]", contributed.DecisionRailText);
            Assert.Equal("GATHER ✓ / DEPOT ✓ / DECIDE ✓", selected.DecisionRailText);
            Assert.Equal("GATHER / DEPOT / DECIDE ✓", selectedWithoutContribution.DecisionRailText);
            Assert.Equal("GATHER / DEPOT / DECIDE", noLongerCarried.DecisionRailText);
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
            Assert.Contains("Stable conditions: C2/2 M3/3 F4/4 B50%", activeText);
            Assert.Contains("Hold: stable 1/2 | collapse 0/3", activeText);
            Assert.DoesNotContain("?", activeText);
            Assert.Contains("Outcome: Stable: all conditions held 2/2 ticks", terminalText);
            Assert.Contains("Hold: stable 2/2 | collapse 0/3", terminalText);
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
                // The compact inspector may use the final civic line when a policy is
                // selected; the ordinary assignment-only fixture deliberately leaves it free.
                Assert.InRange(inspectorBudget.EstimatedRenderedLines, 1, 7);
                Assert.Equal(7, inspectorBudget.AvailableLines);
                Assert.Equal(1, contributionBudget.EstimatedRenderedLines);
                Assert.Equal(1, contributionBudget.AvailableLines);
                Assert.Equal(1, interactionBudget.EstimatedRenderedLines);
                Assert.Equal(1, interactionBudget.AvailableLines);
            }
        }

        [Fact]
        public void CompactWetlandText_NeutralStateIsConciseAndDoesNotInventASelection()
        {
            string text = PrototypeHudTextBuilder.BuildCompactWetlandText(new PrototypeWetlandSnapshot
            {
                PolicyId = "neutral",
                ReedQuotaLimit = 0,
                ReedQuotaConsumed = 0,
                WetlandHealth = 60,
                WetlandHealthBand = "strained"
            });

            Assert.Equal(
                "Policy: not selected (neutral)\nWetland: Strained 60/100",
                text);
            Assert.DoesNotContain("Reeds:", text);
            Assert.DoesNotContain("Effect:", text);
        }

        [Theory]
        [InlineData("protect_wetland", 4, 0, 75, "healthy", "Protect", "fewer; preserved")]
        [InlineData("draw_down_wetland", 12, 0, 45, "strained", "Drawdown", "more; degrades")]
        public void CompactWetlandText_SelectedPoliciesExposeDistinctConsequences(
            string policyId,
            int quotaLimit,
            int quotaConsumed,
            int health,
            string band,
            string policyLabel,
            string consequence)
        {
            string text = PrototypeHudTextBuilder.BuildCompactWetlandText(new PrototypeWetlandSnapshot
            {
                PolicyId = policyId,
                ReedQuotaLimit = quotaLimit,
                ReedQuotaConsumed = quotaConsumed,
                WetlandHealth = health,
                WetlandHealthBand = band
            });

            Assert.Contains($"Policy: {policyLabel} | {char.ToUpperInvariant(band[0]) + band[1..]} {health}/100", text);
            Assert.Contains($"Reeds: {quotaConsumed}/{quotaLimit}, {quotaLimit - quotaConsumed} left | {consequence}", text);
        }

        [Fact]
        public void CompactWetlandText_PostHarvestReflectsQuotaHealthAndBand()
        {
            string text = PrototypeHudTextBuilder.BuildCompactWetlandText(new PrototypeWetlandSnapshot
            {
                PolicyId = "draw_down_wetland",
                ReedQuotaLimit = 12,
                ReedQuotaConsumed = 5,
                WetlandHealth = 35,
                WetlandHealthBand = "degraded"
            });

            Assert.Contains("Policy: Drawdown | Degraded 35/100", text);
            Assert.Contains("Reeds: 5/12, 7 left | more; degrades", text);
        }

        [Fact]
        public void CompactCrisisText_NullWetlandPreservesLegacyCompatibility()
        {
            Assert.Equal(
                "Crisis: none",
                PrototypeHudTextBuilder.BuildCompactCrisisText(
                    null,
                    PrototypeSettlementDirective.Neutral,
                    null,
                    null));
            Assert.Equal(string.Empty, PrototypeHudTextBuilder.BuildCompactWetlandText(null));
        }

        [Theory]
        [InlineData(1920.0f, 1080.0f)]
        [InlineData(1280.0f, 720.0f)]
        public void CompactCrisisText_WetlandConsequenceFitsTargetCardBudget(float width, float height)
        {
            PrototypeCrisisState crisis = CreateCrisis();
            crisis.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            string text = PrototypeHudTextBuilder.BuildCompactCrisisText(
                crisis,
                PrototypeSettlementDirective.FoodAndFuel,
                new Dictionary<string, long> { ["berries"] = 3, ["logs"] = 2 },
                new PrototypeWetlandSnapshot
                {
                    PolicyId = "protect_wetland",
                    ReedQuotaLimit = 4,
                    ReedQuotaConsumed = 0,
                    WetlandHealth = 75,
                    WetlandHealthBand = "healthy"
                });

            PrototypeHudTextBudget budget = PrototypeHudLayout.Calculate(width, height)
                .GetTextBudget(PrototypeHudLayout.Crisis, text, 16);
            Assert.True(budget.Fits, $"Wetland consequence budget: {budget}");
        }

        [Fact]
        public void CompactCrisisText_TerminalSelectedWetlandFitsTargetCardBudget()
        {
            PrototypeCrisisState terminal = CreateCrisis();
            terminal.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            terminal.Advance(new PrototypeCrisisObservation(3, 2, 3, 4, 50));
            string text = PrototypeHudTextBuilder.BuildCompactCrisisText(
                terminal,
                PrototypeSettlementDirective.FoodAndFuel,
                new Dictionary<string, long> { ["berries"] = 3, ["logs"] = 2 },
                new PrototypeWetlandSnapshot
                {
                    PolicyId = "protect_wetland",
                    ReedQuotaLimit = 4,
                    ReedQuotaConsumed = 0,
                    WetlandHealth = 75,
                    WetlandHealthBand = "healthy"
                });

            Assert.Contains("Outcome: Stable: all conditions held 2/2 ticks", text);
            Assert.Contains("Hold: stable 2/2 | collapse 0/3", text);
            Assert.Contains("Policy: Protect | Healthy 75/100", text);
            Assert.Contains("Reeds: 0/4, 4 left | fewer; preserved", text);
            PrototypeHudTextBudget budget = PrototypeHudLayout.Calculate(1280.0f, 720.0f)
                .GetTextBudget(PrototypeHudLayout.Crisis, text, 16);
            Assert.True(budget.Fits, $"Terminal wetland consequence budget: {budget}");
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
