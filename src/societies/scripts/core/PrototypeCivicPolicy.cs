using System;

namespace Societies.Core
{
    /// <summary>
    /// The single bounded civic policy choice owned by a prototype runtime session.
    /// </summary>
    public enum PrototypeCivicPolicy
    {
        Neutral = 0,
        ProtectWetland = 1,
        DrawDownWetland = 2
    }

    public static class PrototypeCivicPolicyCatalog
    {
        public const long SelectionWindowStartTick = 0;
        public const long SelectionWindowEndTick = 1200;

        public static string GetId(PrototypeCivicPolicy policy)
        {
            return policy switch
            {
                PrototypeCivicPolicy.Neutral => "neutral",
                PrototypeCivicPolicy.ProtectWetland => "protect_wetland",
                PrototypeCivicPolicy.DrawDownWetland => "draw_down_wetland",
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown civic policy.")
            };
        }

        public static PrototypeCivicPolicy ParseId(string policyId)
        {
            return policyId switch
            {
                "neutral" => PrototypeCivicPolicy.Neutral,
                "protect_wetland" => PrototypeCivicPolicy.ProtectWetland,
                "draw_down_wetland" => PrototypeCivicPolicy.DrawDownWetland,
                _ => throw new ArgumentException($"Unknown civic policy id '{policyId}'.", nameof(policyId))
            };
        }

        public static string BuildSelectionMessage(PrototypeCivicPolicy policy)
        {
            return $"Civic policy selected: {GetId(policy)}";
        }
    }

    public readonly record struct PrototypeCivicPolicyCommand(
        PrototypeCivicPolicy RequestedPolicy,
        int ExpectedVersion,
        long IssuedTick);

    public readonly record struct PrototypeCivicPolicyCommandResult(
        bool Succeeded,
        string FailureReason,
        PrototypeCivicPolicySnapshot State);

    public sealed class PrototypeCivicPolicySnapshot
    {
        public string PolicyId { get; set; } = "neutral";

        public long? SelectedTick { get; set; }

        public int Version { get; set; }

        public long WindowStartTick { get; set; } = PrototypeCivicPolicyCatalog.SelectionWindowStartTick;

        public long WindowEndTick { get; set; } = PrototypeCivicPolicyCatalog.SelectionWindowEndTick;
    }

    internal sealed class PrototypeCivicPolicyState
    {
        public PrototypeCivicPolicy Policy { get; private set; } = PrototypeCivicPolicy.Neutral;

        public long? SelectedTick { get; private set; }

        public int Version { get; private set; }

        public PrototypeCivicPolicyCommandResult TrySelect(
            PrototypeCivicPolicyCommand command,
            long simulationTick)
        {
            string failureReason = Validate(command, simulationTick);
            if (failureReason.Length != 0)
            {
                return new PrototypeCivicPolicyCommandResult(false, failureReason, CaptureSnapshot());
            }

            Policy = command.RequestedPolicy;
            SelectedTick = simulationTick;
            Version = 1;
            return new PrototypeCivicPolicyCommandResult(true, string.Empty, CaptureSnapshot());
        }

        public PrototypeCivicPolicySnapshot CaptureSnapshot()
        {
            return new PrototypeCivicPolicySnapshot
            {
                PolicyId = PrototypeCivicPolicyCatalog.GetId(Policy),
                SelectedTick = SelectedTick,
                Version = Version,
                WindowStartTick = PrototypeCivicPolicyCatalog.SelectionWindowStartTick,
                WindowEndTick = PrototypeCivicPolicyCatalog.SelectionWindowEndTick
            };
        }

        public static PrototypeCivicPolicyState PrepareRestore(PrototypeCivicPolicySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            PrototypeCivicPolicy policy;
            try
            {
                policy = PrototypeCivicPolicyCatalog.ParseId(snapshot.PolicyId);
            }
            catch (ArgumentException exception)
            {
                throw new System.IO.InvalidDataException("Runtime snapshot civic policy id is invalid.", exception);
            }

            if (snapshot.WindowStartTick != PrototypeCivicPolicyCatalog.SelectionWindowStartTick ||
                snapshot.WindowEndTick != PrototypeCivicPolicyCatalog.SelectionWindowEndTick)
            {
                throw new System.IO.InvalidDataException("Runtime snapshot civic policy window does not match the session contract.");
            }

            bool neutral = policy == PrototypeCivicPolicy.Neutral;
            if ((neutral && (snapshot.SelectedTick != null || snapshot.Version != 0)) ||
                (!neutral && (snapshot.SelectedTick == null || snapshot.Version != 1)) ||
                snapshot.SelectedTick is < PrototypeCivicPolicyCatalog.SelectionWindowStartTick or > PrototypeCivicPolicyCatalog.SelectionWindowEndTick)
            {
                throw new System.IO.InvalidDataException("Runtime snapshot civic policy state is inconsistent.");
            }

            return new PrototypeCivicPolicyState
            {
                Policy = policy,
                SelectedTick = snapshot.SelectedTick,
                Version = snapshot.Version
            };
        }

        private string Validate(PrototypeCivicPolicyCommand command, long simulationTick)
        {
            if (!Enum.IsDefined(typeof(PrototypeCivicPolicy), command.RequestedPolicy))
            {
                return "invalid_policy";
            }

            if (command.RequestedPolicy == PrototypeCivicPolicy.Neutral)
            {
                return "neutral_policy";
            }

            if (command.IssuedTick != simulationTick)
            {
                return "stale_tick";
            }

            if (command.ExpectedVersion != Version)
            {
                return "stale_version";
            }

            if (simulationTick < PrototypeCivicPolicyCatalog.SelectionWindowStartTick ||
                simulationTick > PrototypeCivicPolicyCatalog.SelectionWindowEndTick)
            {
                return "outside_selection_window";
            }

            if (Policy != PrototypeCivicPolicy.Neutral)
            {
                return "already_selected";
            }

            return string.Empty;
        }
    }
}
