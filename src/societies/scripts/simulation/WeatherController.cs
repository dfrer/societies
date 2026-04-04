using Godot;

namespace Societies.Simulation
{
    /// <summary>
    /// Lightweight weather state machine for Prototype 1.
    /// </summary>
    public partial class WeatherController : Node
    {
        public PrototypeWeather CurrentWeather { get; private set; } = PrototypeWeather.Clear;
        public float TimeUntilNextShift { get; private set; }
        public string CurrentWeatherName => PrototypeWeatherService.GetName(CurrentWeather);
        public float SunlightMultiplier => PrototypeWeatherService.GetSunlightMultiplier(CurrentWeather);

        public override void _Ready()
        {
            ApplyState(PrototypeWeather.Clear, 45.0f);
        }

        public void ApplyState(PrototypeWeather weather, float timeUntilNextShift)
        {
            CurrentWeather = weather;
            TimeUntilNextShift = timeUntilNextShift;
        }
    }

    public enum PrototypeWeather
    {
        Clear,
        Rain
    }
}
