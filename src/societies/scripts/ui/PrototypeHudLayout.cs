using Societies.Simulation;
using System;
using System.Collections.Generic;

namespace Societies.UI
{
    /// <summary>
    /// Deterministic screen-space rules for the prototype HUD. Keeping these rules pure lets
    /// automated tests prove that normal-play cards remain readable at target resolutions.
    /// </summary>
    public readonly record struct PrototypeHudBounds(float X, float Y, float Width, float Height)
    {
        public float Right => X + Width;

        public float Bottom => Y + Height;

        public bool FitsWithin(float viewportWidth, float viewportHeight) =>
            X >= 0.0f && Y >= 0.0f && Right <= viewportWidth && Bottom <= viewportHeight;

        public bool Overlaps(PrototypeHudBounds other) =>
            X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
    }

    /// <summary>
    /// Font-independent, conservative text-fit estimate for HUD proof tests. It reserves the
    /// card padding and assumes 0.70em average glyph width and 1.35em line height.
    /// </summary>
    public readonly record struct PrototypeHudTextBudget(int EstimatedRenderedLines, int AvailableLines)
    {
        public bool Fits => EstimatedRenderedLines <= AvailableLines;
    }

    public sealed class PrototypeHudLayout
    {
        public const string Crisis = "Crisis";
        public const string Inspector = "Inspector";
        public const string World = "World";
        public const string Inventory = "Inventory";
        public const string Settlement = "Settlement";
        public const string Interaction = "Interaction";
        public const string Status = "Status";
        public const string Help = "Help";
        public const string Debug = "Debug";
        public const string Crosshair = "Crosshair";

        private PrototypeHudLayout(float viewportWidth, float viewportHeight, Dictionary<string, PrototypeHudBounds> bounds)
        {
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            Bounds = bounds;
        }

        public float ViewportWidth { get; }

        public float ViewportHeight { get; }

        public bool IsCompact => ViewportHeight <= 800.0f;

        public IReadOnlyDictionary<string, PrototypeHudBounds> Bounds { get; }

        public PrototypeHudBounds this[string key] => Bounds[key];

        public PrototypeHudTextBudget GetTextBudget(string key, string text, int fontSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(text);
            if (fontSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize));
            }

            PrototypeHudBounds bounds = this[key];
            float innerWidth = MathF.Max(1.0f, bounds.Width - 24.0f);
            float innerHeight = MathF.Max(1.0f, bounds.Height - 20.0f);
            int charactersPerLine = Math.Max(1, (int)MathF.Floor(innerWidth / (fontSize * 0.70f)));
            int availableLines = Math.Max(0, (int)MathF.Floor(innerHeight / (fontSize * 1.35f)));
            int estimatedLines = EstimateWrappedLines(text, charactersPerLine);
            return new PrototypeHudTextBudget(estimatedLines, availableLines);
        }

        public static PrototypeHudLayout Calculate(float viewportWidth, float viewportHeight)
        {
            float width = MathF.Max(1.0f, viewportWidth);
            float height = MathF.Max(1.0f, viewportHeight);
            bool compact = height <= 800.0f;
            float margin = Clamp(width * 0.012f, 14.0f, 24.0f);
            float gap = compact ? 12.0f : 16.0f;
            float leftWidth = Clamp(width * 0.34f, 435.0f, 540.0f);
            float rightWidth = Clamp(width * 0.26f, 320.0f, 380.0f);
            float crisisHeight = compact ? 224.0f : 250.0f;
            float inspectorHeight = compact ? 190.0f : 230.0f;
            // Compact world text retains two conservative 15px lines (50px inner height),
            // freeing enough vertical space for the two 18px one-line feedback cards.
            float worldHeight = compact ? 70.0f : 150.0f;
            float inventoryHeight = compact ? 226.0f : 292.0f;
            // The 320px compact card aggregates citizens, but still needs room for its
            // directive, needs, stockpile, and active build-queue reading without clipping.
            float settlementHeight = compact ? 310.0f : 250.0f;
            float helpHeight = compact ? 64.0f : 96.0f;
            // Both feedback cards render 18px text with 10px vertical padding. Reserve one
            // complete conservative line at both target resolutions.
            float statusHeight = 46.0f;
            float interactionHeight = 46.0f;

            float rightX = width - margin - rightWidth;
            float helpY = height - margin - helpHeight;
            float statusY = helpY - gap - statusHeight;
            float interactionY = statusY - gap - interactionHeight;
            float debugWidth = Clamp(width * 0.19f, 240.0f, 340.0f);
            float debugHeight = compact ? 100.0f : 124.0f;

            Dictionary<string, PrototypeHudBounds> bounds = new(StringComparer.Ordinal)
            {
                [Crisis] = new(margin, margin, leftWidth, crisisHeight),
                [Inspector] = new(margin, margin + crisisHeight + gap, leftWidth, inspectorHeight),
                [World] = new(margin, margin + crisisHeight + gap + inspectorHeight + gap, leftWidth, worldHeight),
                [Inventory] = new(rightX, margin, rightWidth, inventoryHeight),
                [Settlement] = new(rightX, margin + inventoryHeight + gap, rightWidth, settlementHeight),
                [Interaction] = new((width - 560.0f) * 0.5f, interactionY, 560.0f, interactionHeight),
                [Status] = new((width - 560.0f) * 0.5f, statusY, 560.0f, statusHeight),
                [Help] = new(margin, helpY, width - (margin * 2.0f), helpHeight),
                [Debug] = new((width - debugWidth) * 0.5f, margin, debugWidth, debugHeight),
                [Crosshair] = new((width - 24.0f) * 0.5f, (height - 24.0f) * 0.5f, 24.0f, 24.0f)
            };

            return new PrototypeHudLayout(width, height, bounds);
        }

        public bool HasOverlaps()
        {
            KeyValuePair<string, PrototypeHudBounds>[] cards = new KeyValuePair<string, PrototypeHudBounds>[Bounds.Count];
            int index = 0;
            foreach (KeyValuePair<string, PrototypeHudBounds> card in Bounds)
            {
                cards[index++] = card;
            }
            for (int left = 0; left < cards.Length; left++)
            {
                for (int right = left + 1; right < cards.Length; right++)
                {
                    if (cards[left].Key != Crosshair && cards[right].Key != Crosshair && cards[left].Value.Overlaps(cards[right].Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float Clamp(float value, float min, float max) => MathF.Min(MathF.Max(value, min), max);

        private static int EstimateWrappedLines(string text, int charactersPerLine)
        {
            int lines = 0;
            string[] sourceLines = text.Split('\n');
            foreach (string sourceLine in sourceLines)
            {
                float units = 0.0f;
                foreach (char character in sourceLine)
                {
                    units += character switch
                    {
                        'M' or 'W' or '@' or '%' => 1.25f,
                        'i' or 'l' or '!' or '.' or ',' or ':' or ';' or ' ' => 0.65f,
                        _ => 1.0f
                    };
                }

                lines += Math.Max(1, (int)MathF.Ceiling(units / charactersPerLine));
            }

            return lines;
        }
    }

    public enum PrototypeHudCue
    {
        Neutral,
        FoodAndFuel,
        Shelter,
        Stable,
        Collapsed,
        BlockedInteraction,
        ContributionSuccess
    }

    /// <summary>Pure state-to-cue mapping used by the live HUD and focused tests.</summary>
    public readonly record struct PrototypeHudPresentationState(
        PrototypeHudCue DirectiveCue,
        PrototypeHudCue SettlementCue,
        PrototypeHudCue InteractionCue)
    {
        public static PrototypeHudPresentationState Create(
            PrototypeSettlementDirective directive,
            PrototypeSettlementClassification classification,
            PrototypeCrisisState? crisis,
            string? statusText,
            string? interactionText)
        {
            PrototypeHudCue directiveCue = directive switch
            {
                PrototypeSettlementDirective.FoodAndFuel => PrototypeHudCue.FoodAndFuel,
                PrototypeSettlementDirective.Shelter => PrototypeHudCue.Shelter,
                _ => PrototypeHudCue.Neutral
            };
            PrototypeHudCue settlementCue = crisis?.Outcome switch
            {
                PrototypeCrisisOutcome.Stable => PrototypeHudCue.Stable,
                PrototypeCrisisOutcome.Collapsed => PrototypeHudCue.Collapsed,
                _ when classification == PrototypeSettlementClassification.Collapsed => PrototypeHudCue.Collapsed,
                _ when classification == PrototypeSettlementClassification.Stable => PrototypeHudCue.Stable,
                _ => directiveCue
            };
            string feedback = $"{statusText} {interactionText}";
            PrototypeHudCue interactionCue = Contains(feedback, "contributed")
                ? PrototypeHudCue.ContributionSuccess
                : Contains(feedback, "unavailable") || Contains(feedback, "cannot") ||
                  Contains(feedback, "rejected") || Contains(feedback, "no eligible") ||
                  Contains(feedback, "no resources to contribute") || Contains(feedback, "nothing to contribute") ||
                  Contains(feedback, "move closer") || Contains(feedback, "out of range")
                    ? PrototypeHudCue.BlockedInteraction
                    : directiveCue;
            return new PrototypeHudPresentationState(directiveCue, settlementCue, interactionCue);
        }

        private static bool Contains(string value, string fragment) =>
            value.Contains(fragment, StringComparison.OrdinalIgnoreCase);
    }
}
