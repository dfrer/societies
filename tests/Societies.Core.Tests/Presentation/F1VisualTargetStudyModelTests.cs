using Societies.Presentation;
using Xunit;

namespace Societies.Core.Tests.Presentation
{
    public class F1VisualTargetStudyModelTests
    {
        [Fact]
        public void Directions_AreThreeMateriallyDifferentNamedTreatments()
        {
            Assert.Collection(
                F1VisualTargetStudyModel.OrderedDirections,
                direction => Assert.Equal("REEDWORK FOUNDRY", F1VisualTargetStudyModel.GetTreatment(direction).Title),
                direction => Assert.Equal("FLOODPLAIN COMMONS", F1VisualTargetStudyModel.GetTreatment(direction).Title),
                direction => Assert.Equal("SLUICE OBSERVATORY", F1VisualTargetStudyModel.GetTreatment(direction).Title));

            Assert.Contains("reed fibre", F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.ReedworkFoundry).PrimaryMaterial);
            Assert.Contains("canvas", F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.FloodplainCommons).PrimaryMaterial);
            Assert.Contains("gauge glass", F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.SluiceObservatory).PrimaryMaterial);
        }

        [Fact]
        public void Directions_ExposeDistinctInEngineInteractionLanguagesAndTreatments()
        {
            F1DirectionTreatment reedwork = F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.ReedworkFoundry);
            F1DirectionTreatment commons = F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.FloodplainCommons);
            F1DirectionTreatment observatory = F1VisualTargetStudyModel.GetTreatment(F1StudyDirection.SluiceObservatory);

            Assert.Equal(F1InteractionSurfaceStyle.InstrumentStack, reedwork.InteractionStyle);
            Assert.Equal(F1InteractionSurfaceStyle.PublicNotice, commons.InteractionStyle);
            Assert.Equal(F1InteractionSurfaceStyle.CalibrationRail, observatory.InteractionStyle);
            Assert.Equal("WORK ORDER: CAUSEWAY", reedwork.InteractionHeading);
            Assert.Equal("NOTICE / SPEAK INTO RECORD", commons.InteractionHeading);
            Assert.Equal("FLOW POSITION: CAUSEWAY", observatory.InteractionHeading);
            Assert.Contains("BRACE", reedwork.LaborControl);
            Assert.Contains("SIGN ON", commons.LaborControl);
            Assert.Contains("COMMIT", observatory.LaborControl);
            Assert.NotEqual(reedwork.EvidenceControl, commons.EvidenceControl);
            Assert.NotEqual(commons.DeferControl, observatory.DeferControl);
        }

        [Fact]
        public void MistMaterialPolicy_UsesAlphaTransparencyAndLeavesOpaquePropsOpaque()
        {
            Assert.True(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(0.12f));
            Assert.True(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(0.998f));
            Assert.False(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(0.999f));
            Assert.False(F1VisualTargetStudyModel.ShouldUseAlphaTransparency(1.0f));
        }

        [Fact]
        public void PressedResponseControls_HaveWcagNormalTextContrastForEveryDirection()
        {
            foreach (F1StudyDirection direction in F1VisualTargetStudyModel.OrderedDirections)
            {
                F1PressedControlColors colors = F1VisualTargetStudyModel.GetPressedControlColors(direction);
                Assert.True(
                    colors.ContrastRatio >= 4.5d,
                    $"{direction} pressed pair {colors.BackgroundHex}/{colors.ForegroundHex} has contrast {colors.ContrastRatio:F2}:1.");
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
    }
}
