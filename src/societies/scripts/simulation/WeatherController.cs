using Godot;

namespace Societies.Simulation
{
    /// <summary>
    /// Lightweight weather state machine for Prototype 1.
    /// </summary>
    public partial class WeatherController : Node
    {
        private readonly RandomNumberGenerator _rng = new();
        private float _timeUntilNextShift = 45.0f;

        public PrototypeWeather CurrentWeather { get; private set; } = PrototypeWeather.Clear;
        public string CurrentWeatherName => CurrentWeather == PrototypeWeather.Clear ? "Clear" : "Rain";
        public float SunlightMultiplier => CurrentWeather == PrototypeWeather.Clear ? 1.0f : 0.72f;

        public override void _Ready()
        {
            _rng.Randomize();
            QueueNextShift();
        }

        public override void _Process(double delta)
        {
            _timeUntilNextShift -= (float)delta;
            if (_timeUntilNextShift <= 0.0f)
            {
                CurrentWeather = CurrentWeather == PrototypeWeather.Clear
                    ? PrototypeWeather.Rain
                    : PrototypeWeather.Clear;
                QueueNextShift();
            }
        }

        public void ToggleWeather()
        {
            CurrentWeather = CurrentWeather == PrototypeWeather.Clear
                ? PrototypeWeather.Rain
                : PrototypeWeather.Clear;
            QueueNextShift();
        }

        private void QueueNextShift()
        {
            _timeUntilNextShift = _rng.RandfRange(40.0f, 90.0f);
        }
    }

    public enum PrototypeWeather
    {
        Clear,
        Rain
    }
}
