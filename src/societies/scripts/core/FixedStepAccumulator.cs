using System;

namespace Societies.Core
{
    public static class PrototypeSimulationTime
    {
        public const int TicksPerSecond = 20;

        public const double TickIntervalSeconds = 1.0 / TicksPerSecond;
    }

    /// <summary>
    /// Converts elapsed frame time into a bounded number of fixed simulation steps while
    /// retaining any unprocessed backlog for later frames.
    /// </summary>
    public sealed class FixedStepAccumulator
    {
        private readonly double _tickIntervalSeconds;
        private readonly double _comparisonEpsilon;
        private readonly int _maxTicksPerFrame;
        private double _accumulatedSeconds;

        public FixedStepAccumulator(double tickIntervalSeconds, int maxTicksPerFrame)
        {
            if (!double.IsFinite(tickIntervalSeconds) || tickIntervalSeconds <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickIntervalSeconds), "Tick interval must be finite and positive.");
            }

            if (maxTicksPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerFrame), "Frame tick cap must be positive.");
            }

            _tickIntervalSeconds = tickIntervalSeconds;
            _comparisonEpsilon = tickIntervalSeconds * 1e-9;
            _maxTicksPerFrame = maxTicksPerFrame;
        }

        public double AccumulatedSeconds => _accumulatedSeconds;

        public long PendingWholeTicks
        {
            get
            {
                double pending = Math.Floor((_accumulatedSeconds + _comparisonEpsilon) / _tickIntervalSeconds);
                if (pending <= 0.0)
                {
                    return 0;
                }

                return pending >= long.MaxValue ? long.MaxValue : (long)pending;
            }
        }

        public bool HasBacklog => PendingWholeTicks > 0;

        public int Consume(double elapsedSeconds)
        {
            if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time must be finite and non-negative.");
            }

            double nextAccumulatedSeconds = _accumulatedSeconds + elapsedSeconds;
            if (!double.IsFinite(nextAccumulatedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time would overflow the accumulator.");
            }

            _accumulatedSeconds = nextAccumulatedSeconds;

            int ticksToProcess = 0;
            while (ticksToProcess < _maxTicksPerFrame &&
                   _accumulatedSeconds + _comparisonEpsilon >= _tickIntervalSeconds)
            {
                _accumulatedSeconds -= _tickIntervalSeconds;
                ticksToProcess++;
            }

            if (_accumulatedSeconds < 0.0 && _accumulatedSeconds > -_comparisonEpsilon)
            {
                _accumulatedSeconds = 0.0;
            }

            return ticksToProcess;
        }

        /// <summary>
        /// Returns reserved ticks that were not attempted because a batch stopped early.
        /// The interval for a tick that began execution remains consumed.
        /// </summary>
        public void RestoreUnprocessedTicks(int tickCount)
        {
            if (tickCount < 0 || tickCount > _maxTicksPerFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(tickCount), "Restored tick count must fit within one reserved frame batch.");
            }

            _accumulatedSeconds += tickCount * _tickIntervalSeconds;
        }

        public void Reset()
        {
            _accumulatedSeconds = 0.0;
        }
    }
}
