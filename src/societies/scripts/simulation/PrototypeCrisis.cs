using Societies.Core;
using System;

namespace Societies.Simulation
{
    public enum PrototypeCrisisOutcome
    {
        Active,
        Stable,
        Collapsed
    }

    public enum PrototypeCrisisCollapseCause
    {
        None,
        IncapacitatedHold,
        Deadline
    }

    public readonly record struct PrototypeCrisisObservation(
        int TotalCitizens,
        int CapableCitizens,
        int Meals,
        int HearthFuel,
        int BedCoveragePercent)
    {
        public int IncapacitatedCitizens => TotalCitizens - CapableCitizens;
    }

    /// <summary>
    /// Pure deterministic crisis evaluator. It advances only when the owner supplies an
    /// authoritative simulation tick; presentation code can read it but owns no outcome rule.
    /// </summary>
    public sealed class PrototypeCrisisState
    {
        private readonly PrototypeCrisisDefinition _definition;

        public PrototypeCrisisState(PrototypeCrisisDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(definition.Id) || definition.TicksPerSecond <= 0 ||
                definition.DeadlineTicks <= 0 || definition.StableHoldTicks <= 0 ||
                definition.CollapseHoldTicks <= 0 || definition.StableHoldTicks >= definition.DeadlineTicks ||
                definition.CollapseHoldTicks >= definition.DeadlineTicks ||
                definition.RequiredCapableCitizens <= 0 || definition.CollapseIncapacitatedCitizens <= 0 ||
                definition.RequiredMeals < 0 || definition.RequiredHearthFuel < 0 ||
                definition.RequiredBedCoveragePercent < 0 || definition.RequiredBedCoveragePercent > 100 ||
                !float.IsFinite(definition.CitizenNeedRateMultiplier) ||
                definition.CitizenNeedRateMultiplier <= 0.0f || definition.CitizenNeedRateMultiplier > 1.0f)
            {
                throw new ArgumentException("Crisis definition is malformed.", nameof(definition));
            }
        }

        public PrototypeCrisisDefinition Definition => _definition;

        public int ElapsedTicks { get; private set; }

        public double ElapsedSeconds => ElapsedTicks / (double)_definition.TicksPerSecond;

        public int RemainingTicks => Math.Max(0, _definition.DeadlineTicks - ElapsedTicks);

        public double RemainingSeconds => RemainingTicks / (double)_definition.TicksPerSecond;

        public int StableHoldTicks { get; private set; }

        public int CollapseHoldTicks { get; private set; }

        public PrototypeCrisisOutcome Outcome { get; private set; } = PrototypeCrisisOutcome.Active;

        public PrototypeCrisisCollapseCause CollapseCause { get; private set; } = PrototypeCrisisCollapseCause.None;

        public bool IsTerminal => Outcome != PrototypeCrisisOutcome.Active;

        public bool HasObservation { get; private set; }

        public PrototypeCrisisObservation LastObservation { get; private set; }

        public bool MeetsStabilityCondition => HasObservation &&
            LastObservation.CapableCitizens >= _definition.RequiredCapableCitizens &&
            LastObservation.Meals >= _definition.RequiredMeals &&
            LastObservation.HearthFuel >= _definition.RequiredHearthFuel &&
            LastObservation.BedCoveragePercent >= _definition.RequiredBedCoveragePercent;

        public bool MeetsCollapseCondition => HasObservation &&
            LastObservation.IncapacitatedCitizens >= _definition.CollapseIncapacitatedCitizens;

        public void Advance(PrototypeCrisisObservation observation, bool simulationPaused = false)
        {
            ValidateObservation(observation);
            if (simulationPaused || IsTerminal)
            {
                return;
            }

            LastObservation = observation;
            HasObservation = true;
            ElapsedTicks++;

            StableHoldTicks = MeetsStabilityCondition ? StableHoldTicks + 1 : 0;
            CollapseHoldTicks = MeetsCollapseCondition ? CollapseHoldTicks + 1 : 0;

            if (CollapseHoldTicks >= _definition.CollapseHoldTicks)
            {
                Outcome = PrototypeCrisisOutcome.Collapsed;
                CollapseCause = PrototypeCrisisCollapseCause.IncapacitatedHold;
                return;
            }

            if (ElapsedTicks >= _definition.DeadlineTicks)
            {
                Outcome = PrototypeCrisisOutcome.Collapsed;
                CollapseCause = PrototypeCrisisCollapseCause.Deadline;
                return;
            }

            if (StableHoldTicks >= _definition.StableHoldTicks)
            {
                Outcome = PrototypeCrisisOutcome.Stable;
            }
        }

        public PrototypeCrisisStateSnapshot CaptureSnapshot()
        {
            return new PrototypeCrisisStateSnapshot
            {
                CrisisId = _definition.Id,
                ElapsedTicks = ElapsedTicks,
                StableHoldTicks = StableHoldTicks,
                CollapseHoldTicks = CollapseHoldTicks,
                Outcome = Outcome,
                CollapseCause = CollapseCause,
                HasObservation = HasObservation,
                LastObservation = LastObservation
            };
        }

        public void Restore(PrototypeCrisisStateSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!string.Equals(snapshot.CrisisId, _definition.Id, StringComparison.Ordinal) ||
                snapshot.ElapsedTicks < 0 || snapshot.ElapsedTicks > _definition.DeadlineTicks ||
                snapshot.StableHoldTicks < 0 || snapshot.StableHoldTicks > _definition.StableHoldTicks ||
                snapshot.CollapseHoldTicks < 0 || snapshot.CollapseHoldTicks > _definition.CollapseHoldTicks ||
                !Enum.IsDefined(snapshot.Outcome) || !Enum.IsDefined(snapshot.CollapseCause))
            {
                throw new ArgumentException("Crisis snapshot is malformed or targets a different crisis.", nameof(snapshot));
            }

            if (snapshot.HasObservation)
            {
                ValidateObservation(snapshot.LastObservation);
            }
            else if (snapshot.ElapsedTicks != 0 || snapshot.StableHoldTicks != 0 || snapshot.CollapseHoldTicks != 0)
            {
                throw new ArgumentException("A progressed crisis snapshot must contain an observation.", nameof(snapshot));
            }

            if ((snapshot.Outcome == PrototypeCrisisOutcome.Active && snapshot.CollapseCause != PrototypeCrisisCollapseCause.None) ||
                (snapshot.Outcome == PrototypeCrisisOutcome.Stable && snapshot.CollapseCause != PrototypeCrisisCollapseCause.None) ||
                (snapshot.Outcome == PrototypeCrisisOutcome.Collapsed && snapshot.CollapseCause == PrototypeCrisisCollapseCause.None))
            {
                throw new ArgumentException("Crisis snapshot outcome and collapse cause are inconsistent.", nameof(snapshot));
            }

            bool activeStateIsTerminal = snapshot.ElapsedTicks >= _definition.DeadlineTicks ||
                snapshot.StableHoldTicks >= _definition.StableHoldTicks ||
                snapshot.CollapseHoldTicks >= _definition.CollapseHoldTicks;
            bool holdExceedsElapsed = snapshot.StableHoldTicks > snapshot.ElapsedTicks ||
                snapshot.CollapseHoldTicks > snapshot.ElapsedTicks;
            bool observationMeetsStability = snapshot.HasObservation && ObservationMeetsStability(snapshot.LastObservation);
            bool observationMeetsCollapse = snapshot.HasObservation && ObservationMeetsCollapse(snapshot.LastObservation);
            bool stableHoldContradictsObservation = snapshot.StableHoldTicks > 0 && !observationMeetsStability;
            bool collapseHoldContradictsObservation = snapshot.CollapseHoldTicks > 0 && !observationMeetsCollapse;
            bool stableStateIsIncomplete = snapshot.Outcome == PrototypeCrisisOutcome.Stable &&
                (snapshot.StableHoldTicks != _definition.StableHoldTicks || !observationMeetsStability ||
                    snapshot.ElapsedTicks >= _definition.DeadlineTicks ||
                    snapshot.CollapseHoldTicks >= _definition.CollapseHoldTicks);
            bool incapacityCollapseIsIncomplete = snapshot.Outcome == PrototypeCrisisOutcome.Collapsed &&
                snapshot.CollapseCause == PrototypeCrisisCollapseCause.IncapacitatedHold &&
                (snapshot.CollapseHoldTicks != _definition.CollapseHoldTicks || !observationMeetsCollapse);
            bool deadlineCollapseIsIncomplete = snapshot.Outcome == PrototypeCrisisOutcome.Collapsed &&
                snapshot.CollapseCause == PrototypeCrisisCollapseCause.Deadline &&
                (snapshot.ElapsedTicks != _definition.DeadlineTicks ||
                    snapshot.CollapseHoldTicks >= _definition.CollapseHoldTicks);
            if ((snapshot.Outcome == PrototypeCrisisOutcome.Active && activeStateIsTerminal) ||
                holdExceedsElapsed || stableHoldContradictsObservation || collapseHoldContradictsObservation ||
                stableStateIsIncomplete || incapacityCollapseIsIncomplete || deadlineCollapseIsIncomplete ||
                (snapshot.Outcome != PrototypeCrisisOutcome.Active && !snapshot.HasObservation))
            {
                throw new ArgumentException("Crisis snapshot progress is inconsistent with its outcome.", nameof(snapshot));
            }

            ElapsedTicks = snapshot.ElapsedTicks;
            StableHoldTicks = snapshot.StableHoldTicks;
            CollapseHoldTicks = snapshot.CollapseHoldTicks;
            Outcome = snapshot.Outcome;
            CollapseCause = snapshot.CollapseCause;
            HasObservation = snapshot.HasObservation;
            LastObservation = snapshot.LastObservation;
        }

        private bool ObservationMeetsStability(PrototypeCrisisObservation observation)
        {
            return observation.CapableCitizens >= _definition.RequiredCapableCitizens &&
                observation.Meals >= _definition.RequiredMeals &&
                observation.HearthFuel >= _definition.RequiredHearthFuel &&
                observation.BedCoveragePercent >= _definition.RequiredBedCoveragePercent;
        }

        private bool ObservationMeetsCollapse(PrototypeCrisisObservation observation)
        {
            return observation.IncapacitatedCitizens >= _definition.CollapseIncapacitatedCitizens;
        }

        private static void ValidateObservation(PrototypeCrisisObservation observation)
        {
            if (observation.TotalCitizens < 0 || observation.CapableCitizens < 0 ||
                observation.CapableCitizens > observation.TotalCitizens || observation.Meals < 0 ||
                observation.HearthFuel < 0 || observation.BedCoveragePercent < 0 ||
                observation.BedCoveragePercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(observation), "Crisis observation contains invalid settlement values.");
            }
        }
    }

    public sealed class PrototypeCrisisStateSnapshot
    {
        public string CrisisId { get; set; } = string.Empty;

        public int ElapsedTicks { get; set; }

        public int StableHoldTicks { get; set; }

        public int CollapseHoldTicks { get; set; }

        public PrototypeCrisisOutcome Outcome { get; set; }

        public PrototypeCrisisCollapseCause CollapseCause { get; set; }

        public bool HasObservation { get; set; }

        public PrototypeCrisisObservation LastObservation { get; set; }
    }
}
