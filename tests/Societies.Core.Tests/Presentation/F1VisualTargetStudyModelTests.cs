using Societies.Presentation;
using Xunit;

namespace Societies.Core.Tests.Presentation
{
    public class F1VisualTargetStudyModelTests
    {
        [Fact]
        public void Directions_AreThreeMateriallyDifferentNamedMiniatureTreatments()
        {
            Assert.Collection(
                F1VisualTargetStudyModel.OrderedDirections,
                direction => Assert.Equal("A  HEARTHWOOD CAUSEWAY", F1VisualTargetStudyModel.GetTreatment(direction).Title),
                direction => Assert.Equal("B  REED-KILN WETLANDS", F1VisualTargetStudyModel.GetTreatment(direction).Title),
                direction => Assert.Equal("C  PAINTED SLUICE TOYWORKS", F1VisualTargetStudyModel.GetTreatment(direction).Title));

            Assert.Contains("terracotta clay", F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.HearthwoodCauseway).PrimaryMaterial);
            Assert.Contains("reed matting", F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.ReedKilnWetlands).PrimaryMaterial);
            Assert.Contains("glazed clay channels", F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.PaintedSluiceToyworks).PrimaryMaterial);
        }

        [Fact]
        public void Directions_ExposeDistinctInEngineInteractionLanguagesAndTreatments()
        {
            F1DirectionTreatment hearthwood = F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.HearthwoodCauseway);
            F1DirectionTreatment wetlands = F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.ReedKilnWetlands);
            F1DirectionTreatment toyworks = F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.PaintedSluiceToyworks);

            Assert.Equal(F1InteractionSurfaceStyle.HandboundLedger, hearthwood.InteractionStyle);
            Assert.Equal(F1InteractionSurfaceStyle.KilnTileNotice, wetlands.InteractionStyle);
            Assert.Equal(F1InteractionSurfaceStyle.PaintedControlRail, toyworks.InteractionStyle);
            Assert.Contains("LEDGER", hearthwood.InteractionHeading);
            Assert.Contains("KILN", wetlands.InteractionHeading);
            Assert.Contains("CONTROL RAIL", toyworks.InteractionHeading);
            Assert.Contains("BRACE", hearthwood.LaborControl);
            Assert.Contains("BRACE", wetlands.LaborControl);
            Assert.Contains("BLOCK", toyworks.LaborControl);
            Assert.NotEqual(hearthwood.EvidenceControl, wetlands.EvidenceControl);
            Assert.NotEqual(wetlands.DeferControl, toyworks.DeferControl);
        }

        [Fact]
        public void MiniatureLanguage_IsExplicitAndRejectsTheRealisticAdjacentDirection()
        {
            foreach (F1StudyDirection direction in F1VisualTargetStudyModel.OrderedDirections)
            {
                F1DirectionTreatment treatment = F1VisualTargetStudyModel.GetTreatment(direction);
                Assert.Contains("miniature", treatment.MiniatureStyleTokens);
                Assert.Contains("chunky proportions", treatment.MiniatureStyleTokens);
                Assert.Contains("shallow tabletop", treatment.MiniatureStyleTokens);
                Assert.Contains("matte tactile", treatment.MiniatureStyleTokens);
                Assert.Contains("simplified citizens", treatment.MiniatureStyleTokens);
                Assert.Contains("photorealism", treatment.AvoidedVisualTokens);
                Assert.Contains("realistic PBR", treatment.AvoidedVisualTokens);
                Assert.Contains("realistic human anatomy", treatment.AvoidedVisualTokens);
                Assert.Contains("cinematic fog", treatment.AvoidedVisualTokens);
                Assert.Contains("generic survival HUD", treatment.AvoidedVisualTokens);
                Assert.Contains("toy-store childishness", treatment.AvoidedVisualTokens);
            }
        }

        [Fact]
        public void TabletopWaterMaterialPolicy_UsesAlphaTransparencyAndLeavesOpaquePropsOpaque()
        {
            Assert.True(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(0.12f));
            Assert.True(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(0.998f));
            Assert.False(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(0.999f));
            Assert.False(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(1.0f));
        }

        [Fact]
        public void StudyMaterialPolicy_IsMatteAndUsesTrueAlphaOnlyForTranslucentTabletopAccents()
        {
            F1StudyMaterialProfile transparent = F1VisualTargetStudyModel.GetMaterialProfile(0.24f);
            F1StudyMaterialProfile opaque = F1VisualTargetStudyModel.GetMaterialProfile(1.0f);

            Assert.True(transparent.UsesAlphaTransparency);
            Assert.False(opaque.UsesAlphaTransparency);
            Assert.Equal(0.0f, opaque.Metallic);
            Assert.InRange(opaque.Roughness, 0.9f, 1.0f);
        }

        [Fact]
        public void PhysicalControlColors_HaveWcagContrastForNormalHoverAndPressedStates()
        {
            foreach (F1StudyDirection direction in F1VisualTargetStudyModel.OrderedDirections)
            {
                F1PhysicalControlColors colors = F1VisualTargetStudyModel.GetPhysicalControlColors(direction);
                Assert.True(colors.NormalContrastRatio >= 4.5d, $"{direction} normal contrast is {colors.NormalContrastRatio:F2}:1.");
                Assert.True(colors.HoverContrastRatio >= 4.5d, $"{direction} hover contrast is {colors.HoverContrastRatio:F2}:1.");
                Assert.True(colors.PressedContrastRatio >= 4.5d, $"{direction} pressed contrast is {colors.PressedContrastRatio:F2}:1.");

                F1PressedControlColors pressed = F1VisualTargetStudyModel.GetPressedControlColors(direction);
                Assert.Equal(colors.PressedBackgroundHex, pressed.BackgroundHex);
                Assert.Equal(colors.PressedForegroundHex, pressed.ForegroundHex);
            }
        }

        [Theory]
        [InlineData(1920.0f, 1080.0f)]
        [InlineData(1280.0f, 720.0f)]
        [InlineData(960.0f, 540.0f)]
        public void Layout_FitsNormalAndNarrowSupportedViewports(float width, float height)
        {
            F1StudyLayout layout = F1VisualTargetStudyModel.CalculateLayout(width, height);

            Assert.True(layout.Fits(width, height));
            Assert.InRange(layout.SurfaceWidth, 0.0f, width - layout.Margin * 2.0f);
            Assert.True(layout.HeaderHeight > 0.0f);
        }

        [Fact]
        public void DirectionLayouts_UseTheFinalTreatmentSpecificDimensions()
        {
            AssertDirectionLayout(F1StudyDirection.HearthwoodCauseway, 1920.0f, 1080.0f, 520.0f, 248.0f, false);
            AssertDirectionLayout(F1StudyDirection.ReedKilnWetlands, 1920.0f, 1080.0f, 660.0f, 282.0f, true);
            AssertDirectionLayout(F1StudyDirection.PaintedSluiceToyworks, 1920.0f, 1080.0f, 780.0f, 282.0f, true);
            AssertDirectionLayout(F1StudyDirection.HearthwoodCauseway, 1280.0f, 720.0f, 430.0f, 212.0f, false);
            AssertDirectionLayout(F1StudyDirection.ReedKilnWetlands, 1280.0f, 720.0f, 520.0f, 246.0f, true);
            AssertDirectionLayout(F1StudyDirection.PaintedSluiceToyworks, 1280.0f, 720.0f, 560.0f, 246.0f, true);
        }

        [Fact]
        public void ResponseCycle_UsesWordsAndMarksForPendingRefusalAndConsequence()
        {
            F1ResponsePresentation pending = F1VisualTargetStudyModel.GetPresentation(
                F1StudyResponse.OfferLabor,
                F1StudyState.Pending);
            F1ResponsePresentation refused = F1VisualTargetStudyModel.GetPresentation(
                F1StudyResponse.Defer,
                F1StudyState.Refused);
            F1ResponsePresentation consequence = F1VisualTargetStudyModel.GetPresentation(
                F1StudyResponse.AskForEvidence,
                F1StudyState.Consequence);

            Assert.Equal("↻", pending.StateMark);
            Assert.Contains("ENTERED", pending.StateLabel);
            Assert.Equal("!", refused.StateMark);
            Assert.Contains("DEFERRED", refused.StateLabel);
            Assert.Equal("✓", consequence.StateMark);
            Assert.Contains("WITNESSED", consequence.StateLabel);
        }

        [Fact]
        public void ResponseCycle_IsBoundedAndResetsToLocalOpenPosition()
        {
            AssertResponseCycle(F1StudyResponse.OfferLabor);
            AssertResponseCycle(F1StudyResponse.AskForEvidence);
        }

        [Fact]
        public void DeferCycle_ResetsFromTheVisibleRefusalWithoutAHiddenConsequence()
        {
            F1StudyState refused = F1VisualTargetStudyModel.NextState(F1StudyResponse.Defer, F1StudyState.Awaiting);
            F1StudyState reset = F1VisualTargetStudyModel.NextState(F1StudyResponse.Defer, refused);

            Assert.Equal(F1StudyState.Refused, refused);
            Assert.Equal("DEFERRED / NOT NEUTRAL", F1VisualTargetStudyModel.GetPresentation(F1StudyResponse.Defer, refused).StateLabel);
            Assert.Equal(F1StudyState.Awaiting, reset);
            Assert.Equal("POSITION OPEN", F1VisualTargetStudyModel.GetPresentation(F1StudyResponse.None, reset).StateLabel);
            Assert.Equal(
                F1StudyState.Awaiting,
                F1VisualTargetStudyModel.GetPresentation(F1StudyResponse.Defer, F1StudyState.Consequence).State);
        }

        [Fact]
        public void ResponseCycle_AdvanceWithoutASelectionKeepsThePositionOpen()
        {
            F1StudyState state = F1VisualTargetStudyModel.NextState(F1StudyResponse.None, F1StudyState.Awaiting);

            Assert.Equal(F1StudyState.Awaiting, state);
            Assert.Equal(
                "POSITION OPEN",
                F1VisualTargetStudyModel.GetPresentation(F1StudyResponse.None, state).StateLabel);
        }

        private static void AssertResponseCycle(F1StudyResponse response)
        {
            F1StudyState pending = F1VisualTargetStudyModel.NextState(response, F1StudyState.Awaiting);
            F1StudyState consequence = F1VisualTargetStudyModel.NextState(response, pending);
            F1StudyState reset = F1VisualTargetStudyModel.NextState(response, consequence);

            Assert.Equal(F1StudyState.Pending, pending);
            Assert.True(F1VisualTargetStudyModel.GetPresentation(response, pending).AllowsAdvance);
            Assert.Equal(F1StudyState.Consequence, consequence);
            F1ResponsePresentation consequencePresentation = F1VisualTargetStudyModel.GetPresentation(response, consequence);
            Assert.Equal("✓", consequencePresentation.StateMark);
            Assert.True(consequencePresentation.AllowsAdvance);
            Assert.Equal(F1StudyState.Awaiting, reset);
        }

        private static void AssertDirectionLayout(F1StudyDirection direction, float width, float height, float expectedWidth, float expectedHeight, bool expectedRail)
        {
            F1DirectionSurfaceLayout layout = F1VisualTargetStudyModel.CalculateDirectionSurfaceLayout(direction, width, height);

            Assert.Equal(expectedWidth, layout.SurfaceWidth);
            Assert.Equal(expectedHeight, layout.SurfaceHeight);
            Assert.Equal(expectedRail, layout.UsesHorizontalPhysicalRail);
        }
    }
}
