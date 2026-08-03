using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Societies.Core
{
    /// <summary>
    /// Finite, deterministic codes explaining a citizen's derived civic preference.
    /// The declaration order is also the aggregate-event reason order.
    /// </summary>
    public enum PrototypeCitizenInterestReason
    {
        CriticalNutrition,
        CriticalFatigue,
        FoodSecurity,
        RecoveryNeed,
        FutureReedSupply,
        BalancedLongTermSupply,
        ImmediateShelterSupply,
        ImmediateMaterialSupply,
        MaterialThroughput
    }

    public enum PrototypeCitizenInterestPosition
    {
        Uncommitted,
        Supports,
        Opposes
    }

    public enum PrototypeCitizenNutritionBand
    {
        Critical,
        FoodInsecure,
        Secure
    }

    public enum PrototypeCitizenFatigueBand
    {
        Exhausted,
        NeedsRecovery,
        Rested
    }

    /// <summary>
    /// A read-only explanation derived from current citizen facts. It is not simulation state.
    /// </summary>
    public sealed record PrototypeCitizenInterest(
        string WorkerId,
        PrototypeCivicPolicy PreferredPolicy,
        PrototypeCitizenInterestPosition Position,
        PrototypeCitizenInterestReason Reason,
        string RoleId,
        PrototypeCitizenNutritionBand NutritionBand,
        PrototypeCitizenFatigueBand FatigueBand,
        string Summary);

    /// <summary>
    /// Pure deterministic projection of citizen needs and roles into bounded civic interests.
    /// </summary>
    public static class PrototypeCitizenInterestEvaluator
    {
        public const int MaximumSummaryLength = 64;

        public static PrototypeCitizenInterest Evaluate(
            PrototypeWorkerState worker,
            PrototypeCivicPolicy selectedPolicy)
        {
            ArgumentNullException.ThrowIfNull(worker);
            ArgumentNullException.ThrowIfNull(worker.Needs);
            return Evaluate(worker.WorkerId, worker.Role, worker.Needs.Nutrition, worker.Needs.Fatigue, selectedPolicy);
        }

        public static PrototypeCitizenInterest Evaluate(
            string workerId,
            PrototypeCitizenRole role,
            float nutrition,
            float fatigue,
            PrototypeCivicPolicy selectedPolicy)
        {
            ValidateWorkerIdentity(workerId);
            ValidateNeedValue(nutrition, nameof(nutrition));
            ValidateNeedValue(fatigue, nameof(fatigue));
            ValidatePolicy(selectedPolicy);

            PrototypeCitizenNutritionBand nutritionBand = GetNutritionBand(nutrition);
            PrototypeCitizenFatigueBand fatigueBand = GetFatigueBand(fatigue);
            string roleId = GetRoleId(role);
            (PrototypeCivicPolicy preferredPolicy, PrototypeCitizenInterestReason reason, string summary) =
                ResolvePreference(role, nutritionBand, fatigueBand, roleId);
            if (summary.Length > MaximumSummaryLength)
            {
                throw new InvalidOperationException("Citizen interest summary exceeds its bounded contract.");
            }

            return new PrototypeCitizenInterest(
                workerId,
                preferredPolicy,
                GetPosition(preferredPolicy, selectedPolicy),
                reason,
                roleId,
                nutritionBand,
                fatigueBand,
                summary);
        }

        public static IReadOnlyList<PrototypeCitizenInterest> Capture(
            IEnumerable<PrototypeWorkerState> workers,
            PrototypeCivicPolicy selectedPolicy)
        {
            ArgumentNullException.ThrowIfNull(workers);
            ValidatePolicy(selectedPolicy);

            List<PrototypeCitizenInterest> interests = new();
            foreach (PrototypeWorkerState worker in workers)
            {
                ArgumentNullException.ThrowIfNull(worker);
                interests.Add(Evaluate(worker, selectedPolicy));
            }

            interests.Sort((left, right) => StringComparer.Ordinal.Compare(left.WorkerId, right.WorkerId));
            for (int index = 1; index < interests.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(interests[index - 1].WorkerId, interests[index].WorkerId))
                {
                    throw new InvalidOperationException("Citizen interest capture requires unique worker ids.");
                }
            }

            return interests.AsReadOnly();
        }

        public static string BuildAggregateSummary(IReadOnlyList<PrototypeCitizenInterest> interests)
        {
            ArgumentNullException.ThrowIfNull(interests);

            int protectCount = 0;
            int drawDownCount = 0;
            int[] reasonCounts = new int[9];
            foreach (PrototypeCitizenInterest interest in interests)
            {
                ArgumentNullException.ThrowIfNull(interest);
                switch (interest.PreferredPolicy)
                {
                    case PrototypeCivicPolicy.ProtectWetland:
                        protectCount++;
                        break;
                    case PrototypeCivicPolicy.DrawDownWetland:
                        drawDownCount++;
                        break;
                    default:
                        throw new InvalidOperationException("Citizen interests must prefer a non-neutral civic policy.");
                }

                int reasonIndex = (int)interest.Reason;
                if (reasonIndex < 0 || reasonIndex >= reasonCounts.Length)
                {
                    throw new InvalidOperationException("Citizen interest contains an unknown reason.");
                }

                reasonCounts[reasonIndex]++;
            }

            string summary = "Civic preferences: protect=" + ToInvariant(protectCount) + "; draw_down=" + ToInvariant(drawDownCount) + "; reasons=" +
                "critical_nutrition=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.CriticalNutrition]) + "," +
                "critical_fatigue=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.CriticalFatigue]) + "," +
                "food_security=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.FoodSecurity]) + "," +
                "recovery_need=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.RecoveryNeed]) + "," +
                "future_reed_supply=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.FutureReedSupply]) + "," +
                "balanced_long_term_supply=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.BalancedLongTermSupply]) + "," +
                "immediate_shelter_supply=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.ImmediateShelterSupply]) + "," +
                "immediate_material_supply=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.ImmediateMaterialSupply]) + "," +
                "material_throughput=" + ToInvariant(reasonCounts[(int)PrototypeCitizenInterestReason.MaterialThroughput]);
            if (summary.Length > PrototypeRunArtifactManager.MaximumMessageLength)
            {
                throw new InvalidOperationException("Civic preference summary exceeds its bounded event contract.");
            }

            return summary;
        }

        public static string GetReasonCode(PrototypeCitizenInterestReason reason)
        {
            return reason switch
            {
                PrototypeCitizenInterestReason.CriticalNutrition => "critical_nutrition",
                PrototypeCitizenInterestReason.CriticalFatigue => "critical_fatigue",
                PrototypeCitizenInterestReason.FoodSecurity => "food_security",
                PrototypeCitizenInterestReason.RecoveryNeed => "recovery_need",
                PrototypeCitizenInterestReason.FutureReedSupply => "future_reed_supply",
                PrototypeCitizenInterestReason.BalancedLongTermSupply => "balanced_long_term_supply",
                PrototypeCitizenInterestReason.ImmediateShelterSupply => "immediate_shelter_supply",
                PrototypeCitizenInterestReason.ImmediateMaterialSupply => "immediate_material_supply",
                PrototypeCitizenInterestReason.MaterialThroughput => "material_throughput",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown citizen interest reason.")
            };
        }

        public static string GetRoleId(PrototypeCitizenRole role)
        {
            return role switch
            {
                PrototypeCitizenRole.Forager => "forager",
                PrototypeCitizenRole.Generalist => "generalist",
                PrototypeCitizenRole.Builder => "builder",
                PrototypeCitizenRole.Logger => "logger",
                PrototypeCitizenRole.Mason => "mason",
                PrototypeCitizenRole.Hauler => "hauler",
                PrototypeCitizenRole.Processor => "processor",
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown citizen role.")
            };
        }

        private static PrototypeCitizenNutritionBand GetNutritionBand(float nutrition)
        {
            return nutrition switch
            {
                <= 12.0f => PrototypeCitizenNutritionBand.Critical,
                <= 45.0f => PrototypeCitizenNutritionBand.FoodInsecure,
                _ => PrototypeCitizenNutritionBand.Secure
            };
        }

        private static PrototypeCitizenFatigueBand GetFatigueBand(float fatigue)
        {
            return fatigue switch
            {
                >= 90.0f => PrototypeCitizenFatigueBand.Exhausted,
                >= 62.0f => PrototypeCitizenFatigueBand.NeedsRecovery,
                _ => PrototypeCitizenFatigueBand.Rested
            };
        }

        private static (PrototypeCivicPolicy preferredPolicy, PrototypeCitizenInterestReason reason, string summary)
            ResolvePreference(
                PrototypeCitizenRole role,
                PrototypeCitizenNutritionBand nutritionBand,
                PrototypeCitizenFatigueBand fatigueBand,
                string roleId)
        {
            if (nutritionBand == PrototypeCitizenNutritionBand.Critical)
            {
                return (PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.CriticalNutrition, "nutrition=critical");
            }

            if (fatigueBand == PrototypeCitizenFatigueBand.Exhausted)
            {
                return (PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.CriticalFatigue, "fatigue=exhausted");
            }

            if (nutritionBand == PrototypeCitizenNutritionBand.FoodInsecure)
            {
                return (PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.FoodSecurity, "nutrition=food_insecure");
            }

            if (fatigueBand == PrototypeCitizenFatigueBand.NeedsRecovery)
            {
                return (PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.RecoveryNeed, "fatigue=needs_recovery");
            }

            return role switch
            {
                PrototypeCitizenRole.Forager => (PrototypeCivicPolicy.ProtectWetland, PrototypeCitizenInterestReason.FutureReedSupply, $"role={roleId}"),
                PrototypeCitizenRole.Generalist => (PrototypeCivicPolicy.ProtectWetland, PrototypeCitizenInterestReason.BalancedLongTermSupply, $"role={roleId}"),
                PrototypeCitizenRole.Builder => (PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.ImmediateShelterSupply, $"role={roleId}"),
                PrototypeCitizenRole.Logger or PrototypeCitizenRole.Mason => (PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.ImmediateMaterialSupply, $"role={roleId}"),
                PrototypeCitizenRole.Hauler or PrototypeCitizenRole.Processor => (PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.MaterialThroughput, $"role={roleId}"),
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown citizen role.")
            };
        }

        private static PrototypeCitizenInterestPosition GetPosition(
            PrototypeCivicPolicy preferredPolicy,
            PrototypeCivicPolicy selectedPolicy)
        {
            return selectedPolicy switch
            {
                PrototypeCivicPolicy.Neutral => PrototypeCitizenInterestPosition.Uncommitted,
                _ when selectedPolicy == preferredPolicy => PrototypeCitizenInterestPosition.Supports,
                _ => PrototypeCitizenInterestPosition.Opposes
            };
        }

        private static void ValidatePolicy(PrototypeCivicPolicy policy)
        {
            if (!Enum.IsDefined(typeof(PrototypeCivicPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown civic policy.");
            }
        }

        private static void ValidateWorkerIdentity(string workerId)
        {
            if (string.IsNullOrWhiteSpace(workerId) || workerId.Length > PrototypeRunArtifactManager.MaximumIdentifierLength)
            {
                throw new ArgumentException("Citizen interest requires a bounded worker id.", nameof(workerId));
            }
        }

        private static void ValidateNeedValue(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value < 0.0f || value > 100.0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Citizen need values must be finite values in the inclusive 0..100 range.");
            }
        }

        private static string ToInvariant(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
