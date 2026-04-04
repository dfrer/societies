using Godot;

namespace Societies.Simulation
{
    /// <summary>
    /// Pure time-of-day math for Prototype 1.
    /// </summary>
    public static class PrototypeClockService
    {
        public static float AdvanceHour(float currentHour, float deltaSeconds, float dayLengthSeconds)
        {
            float safeDayLength = Mathf.Max(dayLengthSeconds, 1.0f);
            float hoursPerSecond = 24.0f / safeDayLength;
            return Mathf.PosMod(currentHour + (deltaSeconds * hoursPerSecond), 24.0f);
        }

        public static string FormatTime(float currentHour)
        {
            int hours = Mathf.FloorToInt(currentHour);
            int minutes = Mathf.FloorToInt((currentHour - hours) * 60.0f);
            return $"{hours:00}:{minutes:00}";
        }

        public static PrototypeLightingState CalculateLighting(float currentHour, float weatherLightMultiplier)
        {
            float normalized = currentHour / 24.0f;
            float sunPhase = Mathf.Sin((normalized * Mathf.Tau) - (Mathf.Pi * 0.5f));
            float daylight = Mathf.Clamp((sunPhase + 1.0f) * 0.5f, 0.08f, 1.0f);

            return new PrototypeLightingState(
                new Vector3((normalized * 360.0f) - 90.0f, -35.0f, 0.0f),
                Mathf.Lerp(0.15f, 1.25f, daylight) * weatherLightMultiplier,
                new Color(1.0f, 0.9f, 0.78f).Lerp(new Color(0.56f, 0.63f, 0.92f), 1.0f - daylight),
                new Color(0.04f, 0.07f, 0.18f).Lerp(new Color(0.5f, 0.74f, 0.94f), daylight));
        }
    }

    public readonly record struct PrototypeLightingState(
        Vector3 SunRotationDegrees,
        float SunEnergy,
        Color SunColor,
        Color ClearColor);
}
