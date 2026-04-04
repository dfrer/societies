using Societies.Simulation;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeWeatherSimulationTests
    {
        [Fact]
        public void WeatherSimulation_SameSeed_ProducesSameShiftDelay()
        {
            PrototypeWeatherSimulation first = new(77);
            PrototypeWeatherSimulation second = new(77);

            Assert.Equal(first.TimeUntilNextShift, second.TimeUntilNextShift);
            Assert.Equal(first.RandomState, second.RandomState);
        }

        [Fact]
        public void Advance_WhenEnoughTimePasses_TogglesWeather()
        {
            PrototypeWeatherSimulation simulation = new(10, PrototypeWeather.Clear);
            simulation.SetState(PrototypeWeather.Clear, 0.01f, simulation.RandomState);

            bool changed = simulation.Advance(1.0f);

            Assert.True(changed);
            Assert.Equal(PrototypeWeather.Rain, simulation.CurrentWeather);
            Assert.InRange(simulation.TimeUntilNextShift, 39.0f, 90.0f);
        }

        [Fact]
        public void SetState_RestoresWeatherAndRandomState()
        {
            PrototypeWeatherSimulation original = new(99);
            original.ToggleWeather();
            uint savedRandomState = original.RandomState;
            float savedDelay = original.TimeUntilNextShift;

            PrototypeWeatherSimulation restored = new(99);
            restored.SetState(original.CurrentWeather, savedDelay, savedRandomState);

            Assert.Equal(original.CurrentWeather, restored.CurrentWeather);
            Assert.Equal(savedDelay, restored.TimeUntilNextShift);
            Assert.Equal(savedRandomState, restored.RandomState);
        }
    }
}
