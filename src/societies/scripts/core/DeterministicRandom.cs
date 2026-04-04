using System;

namespace Societies.Core
{
    /// <summary>
    /// Small deterministic RNG used for prototype simulation and tests.
    /// It avoids engine randomness so seeded runs stay stable across unit tests and runtime code.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            _state = seed == 0 ? 0x6D2B79F5u : unchecked((uint)seed);
        }

        public uint State => _state;

        public int NextIntInclusive(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive), "Maximum must be greater than or equal to minimum.");
            }

            uint range = (uint)(maxInclusive - minInclusive + 1);
            return minInclusive + (int)(NextUInt() % range);
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            if (maxExclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must be greater than or equal to minimum.");
            }

            float normalized = NextUInt() / (float)uint.MaxValue;
            return minInclusive + ((maxExclusive - minInclusive) * normalized);
        }

        public void SetState(uint state)
        {
            _state = state == 0 ? 0x6D2B79F5u : state;
        }

        private uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }
}
