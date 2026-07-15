using Godot;

namespace Societies.Simulation
{
    /// <summary>
    /// Combined environment controller handling lighting, day/night cycle, and weather state for Prototype 1.
    /// Merged from DayNightCycle and WeatherController.
    /// </summary>
    public partial class EnvironmentController : Node3D
    {
        // Day/night cycle settings
        [Export] public float DayLengthSeconds { get; set; } = 300.0f;
        [Export] public float StartHour { get; set; } = 8.0f;

        // Weather state
        public PrototypeWeather CurrentWeather { get; private set; } = PrototypeWeather.Clear;
        public float TimeUntilNextWeatherShift { get; private set; }

        // Derived properties
        public string CurrentWeatherName => PrototypeWeatherService.GetName(CurrentWeather);
        public float WeatherSunlightMultiplier => PrototypeWeatherService.GetSunlightMultiplier(CurrentWeather);
        public float CurrentHour { get; private set; }
        public bool IsPresentationLightingLocked => _presentationLightingHour.HasValue;
        public float? PresentationLightingHour => _presentationLightingHour;
        public float? PresentationLightingMultiplier => _presentationLightingMultiplier;

        private DirectionalLight3D? _sunLight;
        private float _weatherLightMultiplier = 1.0f;
        private float? _presentationLightingHour;
        private float? _presentationLightingMultiplier;

        public override void _Ready()
        {
            CurrentHour = StartHour;
            CurrentWeather = PrototypeWeather.Clear;
            TimeUntilNextWeatherShift = 45.0f;

            _sunLight = new DirectionalLight3D
            {
                Name = "SunLight",
                LightEnergy = 1.2f,
                ShadowEnabled = true,
                Position = new Vector3(0.0f, 40.0f, 0.0f)
            };
            AddChild(_sunLight);

            UpdateLighting();
        }

        public void ApplyWeatherState(PrototypeWeather weather, float timeUntilNextShift)
        {
            CurrentWeather = weather;
            TimeUntilNextWeatherShift = timeUntilNextShift;
            UpdateLighting();
        }

        public void SetWeatherLightMultiplier(float multiplier)
        {
            _weatherLightMultiplier = multiplier;
            UpdateLighting();
        }

        public void ApplyState(float currentHour, float weatherLightMultiplier)
        {
            CurrentHour = currentHour;
            _weatherLightMultiplier = weatherLightMultiplier;
            UpdateLighting();
        }

        /// <summary>Locks only rendering lighting; simulation time and weather remain authoritative.</summary>
        public void SetPresentationLighting(float hour, float multiplier)
        {
            _presentationLightingHour = Mathf.PosMod(hour, 24.0f);
            _presentationLightingMultiplier = Mathf.Max(0.0f, multiplier);
            UpdateLighting();
        }

        public void ClearPresentationLighting()
        {
            _presentationLightingHour = null;
            _presentationLightingMultiplier = null;
            UpdateLighting();
        }

        public string GetTimeText()
        {
            return FormatTime(CurrentHour);
        }

        private void UpdateLighting()
        {
            PrototypeLightingState lighting = CalculateLightingState(
                _presentationLightingHour ?? CurrentHour,
                _presentationLightingMultiplier ?? _weatherLightMultiplier);

            if (_sunLight != null)
            {
                _sunLight.RotationDegrees = lighting.SunRotationDegrees;
                _sunLight.LightEnergy = lighting.SunEnergy;
                _sunLight.LightColor = lighting.SunColor;
            }

            RenderingServer.SetDefaultClearColor(lighting.ClearColor);
        }

        private static string FormatTime(float currentHour)
        {
            int hours = Mathf.FloorToInt(currentHour);
            int minutes = Mathf.FloorToInt((currentHour - hours) * 60.0f);
            return $"{hours:00}:{minutes:00}";
        }

        private static PrototypeLightingState CalculateLightingState(float currentHour, float weatherLightMultiplier)
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

    public enum PrototypeWeather
    {
        Clear,
        Rain
    }
}
