using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeClockServiceTests
    {
        [Fact]
        public void AdvanceHour_WrapsAroundTwentyFourHours()
        {
            float advanced = Societies.Simulation.PrototypeClockService.AdvanceHour(23.5f, 30.0f, 300.0f);

            Assert.InRange(advanced, 1.8f, 2.0f);
        }

        [Theory]
        [InlineData(0.0f, "00:00")]
        [InlineData(8.5f, "08:30")]
        [InlineData(23.99f, "23:59")]
        public void FormatTime_ReturnsExpectedClockText(float currentHour, string expected)
        {
            Assert.Equal(expected, Societies.Simulation.PrototypeClockService.FormatTime(currentHour));
        }

        [Fact]
        public void CalculateLighting_MiddayIsBrighterThanMidnight()
        {
            var midnight = Societies.Simulation.PrototypeClockService.CalculateLighting(0.0f, 1.0f);
            var midday = Societies.Simulation.PrototypeClockService.CalculateLighting(12.0f, 1.0f);

            Assert.True(midday.SunEnergy > midnight.SunEnergy);
        }
    }
}
