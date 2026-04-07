using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeClockServiceTests
    {
        [Fact]
        public void AdvanceHour_WrapsAroundTwentyFourHours()
        {
            float advanced = AdvanceHour(23.5f, 30.0f, 300.0f);

            Assert.InRange(advanced, 1.8f, 2.0f);
        }

        [Theory]
        [InlineData(0.0f, "00:00")]
        [InlineData(8.5f, "08:30")]
        [InlineData(23.99f, "23:59")]
        public void FormatTime_ReturnsExpectedClockText(float currentHour, string expected)
        {
            Assert.Equal(expected, FormatTime(currentHour));
        }

        [Fact]
        public void CalculateLighting_MiddayIsBrighterThanMidnight()
        {
            var midnight = CalculateLighting(0.0f, 1.0f);
            var midday = CalculateLighting(12.0f, 1.0f);

            Assert.True(midday.SunEnergy > midnight.SunEnergy);
        }

        private static float AdvanceHour(float currentHour, double tickIntervalSeconds, double dayLengthSeconds)
        {
            double hoursPerTick = 24.0 * tickIntervalSeconds / dayLengthSeconds;
            float next = (float)(currentHour + hoursPerTick);
            while (next >= 24.0f)
            {
                next -= 24.0f;
            }
            return next;
        }

        private static string FormatTime(float currentHour)
        {
            int hours = (int)System.Math.Floor(currentHour);
            int minutes = (int)System.Math.Round((currentHour - hours) * 60.0);

            if (minutes >= 60)
            {
                hours += 1;
                minutes = 0;
            }

            hours = hours % 24;
            return $"{hours:00}:{minutes:00}";
        }

        private static (float SunEnergy, float AmbientIntensity, float AmbientR, float AmbientG, float AmbientB) CalculateLighting(float currentHour, float weatherLightMultiplier)
        {
            float sunAngle = ((currentHour - 6.0f) / 12.0f) * System.MathF.PI;
            float rawSunEnergy = (float)System.Math.Max(0.0, System.Math.Sin(sunAngle));
            float sunEnergy = rawSunEnergy * weatherLightMultiplier;
            float ambientIntensity = 0.15f + 0.35f * rawSunEnergy * weatherLightMultiplier;

            float warmFactor = 1.0f - rawSunEnergy;
            float ambientR = (0.6f + 0.3f * warmFactor) * ambientIntensity;
            float ambientG = (0.6f + 0.2f * warmFactor) * ambientIntensity;
            float ambientB = (0.8f + 0.0f * warmFactor) * ambientIntensity;

            return (sunEnergy, ambientIntensity, ambientR, ambientG, ambientB);
        }
    }
}
