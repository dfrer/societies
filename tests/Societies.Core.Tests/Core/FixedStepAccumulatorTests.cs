using Societies.Core;
using Xunit;

namespace Societies.Core.Tests
{
    public class FixedStepAccumulatorTests
    {
        private const double TickIntervalSeconds = 1.0 / 20.0;

        [Fact]
        public void Constructor_RejectsInvalidConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepAccumulator(0.0, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepAccumulator(double.NaN, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepAccumulator(TickIntervalSeconds, 0));
        }

        [Fact]
        public void Consume_WhenTwentyTicksAreDue_ProcessesTwelveThenEightWithoutLosingTime()
        {
            var accumulator = new FixedStepAccumulator(TickIntervalSeconds, maxTicksPerFrame: 12);

            int firstFrameTicks = accumulator.Consume(1.0);

            Assert.Equal(12, firstFrameTicks);
            Assert.Equal(8L, accumulator.PendingWholeTicks);
            Assert.True(accumulator.HasBacklog);

            int secondFrameTicks = accumulator.Consume(0.0);

            Assert.Equal(8, secondFrameTicks);
            Assert.Equal(0L, accumulator.PendingWholeTicks);
            Assert.False(accumulator.HasBacklog);
            Assert.Equal(0.0, accumulator.AccumulatedSeconds, precision: 9);
        }

        [Fact]
        public void Consume_PreservesFractionalElapsedTimeAcrossFrames()
        {
            var accumulator = new FixedStepAccumulator(TickIntervalSeconds, maxTicksPerFrame: 12);

            Assert.Equal(0, accumulator.Consume(0.02));
            Assert.Equal(0, accumulator.Consume(0.02));
            Assert.Equal(1, accumulator.Consume(0.01));
            Assert.Equal(0.0, accumulator.AccumulatedSeconds, precision: 9);
        }

        [Fact]
        public void Reset_ClearsFractionAndBacklog()
        {
            var accumulator = new FixedStepAccumulator(TickIntervalSeconds, maxTicksPerFrame: 12);
            Assert.Equal(12, accumulator.Consume(0.76));
            Assert.True(accumulator.HasBacklog);

            accumulator.Reset();

            Assert.Equal(0.0, accumulator.AccumulatedSeconds);
            Assert.Equal(0L, accumulator.PendingWholeTicks);
            Assert.Equal(0, accumulator.Consume(0.0));
        }

        [Fact]
        public void Consume_RejectsInvalidElapsedTime()
        {
            var accumulator = new FixedStepAccumulator(TickIntervalSeconds, maxTicksPerFrame: 12);

            Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Consume(-0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Consume(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Consume(double.PositiveInfinity));
        }

        [Fact]
        public void RestoreUnprocessedTicks_PreservesEveryTickThatDidNotStart()
        {
            var accumulator = new FixedStepAccumulator(TickIntervalSeconds, maxTicksPerFrame: 12);
            int reservedTicks = accumulator.Consume(1.0);

            int attemptedTicks = 1;
            accumulator.RestoreUnprocessedTicks(reservedTicks - attemptedTicks);

            Assert.Equal(19L, accumulator.PendingWholeTicks);
            Assert.Equal(12, accumulator.Consume(0.0));
            Assert.Equal(7L, accumulator.PendingWholeTicks);
        }
    }
}
