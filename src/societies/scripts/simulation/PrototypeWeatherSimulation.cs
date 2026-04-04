using Societies.Core;

namespace Societies.Simulation
{
    /// <summary>
    /// Deterministic prototype weather simulation.
    /// </summary>
    public static class PrototypeWeatherService
    {
        public static string GetName(PrototypeWeather weather)
        {
            return weather == PrototypeWeather.Clear ? "Clear" : "Rain";
        }

        public static float GetSunlightMultiplier(PrototypeWeather weather)
        {
            return weather == PrototypeWeather.Clear ? 1.0f : 0.72f;
        }

        public static PrototypeWeather Toggle(PrototypeWeather currentWeather)
        {
            return currentWeather == PrototypeWeather.Clear
                ? PrototypeWeather.Rain
                : PrototypeWeather.Clear;
        }

        public static float GetNextShiftDelay(DeterministicRandom rng)
        {
            return rng.NextFloat(40.0f, 90.0f);
        }
    }

    public sealed class PrototypeWeatherSimulation
    {
        private readonly DeterministicRandom _rng;

        public PrototypeWeatherSimulation(int seed, PrototypeWeather startingWeather = PrototypeWeather.Clear)
        {
            _rng = new DeterministicRandom(seed);
            CurrentWeather = startingWeather;
            TimeUntilNextShift = PrototypeWeatherService.GetNextShiftDelay(_rng);
        }

        public PrototypeWeather CurrentWeather { get; private set; }

        public float TimeUntilNextShift { get; private set; }

        public uint RandomState => _rng.State;

        public bool Advance(float deltaSeconds)
        {
            bool changed = false;
            TimeUntilNextShift -= deltaSeconds;

            while (TimeUntilNextShift <= 0.0f)
            {
                CurrentWeather = PrototypeWeatherService.Toggle(CurrentWeather);
                TimeUntilNextShift += PrototypeWeatherService.GetNextShiftDelay(_rng);
                changed = true;
            }

            return changed;
        }

        public void ToggleWeather()
        {
            CurrentWeather = PrototypeWeatherService.Toggle(CurrentWeather);
            TimeUntilNextShift = PrototypeWeatherService.GetNextShiftDelay(_rng);
        }

        public void SetState(PrototypeWeather weather, float timeUntilNextShift, uint randomState)
        {
            CurrentWeather = weather;
            TimeUntilNextShift = timeUntilNextShift;
            _rng.SetState(randomState);
        }
    }
}
