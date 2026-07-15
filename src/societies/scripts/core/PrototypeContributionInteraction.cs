using Godot;
using Societies.Simulation;
using System;

namespace Societies.Core
{
    /// <summary>
    /// Deterministic scene-boundary guard for central-depot contribution input.
    /// </summary>
    public sealed class PrototypeContributionInteraction
    {
        private ulong? _lastInputFrame;

        public PrototypeContributionBatchResult Execute(
            PrototypeRuntimeSession? session,
            Vector3 playerPosition,
            float interactionRangeMeters,
            ulong inputFrame)
        {
            if (_lastInputFrame == inputFrame)
            {
                return Rejected("duplicate_input");
            }

            _lastInputFrame = inputFrame;
            if (session == null)
            {
                return Rejected("runtime_unavailable");
            }

            if (!IsFinite(playerPosition) || !float.IsFinite(interactionRangeMeters) || interactionRangeMeters <= 0.0f ||
                playerPosition.DistanceTo(session.CentralDepotPosition) > interactionRangeMeters)
            {
                return Rejected("out_of_range");
            }

            return session.ContributeAllEligibleToStockpile();
        }

        public void Reset()
        {
            _lastInputFrame = null;
        }

        private static bool IsFinite(Vector3 position) =>
            float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);

        private static PrototypeContributionBatchResult Rejected(string reason) =>
            new(Array.Empty<PrototypeContributionResult>(), false, reason);
    }
}
