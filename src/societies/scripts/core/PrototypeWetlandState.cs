using System;
using System.Globalization;
using System.IO;

namespace Societies.Core
{
    public enum PrototypeWetlandHealthBand
    {
        Degraded = 0,
        Strained = 1,
        Healthy = 2
    }

    public static class PrototypeWetlandCatalog
    {
        public const string ReedResourceId = "reeds";
        public const int MinimumHealth = 0;
        public const int MaximumHealth = 100;
        public const int NeutralHealth = 60;
        public const int HealthyThreshold = 70;
        public const int StrainedThreshold = 40;
        public const int ProtectReedQuotaLimit = 4;
        public const int DrawDownReedQuotaLimit = 12;
        public const int ProtectSelectionHealthDelta = 15;
        public const int DrawDownSelectionHealthDelta = -15;
        public const int ProtectHarvestHealthDelta = -1;
        public const int DrawDownHarvestHealthDelta = -2;

        public static PrototypeWetlandHealthBand GetHealthBand(int health)
        {
            if (health is < MinimumHealth or > MaximumHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health));
            }

            return health >= HealthyThreshold
                ? PrototypeWetlandHealthBand.Healthy
                : health >= StrainedThreshold
                    ? PrototypeWetlandHealthBand.Strained
                    : PrototypeWetlandHealthBand.Degraded;
        }

        public static string GetHealthBandId(PrototypeWetlandHealthBand band)
        {
            return band switch
            {
                PrototypeWetlandHealthBand.Degraded => "degraded",
                PrototypeWetlandHealthBand.Strained => "strained",
                PrototypeWetlandHealthBand.Healthy => "healthy",
                _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown wetland health band.")
            };
        }

        public static PrototypeWetlandHealthBand ParseHealthBandId(string bandId)
        {
            return bandId switch
            {
                "degraded" => PrototypeWetlandHealthBand.Degraded,
                "strained" => PrototypeWetlandHealthBand.Strained,
                "healthy" => PrototypeWetlandHealthBand.Healthy,
                _ => throw new InvalidDataException("Runtime snapshot wetland health band is invalid.")
            };
        }

        public static int GetQuotaLimit(PrototypeCivicPolicy policy)
        {
            return policy switch
            {
                PrototypeCivicPolicy.Neutral => 0,
                PrototypeCivicPolicy.ProtectWetland => ProtectReedQuotaLimit,
                PrototypeCivicPolicy.DrawDownWetland => DrawDownReedQuotaLimit,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown civic policy.")
            };
        }

        public static int GetSelectionHealth(PrototypeCivicPolicy policy)
        {
            return policy switch
            {
                PrototypeCivicPolicy.Neutral => NeutralHealth,
                PrototypeCivicPolicy.ProtectWetland => NeutralHealth + ProtectSelectionHealthDelta,
                PrototypeCivicPolicy.DrawDownWetland => NeutralHealth + DrawDownSelectionHealthDelta,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown civic policy.")
            };
        }

        public static int GetHarvestHealthDelta(PrototypeCivicPolicy policy)
        {
            return policy switch
            {
                PrototypeCivicPolicy.ProtectWetland => ProtectHarvestHealthDelta,
                PrototypeCivicPolicy.DrawDownWetland => DrawDownHarvestHealthDelta,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Selected policy is required for reed consequences.")
            };
        }

        public static string BuildQuotaAppliedMessage(PrototypeWetlandSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Wetland reed quota applied: policy={snapshot.PolicyId}; limit={snapshot.ReedQuotaLimit}; consumed={snapshot.ReedQuotaConsumed}; remaining={snapshot.ReedQuotaLimit - snapshot.ReedQuotaConsumed}");
        }

        public static string BuildQuotaConsumedMessage(PrototypeWetlandSnapshot snapshot, int amount)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Wetland reed quota consumed: amount={amount}; consumed={snapshot.ReedQuotaConsumed}; remaining={snapshot.ReedQuotaLimit - snapshot.ReedQuotaConsumed}; health={snapshot.WetlandHealth}; band={snapshot.WetlandHealthBand}");
        }

        public static string BuildTransitionMessage(
            string cause,
            int previousHealth,
            PrototypeWetlandHealthBand previousBand,
            PrototypeWetlandSnapshot current)
        {
            ArgumentNullException.ThrowIfNull(current);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Wetland transition: cause={cause}; health={previousHealth}->{current.WetlandHealth}; band={GetHealthBandId(previousBand)}->{current.WetlandHealthBand}");
        }
    }

    public sealed class PrototypeWetlandSnapshot
    {
        public string PolicyId { get; set; } = "neutral";

        public long? PolicySelectedTick { get; set; }

        public int PolicyVersion { get; set; }

        public int ReedQuotaLimit { get; set; }

        public int ReedQuotaConsumed { get; set; }

        public int WetlandHealth { get; set; } = PrototypeWetlandCatalog.NeutralHealth;

        public string WetlandHealthBand { get; set; } = "strained";
    }

    internal readonly record struct PrototypeWetlandTransition(
        int PreviousHealth,
        PrototypeWetlandHealthBand PreviousBand,
        int CurrentHealth,
        PrototypeWetlandHealthBand CurrentBand)
    {
        public bool BandChanged => PreviousBand != CurrentBand;
    }

    internal sealed class PrototypeWetlandState
    {
        public PrototypeCivicPolicy Policy { get; private set; } = PrototypeCivicPolicy.Neutral;

        public long? PolicySelectedTick { get; private set; }

        public int PolicyVersion { get; private set; }

        public int ReedQuotaLimit { get; private set; }

        public int ReedQuotaConsumed { get; private set; }

        public int WetlandHealth { get; private set; } = PrototypeWetlandCatalog.NeutralHealth;

        public PrototypeWetlandHealthBand WetlandHealthBand =>
            PrototypeWetlandCatalog.GetHealthBand(WetlandHealth);

        public int RemainingReedQuota => ReedQuotaLimit - ReedQuotaConsumed;

        public static PrototypeWetlandState CreateForSelection(
            PrototypeCivicPolicy policy,
            long selectedTick,
            int policyVersion)
        {
            if (policy == PrototypeCivicPolicy.Neutral ||
                !Enum.IsDefined(typeof(PrototypeCivicPolicy), policy) ||
                selectedTick < PrototypeCivicPolicyCatalog.SelectionWindowStartTick ||
                selectedTick > PrototypeCivicPolicyCatalog.SelectionWindowEndTick ||
                policyVersion != 1)
            {
                throw new ArgumentException("Wetland selection state does not match a selected civic policy.");
            }

            return new PrototypeWetlandState
            {
                Policy = policy,
                PolicySelectedTick = selectedTick,
                PolicyVersion = policyVersion,
                ReedQuotaLimit = PrototypeWetlandCatalog.GetQuotaLimit(policy),
                ReedQuotaConsumed = 0,
                WetlandHealth = PrototypeWetlandCatalog.GetSelectionHealth(policy)
            };
        }

        public static PrototypeWetlandState MigrateFromCivicPolicy(PrototypeCivicPolicyState civicPolicy)
        {
            ArgumentNullException.ThrowIfNull(civicPolicy);
            return civicPolicy.Policy == PrototypeCivicPolicy.Neutral
                ? new PrototypeWetlandState()
                : CreateForSelection(civicPolicy.Policy, civicPolicy.SelectedTick!.Value, civicPolicy.Version);
        }

        public bool CanApplyHarvest(string resourceId, int amount)
        {
            return !string.Equals(resourceId, PrototypeWetlandCatalog.ReedResourceId, StringComparison.Ordinal) ||
                Policy == PrototypeCivicPolicy.Neutral ||
                amount > 0 && amount <= RemainingReedQuota;
        }

        public PrototypeWetlandTransition CommitSuccessfulReedHarvest(int amount)
        {
            if (amount <= 0 || Policy == PrototypeCivicPolicy.Neutral || amount > RemainingReedQuota)
            {
                throw new InvalidOperationException("Reed consequence commit was not prepared against the current quota.");
            }

            int previousHealth = WetlandHealth;
            PrototypeWetlandHealthBand previousBand = WetlandHealthBand;
            int nextHealth = checked(
                WetlandHealth + checked(PrototypeWetlandCatalog.GetHarvestHealthDelta(Policy) * amount));
            if (nextHealth is < PrototypeWetlandCatalog.MinimumHealth or > PrototypeWetlandCatalog.MaximumHealth)
            {
                throw new InvalidOperationException("Reed consequence would exceed bounded wetland health.");
            }

            ReedQuotaConsumed = checked(ReedQuotaConsumed + amount);
            WetlandHealth = nextHealth;
            return new PrototypeWetlandTransition(
                previousHealth,
                previousBand,
                WetlandHealth,
                WetlandHealthBand);
        }

        public PrototypeWetlandSnapshot CaptureSnapshot()
        {
            return new PrototypeWetlandSnapshot
            {
                PolicyId = PrototypeCivicPolicyCatalog.GetId(Policy),
                PolicySelectedTick = PolicySelectedTick,
                PolicyVersion = PolicyVersion,
                ReedQuotaLimit = ReedQuotaLimit,
                ReedQuotaConsumed = ReedQuotaConsumed,
                WetlandHealth = WetlandHealth,
                WetlandHealthBand = PrototypeWetlandCatalog.GetHealthBandId(WetlandHealthBand)
            };
        }

        public static PrototypeWetlandState PrepareRestore(
            PrototypeWetlandSnapshot snapshot,
            PrototypeCivicPolicyState civicPolicy)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(civicPolicy);

            PrototypeCivicPolicy policy;
            try
            {
                policy = PrototypeCivicPolicyCatalog.ParseId(snapshot.PolicyId);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Runtime snapshot wetland policy id is invalid.", exception);
            }

            PrototypeWetlandHealthBand storedBand =
                PrototypeWetlandCatalog.ParseHealthBandId(snapshot.WetlandHealthBand);
            if (policy != civicPolicy.Policy ||
                snapshot.PolicySelectedTick != civicPolicy.SelectedTick ||
                snapshot.PolicyVersion != civicPolicy.Version ||
                snapshot.WetlandHealth is < PrototypeWetlandCatalog.MinimumHealth or > PrototypeWetlandCatalog.MaximumHealth ||
                storedBand != PrototypeWetlandCatalog.GetHealthBand(snapshot.WetlandHealth))
            {
                throw new InvalidDataException("Runtime snapshot wetland state does not match its civic policy or health band.");
            }

            int expectedLimit = PrototypeWetlandCatalog.GetQuotaLimit(policy);
            if (snapshot.ReedQuotaLimit != expectedLimit ||
                snapshot.ReedQuotaConsumed < 0 ||
                snapshot.ReedQuotaConsumed > expectedLimit)
            {
                throw new InvalidDataException("Runtime snapshot wetland quota state is inconsistent.");
            }

            int selectionHealth = PrototypeWetlandCatalog.GetSelectionHealth(policy);
            if (policy == PrototypeCivicPolicy.Neutral)
            {
                if (snapshot.ReedQuotaConsumed != 0 || snapshot.WetlandHealth != selectionHealth)
                {
                    throw new InvalidDataException("Neutral civic policy requires neutral wetland state.");
                }
            }
            else if (snapshot.WetlandHealth != checked(
                selectionHealth + checked(
                    PrototypeWetlandCatalog.GetHarvestHealthDelta(policy) * snapshot.ReedQuotaConsumed)))
            {
                throw new InvalidDataException("Runtime snapshot wetland health does not match exact per-unit reed consequences.");
            }

            return new PrototypeWetlandState
            {
                Policy = policy,
                PolicySelectedTick = snapshot.PolicySelectedTick,
                PolicyVersion = snapshot.PolicyVersion,
                ReedQuotaLimit = snapshot.ReedQuotaLimit,
                ReedQuotaConsumed = snapshot.ReedQuotaConsumed,
                WetlandHealth = snapshot.WetlandHealth
            };
        }

    }
}
