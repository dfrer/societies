using System;
using System.Collections.Generic;

namespace Societies.Presentation
{
    /// <summary>
    /// Fixed, presentation-only copy and layout data for the F1 visual direction study.
    /// It deliberately has no simulation, persistence, network, or command dependency.
    /// </summary>
    public enum F1StudyDirection
    {
        ReedworkFoundry,
        FloodplainCommons,
        SluiceObservatory
    }

    public enum F1StudyResponse
    {
        None,
        OfferLabor,
        AskForEvidence,
        Defer
    }

    public enum F1StudyState
    {
        Awaiting,
        Pending,
        Refused,
        Consequence
    }

    public enum F1InteractionSurfaceStyle
    {
        InstrumentStack,
        PublicNotice,
        CalibrationRail
    }

    public sealed record F1DirectionTreatment(
        F1StudyDirection Direction,
        string Title,
        string Strapline,
        string PlaceLabel,
        string InteractionHeading,
        F1InteractionSurfaceStyle InteractionStyle,
        string LaborControl,
        string EvidenceControl,
        string DeferControl,
        string PrimaryMaterial,
        string VisualCue);

    public sealed record F1ResponsePresentation(
        F1StudyState State,
        string StateLabel,
        string StateMark,
        string MaraLine,
        string ConsequenceLine,
        bool AllowsAdvance);

    public sealed record F1StudyLayout(
        float Margin,
        float HeaderHeight,
        float SurfaceWidth,
        float SurfaceHeight,
        bool IsCompact)
    {
        public bool Fits(float width, float height) =>
            Margin >= 12.0f && HeaderHeight > 0.0f && SurfaceWidth <= width - Margin * 2.0f &&
            SurfaceHeight <= height - Margin * 2.0f;
    }

    public sealed record F1PressedControlColors(string BackgroundHex, string ForegroundHex)
    {
        public double ContrastRatio => CalculateContrastRatio(BackgroundHex, ForegroundHex);

        private static double CalculateContrastRatio(string first, string second)
        {
            double firstLuminance = RelativeLuminance(first);
            double secondLuminance = RelativeLuminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + 0.05d) /
                   (Math.Min(firstLuminance, secondLuminance) + 0.05d);
        }

        private static double RelativeLuminance(string hex)
        {
            if (hex.Length != 6)
            {
                throw new ArgumentException("Expected a six-character RGB hex value.", nameof(hex));
            }

            double red = ToLinear(hex, 0);
            double green = ToLinear(hex, 2);
            double blue = ToLinear(hex, 4);
            return 0.2126d * red + 0.7152d * green + 0.0722d * blue;
        }

        private static double ToLinear(string hex, int offset)
        {
            double channel = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255.0d;
            return channel <= 0.04045d
                ? channel / 12.92d
                : Math.Pow((channel + 0.055d) / 1.055d, 2.4d);
        }
    }

    public static class F1VisualTargetStudyModel
    {
        private static readonly IReadOnlyDictionary<F1StudyDirection, F1DirectionTreatment> Treatments =
            new Dictionary<F1StudyDirection, F1DirectionTreatment>
            {
                [F1StudyDirection.ReedworkFoundry] = new(
                    F1StudyDirection.ReedworkFoundry,
                    "REEDWORK FOUNDRY",
                    "WEATHERED ECOLOGICAL FUTURISM",
                    "SOUTH SPAN / REED LINE",
                    "WORK ORDER: CAUSEWAY",
                    F1InteractionSurfaceStyle.InstrumentStack,
                    "Q  TAKE THE BRACE",
                    "W  READ THE GAUGE",
                    "E  HOLD BACK",
                    "reed fibre / oxidized iron / amber work light",
                    "Layered reed ribs hold against a cold, reflective marsh."),
                [F1StudyDirection.FloodplainCommons] = new(
                    F1StudyDirection.FloodplainCommons,
                    "FLOODPLAIN COMMONS",
                    "CIVIC FIELDCRAFT",
                    "LOW WATER NOTICE / SOUTH SPAN",
                    "NOTICE / SPEAK INTO RECORD",
                    F1InteractionSurfaceStyle.PublicNotice,
                    "Q  SIGN ON FOR LABOR",
                    "W  READ THE WATER MARKS",
                    "E  POST A DEFERRAL",
                    "timber / canvas / enamel marks / safety orange",
                    "A public repair is posted in view of the depot and its witnesses."),
                [F1StudyDirection.SluiceObservatory] = new(
                    F1StudyDirection.SluiceObservatory,
                    "SLUICE OBSERVATORY",
                    "HYDROLOGICAL INSTRUMENTALISM",
                    "BASIN 03 / SOUTH CAUSEWAY",
                    "FLOW POSITION: CAUSEWAY",
                    F1InteractionSurfaceStyle.CalibrationRail,
                    "Q  COMMIT HANDS",
                    "W  REQUEST MEASURE",
                    "E  HOLD POSITION",
                    "limewash / ceramic / blue-green gauge glass",
                    "Measured water, a hard horizon, and a deliberately quiet decision surface.")
            };

        public static F1DirectionTreatment GetTreatment(F1StudyDirection direction) => Treatments[direction];

        public static IReadOnlyList<F1StudyDirection> OrderedDirections { get; } = new[]
        {
            F1StudyDirection.ReedworkFoundry,
            F1StudyDirection.FloodplainCommons,
            F1StudyDirection.SluiceObservatory
        };

        public static F1StudyLayout CalculateLayout(float width, float height)
        {
            bool compact = width < 1500.0f || height < 820.0f;
            float margin = compact ? 18.0f : 32.0f;
            float headerHeight = compact ? 56.0f : 76.0f;
            float surfaceWidth = MathF.Min(compact ? 430.0f : 520.0f, width - margin * 2.0f);
            float surfaceHeight = compact ? 212.0f : 248.0f;
            return new F1StudyLayout(margin, headerHeight, surfaceWidth, surfaceHeight, compact);
        }

        /// <summary>Shared by the mesh factory so translucent atmosphere is never silently rendered opaque.</summary>
        public static bool ShouldUseAlphaTransparency(float alpha) => alpha < 0.999f;

        /// <summary>Opaque pressed-state pairs with WCAG normal-text contrast at or above 4.5:1.</summary>
        public static F1PressedControlColors GetPressedControlColors(F1StudyDirection direction) => direction switch
        {
            F1StudyDirection.ReedworkFoundry => new F1PressedControlColors("1B2423", "D8E8DB"),
            F1StudyDirection.FloodplainCommons => new F1PressedControlColors("2A3029", "FFF2CE"),
            F1StudyDirection.SluiceObservatory => new F1PressedControlColors("102125", "EFF5E8"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown F1 direction.")
        };

        public static F1ResponsePresentation GetPresentation(F1StudyResponse response, F1StudyState state)
        {
            if (response == F1StudyResponse.None || state == F1StudyState.Awaiting)
            {
                return new F1ResponsePresentation(
                    F1StudyState.Awaiting,
                    "POSITION OPEN",
                    "◇",
                    "Mara: “The water is taking the old fill. Decide with your hands open.”",
                    "Ivo is bracing the split. Sena watches from the depot edge.",
                    false);
            }

            return (response, state) switch
            {
                (F1StudyResponse.OfferLabor, F1StudyState.Pending) => new(
                    F1StudyState.Pending, "LABOR ENTERED", "↻",
                    "Mara: “Take Ivo’s far side. Keep the reed bed clear.”",
                    "Pending: your labor is being placed beside the repair.", true),
                (F1StudyResponse.AskForEvidence, F1StudyState.Pending) => new(
                    F1StudyState.Pending, "EVIDENCE BEING SET", "↻",
                    "Mara unfolds the water marks; Ivo keeps the brace from slipping.",
                    "Pending: the evidence is visible before the next choice.", true),
                (F1StudyResponse.Defer, F1StudyState.Refused) => new(
                    F1StudyState.Refused, "DEFERRED / NOT NEUTRAL", "!",
                    "Mara: “Defer is a position while the causeway sinks.”",
                    "Refusal: the repair proceeds without your commitment.", true),
                (F1StudyResponse.OfferLabor, F1StudyState.Consequence) => new(
                    F1StudyState.Consequence, "HANDS ON THE SPAN", "✓",
                    "Ivo: “Brace is holding. Mara has the reed line.”",
                    "Consequence: the crossing holds long enough to move the depot stock.", true),
                (F1StudyResponse.AskForEvidence, F1StudyState.Consequence) => new(
                    F1StudyState.Consequence, "MARKS WITNESSED", "✓",
                    "Mara: “The silt line tells us where not to build again.”",
                    "Consequence: the repair boundary is now legible to everyone here.", true),
                _ => new(
                    F1StudyState.Awaiting, "POSITION OPEN", "◇",
                    "Mara: “The water is taking the old fill. Decide with your hands open.”",
                    "Ivo is bracing the split. Sena watches from the depot edge.",
                    false)
            };
        }

        public static F1StudyState NextState(F1StudyResponse response, F1StudyState state) =>
            state switch
            {
                F1StudyState.Awaiting when response == F1StudyResponse.None => F1StudyState.Awaiting,
                F1StudyState.Awaiting when response == F1StudyResponse.Defer => F1StudyState.Refused,
                F1StudyState.Awaiting => F1StudyState.Pending,
                F1StudyState.Pending => F1StudyState.Consequence,
                F1StudyState.Refused => F1StudyState.Awaiting,
                _ => F1StudyState.Awaiting
            };
    }
}
