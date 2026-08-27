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
        HearthwoodCauseway,
        ReedKilnWetlands,
        PaintedSluiceToyworks
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
        HandboundLedger,
        KilnTileNotice,
        PaintedControlRail
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
        string VisualCue,
        IReadOnlyList<string> MiniatureStyleTokens,
        IReadOnlyList<string> AvoidedVisualTokens);

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

    public sealed record F1DirectionSurfaceLayout(
        float SurfaceWidth,
        float SurfaceHeight,
        bool UsesHorizontalPhysicalRail);

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

    public sealed record F1PhysicalControlColors(
        string NormalBackgroundHex,
        string NormalForegroundHex,
        string HoverBackgroundHex,
        string HoverForegroundHex,
        string PressedBackgroundHex,
        string PressedForegroundHex)
    {
        public double NormalContrastRatio => CalculateContrastRatio(NormalBackgroundHex, NormalForegroundHex);
        public double HoverContrastRatio => CalculateContrastRatio(HoverBackgroundHex, HoverForegroundHex);
        public double PressedContrastRatio => CalculateContrastRatio(PressedBackgroundHex, PressedForegroundHex);

        private static double CalculateContrastRatio(string first, string second)
        {
            return new F1PressedControlColors(first, second).ContrastRatio;
        }
    }

    public sealed record F1StudyMaterialProfile(float Roughness, float Metallic, bool UsesAlphaTransparency);

    public static class F1VisualTargetStudyModel
    {
        private static readonly IReadOnlyDictionary<F1StudyDirection, F1DirectionTreatment> Treatments =
            new Dictionary<F1StudyDirection, F1DirectionTreatment>
            {
                [F1StudyDirection.HearthwoodCauseway] = new(
                    F1StudyDirection.HearthwoodCauseway,
                    "A  HEARTHWOOD CAUSEWAY",
                    "CHUNKY HAND-CARVED COMMONWORK",
                    "SOUTH SPAN / HEARTHWOOD TABLE",
                    "HANDWORK LEDGER / CAUSEWAY",
                    F1InteractionSurfaceStyle.HandboundLedger,
                    "Q  TAKE THE WOOD BRACE",
                    "W  READ THE CLAY MARKS",
                    "E  SET WORK ASIDE",
                    "hand-carved wood / terracotta clay / wool felt",
                    "Broad peg-planks, a terracotta split marker, and a felt work mat make the shared repair tactile.",
                    new[] { "miniature", "chunky proportions", "shallow tabletop", "matte tactile", "simplified citizens" },
                    new[] { "photorealism", "realistic PBR", "realistic human anatomy", "cinematic fog", "generic survival HUD", "toy-store childishness" }),
                [F1StudyDirection.ReedKilnWetlands] = new(
                    F1StudyDirection.ReedKilnWetlands,
                    "B  REED-KILN WETLANDS",
                    "EARTHENWARE WETLAND CRAFT",
                    "SOUTH SPAN / KILN MIRE TABLE",
                    "KILN TILE / WITNESS MARKS",
                    F1InteractionSurfaceStyle.KilnTileNotice,
                    "Q  LIFT THE KILN BRACE",
                    "W  TRACE THE WET MARKS",
                    "E  LEAVE A VISIBLE DEFER",
                    "rough earthenware / reed matting / scorched wood",
                    "Uneven clay islands, woven reed mats, and scorched braces hold a wetland repair in deliberate asymmetry.",
                    new[] { "miniature", "chunky proportions", "shallow tabletop", "matte tactile", "organic asymmetry", "simplified citizens" },
                    new[] { "photorealism", "realistic PBR", "realistic human anatomy", "cinematic fog", "generic survival HUD", "toy-store childishness" }),
                [F1StudyDirection.PaintedSluiceToyworks] = new(
                    F1StudyDirection.PaintedSluiceToyworks,
                    "C  PAINTED SLUICE TOYWORKS",
                    "GRAPHIC CIVIC CAUSE AND EFFECT",
                    "SOUTH SPAN / PAINTED FLOW TABLE",
                    "PAINTED CONTROL RAIL / CAUSEWAY",
                    F1InteractionSurfaceStyle.PaintedControlRail,
                    "Q  PLACE THE HAND BLOCK",
                    "W  CHECK THE GLAZED GAUGE",
                    "E  PARK THE SLUICE TOKEN",
                    "painted wood blocks / glazed clay channels / dial gauges",
                    "Cool painted blocks route a glazed clay channel through a visible wheel, so each civic response reads as a mechanism.",
                    new[] { "miniature", "chunky proportions", "shallow tabletop", "matte tactile", "graphic mechanism", "simplified citizens" },
                    new[] { "photorealism", "realistic PBR", "realistic human anatomy", "cinematic fog", "generic survival HUD", "toy-store childishness" })
            };

        public static F1DirectionTreatment GetTreatment(F1StudyDirection direction) => Treatments[direction];

        public static IReadOnlyList<F1StudyDirection> OrderedDirections { get; } = new[]
        {
            F1StudyDirection.HearthwoodCauseway,
            F1StudyDirection.ReedKilnWetlands,
            F1StudyDirection.PaintedSluiceToyworks
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

        public static F1DirectionSurfaceLayout CalculateDirectionSurfaceLayout(F1StudyDirection direction, float width, float height)
        {
            F1StudyLayout layout = CalculateLayout(width, height);
            float maximumWidth = width - layout.Margin * 2.0f;
            return direction switch
            {
                F1StudyDirection.HearthwoodCauseway => new F1DirectionSurfaceLayout(
                    MathF.Min(layout.SurfaceWidth, maximumWidth), layout.SurfaceHeight, false),
                F1StudyDirection.ReedKilnWetlands => new F1DirectionSurfaceLayout(
                    MathF.Min(layout.IsCompact ? 520.0f : 660.0f, maximumWidth), layout.SurfaceHeight + 34.0f, true),
                F1StudyDirection.PaintedSluiceToyworks => new F1DirectionSurfaceLayout(
                    MathF.Min(layout.IsCompact ? 560.0f : 780.0f, maximumWidth), layout.SurfaceHeight + 34.0f, true),
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown F1 direction.")
            };
        }

        /// <summary>Shared by the mesh factory so translucent tabletop-water accents are never silently rendered opaque.</summary>
        public static bool ShouldUseAlphaTransparency(float alpha) => alpha < 0.999f;

        /// <summary>Pure material policy for unit tests; Godot resources are constructed only by the running engine.</summary>
        public static F1StudyMaterialProfile GetMaterialProfile(float alpha) =>
            new(0.93f, 0.0f, ShouldUseAlphaTransparency(alpha));

        /// <summary>Opaque pressed-state pairs with WCAG normal-text contrast at or above 4.5:1.</summary>
        public static F1PressedControlColors GetPressedControlColors(F1StudyDirection direction) => direction switch
        {
            F1StudyDirection.HearthwoodCauseway => new F1PressedControlColors("3A241B", "FFF3DB"),
            F1StudyDirection.ReedKilnWetlands => new F1PressedControlColors("30251F", "FFF1D0"),
            F1StudyDirection.PaintedSluiceToyworks => new F1PressedControlColors("16303A", "F4F7E9"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown F1 direction.")
        };

        /// <summary>Contrast-checked colors used directly by the world-space carved control pieces.</summary>
        public static F1PhysicalControlColors GetPhysicalControlColors(F1StudyDirection direction) => direction switch
        {
            F1StudyDirection.HearthwoodCauseway => new F1PhysicalControlColors("352119", "F7E5C5", "6A3B25", "FFF3DB", "3A241B", "FFF3DB"),
            F1StudyDirection.ReedKilnWetlands => new F1PhysicalControlColors("30251F", "FFF0CF", "554235", "FFF1D0", "30251F", "FFF1D0"),
            F1StudyDirection.PaintedSluiceToyworks => new F1PhysicalControlColors("16303A", "F4F7E9", "315666", "F4F7E9", "16303A", "F4F7E9"),
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
