using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Societies.Core
{
    /// <summary>
    /// Closed, scenario-specific authority for the pre-citizen Causeway Before Nightfall situation.
    /// It intentionally models only the material and ecological facts Packet 02 needs.
    /// </summary>
    public sealed class PrototypeCausewayState
    {
        public const int MaximumEventCount = 64;
        private readonly PrototypeCausewayDefinition _definition;

        public PrototypeCausewayState(PrototypeCausewayDefinition definition)
        {
            _definition = PrototypeCausewayDefinitionContract.Freeze(
                definition ?? throw new ArgumentNullException(nameof(definition)), "authority");
            CausewayIntegrity = _definition.InitialCausewayIntegrity;
            WetlandHealth = _definition.InitialWetlandHealth;
            ReservedDryTimber = _definition.ReservedDryTimber;
            CommunityTimber = _definition.InitialCommunityTimber;
            Stone = _definition.InitialStone;
            ReedBundles = _definition.InitialReedBundles;
            AvailablePlayerLabor = _definition.InitialPlayerLabor;
        }

        public long Revision { get; private set; }
        public int MigrationSourceSchemaVersion { get; private set; }
        public int CausewayIntegrity { get; private set; }
        public int WetlandHealth { get; private set; }
        public int ReservedDryTimber { get; private set; }
        public int CommunityTimber { get; private set; }
        public int Stone { get; private set; }
        public int ReedBundles { get; private set; }
        public int AvailablePlayerLabor { get; private set; }
        public int PlayerLabor { get; private set; }
        public int ShelterTimberSpent { get; private set; }
        public int ShelterLaborSpent { get; private set; }
        public int CausewayTimberCommitted { get; private set; }
        public int CausewayStoneCommitted { get; private set; }
        public int CausewayReedsCommitted { get; private set; }
        public PrototypeCausewayWaterControl WaterControl { get; private set; }
        public bool NightfallReached { get; private set; }
        public bool MorningResolved { get; private set; }
        public PrototypeCausewayMorningOutcome MorningOutcome { get; private set; }
        public bool PlayerShelterRepaired { get; private set; }
        public bool RestorationRequired { get; private set; }
        public int RestorationDueMorning { get; private set; }

        public PrototypeCausewayProjection CaptureProjection(float currentHour)
        {
            return new PrototypeCausewayProjection(
                Revision, CausewayIntegrity, WetlandHealth, ReservedDryTimber, CommunityTimber, Stone, ReedBundles,
                PlayerLabor, AvailablePlayerLabor, ShelterTimberSpent, ShelterLaborSpent,
                CausewayTimberCommitted, CausewayStoneCommitted, CausewayReedsCommitted, WaterControl,
                NightfallReached, MorningResolved, MorningOutcome, PlayerShelterRepaired, RestorationRequired,
                RestorationDueMorning, CanRepairPlayerShelter, currentHour,
                MorningResolved ? "morning resolved" : NightfallReached ? "through nightfall" : "before nightfall",
                _definition.CausewayAnchor.ToVector3(), _definition.NurseryAnchor.ToVector3(), _definition.ShelterAnchor.ToVector3());
        }

        public PrototypeCausewayStateSnapshot CaptureSnapshot()
        {
            return new PrototypeCausewayStateSnapshot
            {
                Revision = Revision, MigrationSourceSchemaVersion = MigrationSourceSchemaVersion,
                CausewayIntegrity = CausewayIntegrity, WetlandHealth = WetlandHealth,
                ReservedDryTimber = ReservedDryTimber, CommunityTimber = CommunityTimber, Stone = Stone,
                ReedBundles = ReedBundles, AvailablePlayerLabor = AvailablePlayerLabor, PlayerLabor = PlayerLabor,
                ShelterTimberSpent = ShelterTimberSpent, ShelterLaborSpent = ShelterLaborSpent,
                CausewayTimberCommitted = CausewayTimberCommitted,
                CausewayStoneCommitted = CausewayStoneCommitted, CausewayReedsCommitted = CausewayReedsCommitted,
                WaterControl = WaterControl.ToString(), NightfallReached = NightfallReached, MorningResolved = MorningResolved,
                MorningOutcome = MorningOutcome.ToString(), PlayerShelterRepaired = PlayerShelterRepaired,
                RestorationRequired = RestorationRequired, RestorationDueMorning = RestorationDueMorning,
                Definition = PrototypeCausewayDefinitionContract.CaptureSnapshot(_definition)
            };
        }

        public PrototypeCausewayCommandResult Execute(PrototypeCausewayCommand command)
        {
            if (command == null || !string.Equals(command.ActorId, "player", StringComparison.Ordinal)) return Reject("invalid_actor");
            if (MorningResolved) return Reject("morning_already_resolved");
            if (NightfallReached) return Reject("nightfall_deadline_passed");
            if (command.ExpectedRevision != Revision) return Reject("stale_revision");
            if (!TryGetNextRevision(Revision, out _)) return Reject("causeway_history_full");
            if ((command.Quantity <= 0 || command.Quantity > 16) && command.Kind is not PrototypeCausewayCommandKind.SelectWaterControl) return Reject("invalid_quantity");

            return command.Kind switch
            {
                PrototypeCausewayCommandKind.ContributeReservedDryTimber when ReservedDryTimber >= command.Quantity =>
                    Commit(command, PrototypeEventTypes.CausewayTimberSacrificed, $"reserved_dry_timber:{command.Quantity}", state =>
                    {
                        state.ReservedDryTimber -= command.Quantity;
                        state.CausewayTimberCommitted += command.Quantity;
                    }),
                PrototypeCausewayCommandKind.ContributeCommunityTimber when CommunityTimber >= command.Quantity =>
                    Commit(command, PrototypeEventTypes.CausewayMaterialCommitted, $"community_timber:{command.Quantity}", state =>
                    {
                        state.CommunityTimber -= command.Quantity;
                        state.CausewayTimberCommitted += command.Quantity;
                    }),
                PrototypeCausewayCommandKind.ContributeStone when Stone >= command.Quantity =>
                    Commit(command, PrototypeEventTypes.CausewayMaterialCommitted, $"stone:{command.Quantity}", state =>
                    {
                        state.Stone -= command.Quantity;
                        state.CausewayStoneCommitted += command.Quantity;
                    }),
                PrototypeCausewayCommandKind.ContributeReedBundles when ReedBundles >= command.Quantity =>
                    Commit(command, PrototypeEventTypes.CausewayMaterialCommitted, $"reeds:{command.Quantity}", state =>
                    {
                        state.ReedBundles -= command.Quantity;
                        state.CausewayReedsCommitted += command.Quantity;
                    }),
                PrototypeCausewayCommandKind.ContributeLabor when AvailablePlayerLabor >= command.Quantity =>
                    Commit(command, PrototypeEventTypes.CausewayLaborCommitted, $"player_labor:{command.Quantity}", state =>
                    {
                        state.AvailablePlayerLabor -= command.Quantity;
                        state.PlayerLabor += command.Quantity;
                    }),
                PrototypeCausewayCommandKind.RepairPlayerShelter when CanRepairPlayerShelter && command.Quantity == 1 =>
                    Commit(command, PrototypeEventTypes.CausewayShelterRepaired,
                        $"reserved_dry_timber:{_definition.RequiredShelterTimber};player_labor:1", state =>
                    {
                        state.ReservedDryTimber -= state._definition.RequiredShelterTimber;
                        state.AvailablePlayerLabor--;
                        state.ShelterTimberSpent += state._definition.RequiredShelterTimber;
                        state.ShelterLaborSpent++;
                        state.PlayerShelterRepaired = true;
                    }),
                PrototypeCausewayCommandKind.SelectWaterControl when WaterControl == PrototypeCausewayWaterControl.Unselected &&
                    Enum.IsDefined(typeof(PrototypeCausewayWaterControl), command.WaterControl) &&
                    command.WaterControl != PrototypeCausewayWaterControl.Unselected =>
                    Commit(command, PrototypeEventTypes.CausewayWaterControlSelected, command.WaterControl.ToString(), state => state.WaterControl = command.WaterControl),
                PrototypeCausewayCommandKind.ContributeReservedDryTimber or PrototypeCausewayCommandKind.ContributeCommunityTimber or
                PrototypeCausewayCommandKind.ContributeStone or PrototypeCausewayCommandKind.ContributeReedBundles => Reject("insufficient_material"),
                PrototypeCausewayCommandKind.ContributeLabor => Reject("insufficient_labor"),
                PrototypeCausewayCommandKind.RepairPlayerShelter => Reject("shelter_repair_unavailable"),
                PrototypeCausewayCommandKind.SelectWaterControl when WaterControl != PrototypeCausewayWaterControl.Unselected => Reject("water_control_already_selected"),
                PrototypeCausewayCommandKind.SelectWaterControl => Reject("invalid_water_control"),
                _ => Reject("unsupported_command")
            };
        }

        public IReadOnlyList<PrototypeCausewayTransitionResult> PrepareAdvance(double previousHour, double elapsedHours)
        {
            if (!double.IsFinite(previousHour) || previousHour < 0.0 || previousHour >= 24.0 ||
                !double.IsFinite(elapsedHours) || elapsedHours <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedHours), "Causeway time advance must be finite and positive from a normalized hour.");
            }

            var transitions = new List<PrototypeCausewayTransitionResult>(2);
            long preparedRevision = Revision;
            bool preparedNightfall = NightfallReached;
            double nightfallDistance = double.PositiveInfinity;
            if (!preparedNightfall)
            {
                nightfallDistance = ForwardDistance(previousHour, _definition.NightfallHour);
                if (nightfallDistance <= elapsedHours)
                {
                    preparedRevision = NextRevisionOrThrow(preparedRevision);
                    transitions.Add(PrototypeCausewayTransitionResult.Nightfall(
                        preparedRevision - 1, preparedRevision));
                    preparedNightfall = true;
                }
            }

            if (preparedNightfall && !MorningResolved)
            {
                double morningDistance = NightfallReached
                    ? ForwardDistance(previousHour, _definition.MorningHour)
                    : nightfallDistance + ForwardDistance(_definition.NightfallHour, _definition.MorningHour);
                if (morningDistance <= elapsedHours)
                {
                    CausewayMorningResolution resolution = DeriveMorningResolution();
                    long previousRevision = preparedRevision;
                    preparedRevision = NextRevisionOrThrow(preparedRevision);
                    transitions.Add(PrototypeCausewayTransitionResult.Morning(
                        previousRevision, preparedRevision, resolution));
                }
            }

            return transitions;
        }

        internal void Commit(PrototypeCausewayTransitionResult transition)
        {
            if (transition.PreviousRevision != Revision || transition.Revision != NextRevisionOrThrow(Revision))
            {
                throw new InvalidOperationException("Causeway time transition no longer matches authoritative revision.");
            }

            if (transition.Kind == PrototypeCausewayTransitionKind.Nightfall && !NightfallReached && !MorningResolved)
            {
                NightfallReached = true;
            }
            else if (transition.Kind == PrototypeCausewayTransitionKind.Morning && NightfallReached && !MorningResolved)
            {
                CausewayIntegrity = transition.CausewayIntegrity;
                WetlandHealth = transition.WetlandHealth;
                MorningOutcome = transition.MorningOutcome;
                RestorationRequired = true;
                RestorationDueMorning = transition.RestorationDueMorning;
                MorningResolved = true;
            }
            else
            {
                throw new InvalidOperationException("Causeway time transition is invalid for the authoritative phase.");
            }

            Revision = transition.Revision;
        }

        public static PrototypeCausewayState PrepareRestore(
            PrototypeCausewayDefinition definition,
            PrototypeCausewayStateSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            PrototypeCausewayDefinitionContract.ValidateBinding(definition, snapshot.Definition!);
            if (!TryParseCanonicalEnum(snapshot.WaterControl, out PrototypeCausewayWaterControl waterControl) ||
                !TryParseCanonicalEnum(snapshot.MorningOutcome, out PrototypeCausewayMorningOutcome morningOutcome))
            {
                throw new InvalidDataException("Causeway snapshot contains an unknown enum value.");
            }

            PrototypeCausewayState result = new(definition)
            {
                Revision = snapshot.Revision,
                MigrationSourceSchemaVersion = snapshot.MigrationSourceSchemaVersion,
                CausewayIntegrity = snapshot.CausewayIntegrity,
                WetlandHealth = snapshot.WetlandHealth,
                ReservedDryTimber = snapshot.ReservedDryTimber,
                CommunityTimber = snapshot.CommunityTimber,
                Stone = snapshot.Stone,
                ReedBundles = snapshot.ReedBundles,
                AvailablePlayerLabor = snapshot.AvailablePlayerLabor,
                PlayerLabor = snapshot.PlayerLabor,
                ShelterTimberSpent = snapshot.ShelterTimberSpent,
                ShelterLaborSpent = snapshot.ShelterLaborSpent,
                CausewayTimberCommitted = snapshot.CausewayTimberCommitted,
                CausewayStoneCommitted = snapshot.CausewayStoneCommitted,
                CausewayReedsCommitted = snapshot.CausewayReedsCommitted,
                WaterControl = waterControl,
                NightfallReached = snapshot.NightfallReached,
                MorningResolved = snapshot.MorningResolved,
                MorningOutcome = morningOutcome,
                PlayerShelterRepaired = snapshot.PlayerShelterRepaired,
                RestorationRequired = snapshot.RestorationRequired,
                RestorationDueMorning = snapshot.RestorationDueMorning
            };
            result.ValidateAgainstDefinition();
            return result;
        }

        public static PrototypeCausewayState PrepareMigration(
            PrototypeCausewayDefinition definition,
            float currentHour,
            int sourceSchemaVersion)
        {
            if (sourceSchemaVersion is not (10 or 11))
            {
                throw new InvalidDataException("Causeway migration source schema must be v10 or v11.");
            }
            PrototypeCausewayState result = new(definition)
            {
                MigrationSourceSchemaVersion = sourceSchemaVersion
            };
            result.ApplyMigrationPhase(currentHour);
            result.ValidateAgainstDefinition();
            result.ValidateCurrentHour(currentHour);
            return result;
        }

        public static void ValidateSnapshot(PrototypeCausewayStateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Revision < 0 || snapshot.Revision > MaximumEventCount ||
                snapshot.MigrationSourceSchemaVersion is not (0 or 10 or 11) || snapshot.CausewayIntegrity is < 0 or > 100 ||
                snapshot.WetlandHealth is < 0 or > 100 || snapshot.ReservedDryTimber < 0 || snapshot.CommunityTimber < 0 ||
                snapshot.Stone < 0 || snapshot.ReedBundles < 0 || snapshot.AvailablePlayerLabor < 0 || snapshot.PlayerLabor < 0 ||
                snapshot.ShelterTimberSpent < 0 || snapshot.ShelterLaborSpent < 0 || snapshot.CausewayTimberCommitted < 0 ||
                snapshot.CausewayStoneCommitted < 0 || snapshot.CausewayReedsCommitted < 0 || snapshot.RestorationDueMorning < 0 ||
                string.IsNullOrWhiteSpace(snapshot.WaterControl) || string.IsNullOrWhiteSpace(snapshot.MorningOutcome) ||
                (snapshot.MorningResolved && !snapshot.NightfallReached) ||
                (!snapshot.MorningResolved && snapshot.MorningOutcome != PrototypeCausewayMorningOutcome.Unresolved.ToString()) ||
                (snapshot.MorningResolved && snapshot.MorningOutcome == PrototypeCausewayMorningOutcome.Unresolved.ToString()) ||
                !TryParseCanonicalEnum(snapshot.WaterControl, out PrototypeCausewayWaterControl _) ||
                !TryParseCanonicalEnum(snapshot.MorningOutcome, out PrototypeCausewayMorningOutcome _))
            {
                throw new InvalidDataException("Causeway snapshot is malformed or internally inconsistent.");
            }
            _ = PrototypeCausewayDefinitionContract.PrepareFromSnapshot(snapshot.Definition);
        }

        internal static PrototypeCausewayStateSnapshot CloneSnapshot(PrototypeCausewayStateSnapshot snapshot) => new()
        {
            Revision = snapshot.Revision,
            MigrationSourceSchemaVersion = snapshot.MigrationSourceSchemaVersion,
            CausewayIntegrity = snapshot.CausewayIntegrity,
            WetlandHealth = snapshot.WetlandHealth,
            ReservedDryTimber = snapshot.ReservedDryTimber,
            CommunityTimber = snapshot.CommunityTimber,
            Stone = snapshot.Stone,
            ReedBundles = snapshot.ReedBundles,
            AvailablePlayerLabor = snapshot.AvailablePlayerLabor,
            PlayerLabor = snapshot.PlayerLabor,
            ShelterTimberSpent = snapshot.ShelterTimberSpent,
            ShelterLaborSpent = snapshot.ShelterLaborSpent,
            CausewayTimberCommitted = snapshot.CausewayTimberCommitted,
            CausewayStoneCommitted = snapshot.CausewayStoneCommitted,
            CausewayReedsCommitted = snapshot.CausewayReedsCommitted,
            WaterControl = snapshot.WaterControl,
            NightfallReached = snapshot.NightfallReached,
            MorningResolved = snapshot.MorningResolved,
            MorningOutcome = snapshot.MorningOutcome,
            PlayerShelterRepaired = snapshot.PlayerShelterRepaired,
            RestorationRequired = snapshot.RestorationRequired,
            RestorationDueMorning = snapshot.RestorationDueMorning,
            Definition = PrototypeCausewayDefinitionContract.CloneSnapshot(snapshot.Definition!)
        };

        internal static bool SnapshotsEqual(PrototypeCausewayStateSnapshot? first, PrototypeCausewayStateSnapshot? second) =>
            first != null && second != null &&
            first.Revision == second.Revision && first.MigrationSourceSchemaVersion == second.MigrationSourceSchemaVersion &&
            first.CausewayIntegrity == second.CausewayIntegrity &&
            first.WetlandHealth == second.WetlandHealth && first.ReservedDryTimber == second.ReservedDryTimber &&
            first.CommunityTimber == second.CommunityTimber && first.Stone == second.Stone &&
            first.ReedBundles == second.ReedBundles && first.AvailablePlayerLabor == second.AvailablePlayerLabor &&
            first.PlayerLabor == second.PlayerLabor && first.ShelterTimberSpent == second.ShelterTimberSpent &&
            first.ShelterLaborSpent == second.ShelterLaborSpent &&
            first.CausewayTimberCommitted == second.CausewayTimberCommitted &&
            first.CausewayStoneCommitted == second.CausewayStoneCommitted &&
            first.CausewayReedsCommitted == second.CausewayReedsCommitted &&
            string.Equals(first.WaterControl, second.WaterControl, StringComparison.Ordinal) &&
            first.NightfallReached == second.NightfallReached && first.MorningResolved == second.MorningResolved &&
            string.Equals(first.MorningOutcome, second.MorningOutcome, StringComparison.Ordinal) &&
            first.PlayerShelterRepaired == second.PlayerShelterRepaired &&
            first.RestorationRequired == second.RestorationRequired &&
            first.RestorationDueMorning == second.RestorationDueMorning &&
            PrototypeCausewayDefinitionContract.SnapshotsEqual(first.Definition, second.Definition);

        internal static void ValidateEventCoherence(
            PrototypeCausewayStateSnapshot snapshot,
            IReadOnlyList<PrototypeEventRecord> causewayEvents)
        {
            ValidateSnapshot(snapshot);
            if (causewayEvents.Count != snapshot.Revision || causewayEvents.Count > MaximumEventCount)
            {
                throw new InvalidDataException("Causeway revision does not match its bounded authoritative event sequence.");
            }

            bool hasNightfallEvent = causewayEvents.Any(entry => entry.EventType == PrototypeEventTypes.CausewayNightfallReached);
            bool hasMorningEvent = causewayEvents.Any(entry => entry.EventType == PrototypeEventTypes.CausewayMorningResolved);
            bool nightfall = false;
            bool morning = false;
            if (snapshot.MigrationSourceSchemaVersion is 10 or 11 && !hasNightfallEvent)
            {
                if (hasMorningEvent) nightfall = true;
                else
                {
                    nightfall = snapshot.NightfallReached;
                    morning = snapshot.MorningResolved;
                }
            }

            PrototypeCausewayDefinition definition =
                PrototypeCausewayDefinitionContract.PrepareFromSnapshot(snapshot.Definition);
            int reservedDryTimber = definition.ReservedDryTimber;
            int communityTimber = definition.InitialCommunityTimber;
            int stone = definition.InitialStone;
            int reeds = definition.InitialReedBundles;
            int availablePlayerLabor = definition.InitialPlayerLabor;
            int reservedTimberCommitted = 0;
            int communityTimberCommitted = 0;
            int stoneCommitted = 0;
            int reedsCommitted = 0;
            int playerLaborCommitted = 0;
            int shelterTimberSpent = 0;
            int shelterLaborSpent = 0;
            bool playerShelterRepaired = false;
            PrototypeCausewayWaterControl waterControl = PrototypeCausewayWaterControl.Unselected;
            foreach (PrototypeEventRecord entry in causewayEvents)
            {
                if (morning)
                {
                    throw new InvalidDataException("Causeway event sequence continues after morning resolution.");
                }
                if (nightfall && IsCausewayCommandEventType(entry.EventType))
                {
                    throw new InvalidDataException("Causeway command event occurs after the nightfall deadline.");
                }

                switch (entry.EventType)
                {
                    case PrototypeEventTypes.CausewayTimberSacrificed:
                    {
                        int quantity = ParseQuantity(entry.Message, "reserved_dry_timber:");
                        if (reservedDryTimber < quantity)
                            throw new InvalidDataException("Causeway reserved timber sacrifice exceeds its source custody.");
                        reservedDryTimber -= quantity;
                        reservedTimberCommitted += quantity;
                        break;
                    }
                    case PrototypeEventTypes.CausewayMaterialCommitted:
                    {
                        if (entry.Message.StartsWith("community_timber:", StringComparison.Ordinal))
                        {
                            int quantity = ParseQuantity(entry.Message, "community_timber:");
                            if (communityTimber < quantity)
                                throw new InvalidDataException("Causeway community timber commitment exceeds its source custody.");
                            communityTimber -= quantity;
                            communityTimberCommitted += quantity;
                        }
                        else if (entry.Message.StartsWith("stone:", StringComparison.Ordinal))
                        {
                            int quantity = ParseQuantity(entry.Message, "stone:");
                            if (stone < quantity)
                                throw new InvalidDataException("Causeway stone commitment exceeds its source custody.");
                            stone -= quantity;
                            stoneCommitted += quantity;
                        }
                        else if (entry.Message.StartsWith("reeds:", StringComparison.Ordinal))
                        {
                            int quantity = ParseQuantity(entry.Message, "reeds:");
                            if (reeds < quantity)
                                throw new InvalidDataException("Causeway reed commitment exceeds its source custody.");
                            reeds -= quantity;
                            reedsCommitted += quantity;
                        }
                        else throw new InvalidDataException("Causeway material event message is noncanonical.");
                        break;
                    }
                    case PrototypeEventTypes.CausewayLaborCommitted:
                    {
                        int quantity = ParseQuantity(entry.Message, "player_labor:");
                        if (availablePlayerLabor < quantity)
                            throw new InvalidDataException("Causeway labor commitment exceeds available player labor.");
                        availablePlayerLabor -= quantity;
                        playerLaborCommitted += quantity;
                        break;
                    }
                    case PrototypeEventTypes.CausewayShelterRepaired:
                    {
                        string expectedRepair = $"reserved_dry_timber:{definition.RequiredShelterTimber};player_labor:1";
                        if (playerShelterRepaired || reservedDryTimber < definition.RequiredShelterTimber || availablePlayerLabor < 1 ||
                            !string.Equals(entry.Message, expectedRepair, StringComparison.Ordinal))
                            throw new InvalidDataException("Causeway shelter-repair event is noncanonical or duplicated.");
                        reservedDryTimber -= definition.RequiredShelterTimber;
                        availablePlayerLabor--;
                        shelterTimberSpent += definition.RequiredShelterTimber;
                        shelterLaborSpent++;
                        playerShelterRepaired = true;
                        break;
                    }
                    case PrototypeEventTypes.CausewayWaterControlSelected:
                        if (waterControl != PrototypeCausewayWaterControl.Unselected ||
                            !TryParseCanonicalEnum(entry.Message, out PrototypeCausewayWaterControl selectedWaterControl) ||
                            selectedWaterControl == PrototypeCausewayWaterControl.Unselected)
                            throw new InvalidDataException("Causeway water-control event is noncanonical.");
                        waterControl = selectedWaterControl;
                        break;
                    case PrototypeEventTypes.CausewayNightfallReached:
                        if (nightfall || !string.Equals(entry.Message, "nightfall", StringComparison.Ordinal))
                            throw new InvalidDataException("Causeway nightfall event is noncanonical or duplicated.");
                        nightfall = true;
                        break;
                    case PrototypeEventTypes.CausewayMorningResolved:
                        if (!nightfall || morning || !string.Equals(entry.Message, snapshot.MorningOutcome, StringComparison.Ordinal))
                            throw new InvalidDataException("Causeway morning event is missing its ordered nightfall or outcome binding.");
                        morning = true;
                        break;
                    default:
                        throw new InvalidDataException("Causeway event sequence contains an unknown event type.");
                }
            }

            if (snapshot.ReservedDryTimber != reservedDryTimber || snapshot.CommunityTimber != communityTimber ||
                snapshot.Stone != stone || snapshot.ReedBundles != reeds ||
                snapshot.AvailablePlayerLabor != availablePlayerLabor || snapshot.PlayerLabor != playerLaborCommitted ||
                snapshot.ShelterTimberSpent != shelterTimberSpent || snapshot.ShelterLaborSpent != shelterLaborSpent ||
                snapshot.CausewayTimberCommitted != reservedTimberCommitted + communityTimberCommitted ||
                snapshot.CausewayStoneCommitted != stoneCommitted || snapshot.CausewayReedsCommitted != reedsCommitted ||
                snapshot.PlayerShelterRepaired != playerShelterRepaired ||
                snapshot.NightfallReached != nightfall || snapshot.MorningResolved != morning ||
                !string.Equals(snapshot.WaterControl, waterControl.ToString(), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Causeway events do not derive the persisted custody, phase, or water-control state.");
            }
        }

        internal static bool IsCausewayEventType(string eventType) =>
            eventType.StartsWith("causeway.", StringComparison.Ordinal);

        private static bool IsCausewayCommandEventType(string eventType) =>
            eventType is PrototypeEventTypes.CausewayTimberSacrificed or
                PrototypeEventTypes.CausewayMaterialCommitted or
                PrototypeEventTypes.CausewayLaborCommitted or
                PrototypeEventTypes.CausewayShelterRepaired or
                PrototypeEventTypes.CausewayWaterControlSelected;

        private static int ParseQuantity(string message, string prefix)
        {
            string value = message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..] : string.Empty;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int quantity) ||
                quantity is < 1 or > 16 || !string.Equals(value, quantity.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Causeway event quantity is noncanonical or out of range.");
            }
            return quantity;
        }

        private PrototypeCausewayCommandResult Commit(PrototypeCausewayCommand command, string eventType, string message, Action<PrototypeCausewayState> apply)
        {
            // All eligibility is checked before this point; the caller appends the returned event before committing once.
            if (!TryGetNextRevision(Revision, out long nextRevision)) return Reject("causeway_history_full");
            return new PrototypeCausewayCommandResult(true, string.Empty, Revision, nextRevision, eventType, message, apply);
        }

        internal void Commit(PrototypeCausewayCommandResult accepted)
        {
            if (!accepted.Accepted || accepted.Apply == null || accepted.PreviousRevision != Revision ||
                !TryGetNextRevision(Revision, out long nextRevision) || accepted.Revision != nextRevision)
            {
                throw new InvalidOperationException("Causeway command result no longer matches authoritative revision.");
            }
            accepted.Apply!(this);
            Revision = nextRevision;
        }

        private PrototypeCausewayCommandResult Reject(string reason) =>
            new(false, reason, Revision, Revision, string.Empty, string.Empty, null);

        private CausewayMorningResolution DeriveMorningResolution()
        {
            bool materialReady = CausewayTimberCommitted >= _definition.RequiredCausewayTimber &&
                CausewayStoneCommitted >= _definition.RequiredCausewayStone && CausewayReedsCommitted >= _definition.RequiredCausewayReeds;
            bool laborReady = PlayerLabor >= _definition.RequiredPlayerLabor;
            if (materialReady && laborReady && WaterControl == PrototypeCausewayWaterControl.ProtectNursery)
            {
                return new CausewayMorningResolution(68, Math.Max(0, _definition.InitialWetlandHealth - 8),
                    PrototypeCausewayMorningOutcome.StagedProtection, 2);
            }
            else if (materialReady && laborReady && WaterControl == PrototypeCausewayWaterControl.DrawDownWetland)
            {
                return new CausewayMorningResolution(76, Math.Max(0, _definition.InitialWetlandHealth - 25),
                    PrototypeCausewayMorningOutcome.DrawdownRepair, 1);
            }
            else
            {
                return new CausewayMorningResolution(Math.Max(0, _definition.InitialCausewayIntegrity - 18),
                    Math.Max(0, _definition.InitialWetlandHealth - 6), PrototypeCausewayMorningOutcome.CausewayBreach, 1);
            }
        }

        private void ValidateAgainstDefinition()
        {
            if (ReservedDryTimber > _definition.ReservedDryTimber || CommunityTimber > _definition.InitialCommunityTimber ||
                Stone > _definition.InitialStone || ReedBundles > _definition.InitialReedBundles ||
                AvailablePlayerLabor > _definition.InitialPlayerLabor ||
                PlayerLabor + AvailablePlayerLabor + ShelterLaborSpent != _definition.InitialPlayerLabor ||
                CausewayTimberCommitted + ReservedDryTimber + CommunityTimber + ShelterTimberSpent != _definition.ReservedDryTimber + _definition.InitialCommunityTimber ||
                CausewayStoneCommitted + Stone != _definition.InitialStone || CausewayReedsCommitted + ReedBundles != _definition.InitialReedBundles ||
                (PlayerShelterRepaired && (ShelterTimberSpent != _definition.RequiredShelterTimber || ShelterLaborSpent != 1)) ||
                (!PlayerShelterRepaired && (ShelterTimberSpent != 0 || ShelterLaborSpent != 0)))
            {
                throw new InvalidDataException("Causeway snapshot does not preserve configured material custody or outcome costs.");
            }

            if (!MorningResolved)
            {
                if (MorningOutcome != PrototypeCausewayMorningOutcome.Unresolved || RestorationRequired || RestorationDueMorning != 0 ||
                    CausewayIntegrity != _definition.InitialCausewayIntegrity || WetlandHealth != _definition.InitialWetlandHealth)
                {
                    throw new InvalidDataException("Causeway snapshot contains a fabricated pre-morning outcome.");
                }
                return;
            }

            CausewayMorningResolution expected = DeriveMorningResolution();
            if (!NightfallReached || MorningOutcome != expected.Outcome || CausewayIntegrity != expected.CausewayIntegrity ||
                WetlandHealth != expected.WetlandHealth || !RestorationRequired || RestorationDueMorning != expected.RestorationDueMorning)
            {
                throw new InvalidDataException("Causeway snapshot outcome does not match its authoritative costs and water control.");
            }
        }

        internal static bool TryGetNextRevision(long revision, out long nextRevision)
        {
            if (revision < 0 || revision >= MaximumEventCount)
            {
                nextRevision = revision;
                return false;
            }
            nextRevision = revision + 1;
            return true;
        }

        private static long NextRevisionOrThrow(long revision) =>
            TryGetNextRevision(revision, out long nextRevision)
                ? nextRevision
                : throw new InvalidOperationException("Causeway revision space is exhausted.");

        private static double ForwardDistance(double fromHour, double targetHour)
        {
            double distance = targetHour - fromHour;
            return distance > 0.0 ? distance : distance + 24.0;
        }

        private static bool TryParseCanonicalEnum<T>(string value, out T parsed) where T : struct, Enum =>
            Enum.TryParse(value, ignoreCase: false, out parsed) && Enum.IsDefined(parsed) &&
            string.Equals(value, parsed.ToString(), StringComparison.Ordinal);

        internal readonly record struct CausewayMorningResolution(
            int CausewayIntegrity, int WetlandHealth, PrototypeCausewayMorningOutcome Outcome, int RestorationDueMorning);

        internal void ValidateCurrentHour(float currentHour)
        {
            if (!float.IsFinite(currentHour) || currentHour < 0.0f || currentHour >= 24.0f)
            {
                throw new InvalidDataException("Causeway current hour is not canonical.");
            }
            if (MorningResolved) return;

            bool overnight = currentHour >= _definition.NightfallHour || currentHour < _definition.MorningHour;
            bool followingMorning = currentHour >= _definition.MorningHour && currentHour < _definition.ScenarioStartHour;
            if ((overnight && !NightfallReached) || followingMorning ||
                (!overnight && !followingMorning && NightfallReached))
            {
                throw new InvalidDataException("Causeway phase contradicts the authoritative current hour.");
            }
        }

        private void ApplyMigrationPhase(float currentHour)
        {
            bool overnight = currentHour >= _definition.NightfallHour || currentHour < _definition.MorningHour;
            bool followingMorning = currentHour >= _definition.MorningHour && currentHour < _definition.ScenarioStartHour;
            if (overnight || followingMorning) NightfallReached = true;
            if (followingMorning)
            {
                CausewayMorningResolution resolution = DeriveMorningResolution();
                CausewayIntegrity = resolution.CausewayIntegrity;
                WetlandHealth = resolution.WetlandHealth;
                MorningOutcome = resolution.Outcome;
                RestorationRequired = true;
                RestorationDueMorning = resolution.RestorationDueMorning;
                MorningResolved = true;
            }
        }

        public bool CanRepairPlayerShelter => !MorningResolved && !PlayerShelterRepaired &&
            !NightfallReached && ReservedDryTimber >= _definition.RequiredShelterTimber && AvailablePlayerLabor >= 1;
    }

    public enum PrototypeCausewayCommandKind { ContributeReservedDryTimber, ContributeCommunityTimber, ContributeStone, ContributeReedBundles, ContributeLabor, RepairPlayerShelter, SelectWaterControl }
    public enum PrototypeCausewayWaterControl { Unselected, ProtectNursery, DrawDownWetland }
    public enum PrototypeCausewayMorningOutcome { Unresolved, StagedProtection, DrawdownRepair, CausewayBreach }

    public sealed class PrototypeCausewayCommand
    {
        public string ActorId { get; set; } = "player";
        public long ExpectedRevision { get; set; }
        public PrototypeCausewayCommandKind Kind { get; set; }
        public int Quantity { get; set; } = 1;
        public PrototypeCausewayWaterControl WaterControl { get; set; }
    }

    public sealed class PrototypeCausewayCommandResult
    {
        internal PrototypeCausewayCommandResult(bool accepted, string rejection, long previousRevision, long revision, string eventType, string eventMessage, Action<PrototypeCausewayState>? apply)
        {
            Accepted = accepted; Rejection = rejection; PreviousRevision = previousRevision; Revision = revision;
            EventType = eventType; EventMessage = eventMessage; Apply = apply;
        }
        public bool Accepted { get; }
        public string Rejection { get; }
        public long PreviousRevision { get; }
        public long Revision { get; }
        public string EventType { get; }
        public string EventMessage { get; }
        internal Action<PrototypeCausewayState>? Apply { get; }
    }

    public enum PrototypeCausewayTransitionKind { Nightfall, Morning }

    public readonly record struct PrototypeCausewayTransitionResult(
        long PreviousRevision,
        long Revision,
        string EventType,
        string Message,
        bool Changed,
        PrototypeCausewayMorningOutcome MorningOutcome,
        int CausewayIntegrity,
        int WetlandHealth,
        int RestorationDueMorning,
        PrototypeCausewayTransitionKind Kind)
    {
        internal static PrototypeCausewayTransitionResult Nightfall(long previousRevision, long revision) =>
            new(previousRevision, revision, PrototypeEventTypes.CausewayNightfallReached, "nightfall", true,
                PrototypeCausewayMorningOutcome.Unresolved, 0, 0, 0, PrototypeCausewayTransitionKind.Nightfall);

        internal static PrototypeCausewayTransitionResult Morning(
            long previousRevision,
            long revision,
            PrototypeCausewayState.CausewayMorningResolution resolution) =>
            new(previousRevision, revision, PrototypeEventTypes.CausewayMorningResolved, resolution.Outcome.ToString(), true,
                resolution.Outcome, resolution.CausewayIntegrity, resolution.WetlandHealth,
                resolution.RestorationDueMorning, PrototypeCausewayTransitionKind.Morning);
    }

    public sealed class PrototypeCausewayDefinition
    {
        public int InitialCausewayIntegrity { get; set; } = 32;
        public int InitialWetlandHealth { get; set; } = 84;
        public int ReservedDryTimber { get; set; } = 2;
        public int InitialCommunityTimber { get; set; } = 1;
        public int InitialStone { get; set; } = 2;
        public int InitialReedBundles { get; set; } = 2;
        public int InitialPlayerLabor { get; set; } = 3;
        public int RequiredCausewayTimber { get; set; } = 2;
        public int RequiredCausewayStone { get; set; } = 1;
        public int RequiredCausewayReeds { get; set; } = 1;
        public int RequiredPlayerLabor { get; set; } = 2;
        public int RequiredShelterTimber { get; set; } = 2;
        public float NightfallHour { get; set; } = 18.0f;
        public float MorningHour { get; set; } = 6.0f;
        public float ScenarioStartHour { get; set; } = 8.0f;
        public PrototypeSerializableVector3 CausewayAnchor { get; set; } = new() { X = -5.0f, Y = 14.0f, Z = 2.0f };
        public PrototypeSerializableVector3 NurseryAnchor { get; set; } = new() { X = -9.0f, Y = 13.0f, Z = 7.0f };
        public PrototypeSerializableVector3 ShelterAnchor { get; set; } = new() { X = 4.0f, Y = 14.0f, Z = -3.0f };
    }

    public sealed class PrototypeCausewayDefinitionSnapshot
    {
        public string Schema { get; set; } = PrototypeCausewayDefinitionContract.Schema;
        public int SchemaVersion { get; set; } = PrototypeCausewayDefinitionContract.SchemaVersion;
        public int InitialCausewayIntegrity { get; set; }
        public int InitialWetlandHealth { get; set; }
        public int ReservedDryTimber { get; set; }
        public int InitialCommunityTimber { get; set; }
        public int InitialStone { get; set; }
        public int InitialReedBundles { get; set; }
        public int InitialPlayerLabor { get; set; }
        public int RequiredCausewayTimber { get; set; }
        public int RequiredCausewayStone { get; set; }
        public int RequiredCausewayReeds { get; set; }
        public int RequiredPlayerLabor { get; set; }
        public int RequiredShelterTimber { get; set; }
        public float NightfallHour { get; set; }
        public float MorningHour { get; set; }
        public float ScenarioStartHour { get; set; }
        public PrototypeSerializableVector3 CausewayAnchor { get; set; }
        public PrototypeSerializableVector3 NurseryAnchor { get; set; }
        public PrototypeSerializableVector3 ShelterAnchor { get; set; }
        public string Digest { get; set; } = string.Empty;
    }

    internal static class PrototypeCausewayDefinitionContract
    {
        internal const string Schema = "societies_causeway_definition";
        internal const int SchemaVersion = 1;

        internal static PrototypeCausewayDefinition Freeze(
            PrototypeCausewayDefinition definition,
            string scenarioId)
        {
            ArgumentNullException.ThrowIfNull(definition);
            Validate(definition, scenarioId);
            return new PrototypeCausewayDefinition
            {
                InitialCausewayIntegrity = definition.InitialCausewayIntegrity,
                InitialWetlandHealth = definition.InitialWetlandHealth,
                ReservedDryTimber = definition.ReservedDryTimber,
                InitialCommunityTimber = definition.InitialCommunityTimber,
                InitialStone = definition.InitialStone,
                InitialReedBundles = definition.InitialReedBundles,
                InitialPlayerLabor = definition.InitialPlayerLabor,
                RequiredCausewayTimber = definition.RequiredCausewayTimber,
                RequiredCausewayStone = definition.RequiredCausewayStone,
                RequiredCausewayReeds = definition.RequiredCausewayReeds,
                RequiredPlayerLabor = definition.RequiredPlayerLabor,
                RequiredShelterTimber = definition.RequiredShelterTimber,
                NightfallHour = definition.NightfallHour,
                MorningHour = definition.MorningHour,
                ScenarioStartHour = definition.ScenarioStartHour,
                CausewayAnchor = definition.CausewayAnchor,
                NurseryAnchor = definition.NurseryAnchor,
                ShelterAnchor = definition.ShelterAnchor
            };
        }

        internal static void Validate(PrototypeCausewayDefinition definition, string scenarioId)
        {
            if (definition.InitialCausewayIntegrity is < 1 or > 99 ||
                definition.InitialWetlandHealth is < 1 or > 100 ||
                definition.ReservedDryTimber <= 0 || definition.InitialCommunityTimber < 0 ||
                definition.InitialStone < 0 || definition.InitialReedBundles < 0 ||
                definition.InitialPlayerLabor <= 0 || definition.RequiredCausewayTimber <= 0 ||
                definition.RequiredCausewayStone <= 0 || definition.RequiredCausewayReeds <= 0 ||
                definition.RequiredPlayerLabor <= 0 || definition.RequiredShelterTimber <= 0 ||
                definition.RequiredShelterTimber > definition.ReservedDryTimber ||
                definition.RequiredCausewayTimber > definition.ReservedDryTimber + definition.InitialCommunityTimber ||
                definition.RequiredCausewayStone > definition.InitialStone ||
                definition.RequiredCausewayReeds > definition.InitialReedBundles ||
                definition.RequiredPlayerLabor > definition.InitialPlayerLabor ||
                !float.IsFinite(definition.NightfallHour) || !float.IsFinite(definition.MorningHour) ||
                !float.IsFinite(definition.ScenarioStartHour) ||
                definition.NightfallHour is < 0.0f or >= 24.0f ||
                definition.MorningHour is < 0.0f or >= 24.0f ||
                definition.ScenarioStartHour is < 0.0f or >= 24.0f ||
                definition.MorningHour >= definition.ScenarioStartHour ||
                definition.ScenarioStartHour >= definition.NightfallHour)
            {
                throw new InvalidOperationException(
                    $"Causeway scenario '{scenarioId}' has invalid material, time, or cost requirements.");
            }

            PrototypeSerializableVector3[] anchors =
            {
                definition.CausewayAnchor, definition.NurseryAnchor, definition.ShelterAnchor
            };
            if (anchors.Any(anchor => !IsFinite(anchor) || !IsInsideVoxelAuthority(anchor)) ||
                anchors[0].Equals(anchors[1]) || anchors[0].Equals(anchors[2]) || anchors[1].Equals(anchors[2]))
            {
                throw new InvalidOperationException(
                    $"Causeway scenario '{scenarioId}' has non-finite, duplicate, or out-of-bounds anchors.");
            }
        }

        internal static void ValidateNewRunStartHour(
            PrototypeCausewayDefinition definition,
            float startHour)
        {
            if (!float.IsFinite(startHour) ||
                startHour < definition.ScenarioStartHour || startHour >= definition.NightfallHour)
            {
                throw new ArgumentOutOfRangeException(nameof(startHour),
                    "A fresh Causeway run must start at or after the scenario start and before nightfall.");
            }
        }

        internal static PrototypeCausewayDefinitionSnapshot CaptureSnapshot(
            PrototypeCausewayDefinition definition)
        {
            PrototypeCausewayDefinition frozen = Freeze(definition, "authority");
            var snapshot = new PrototypeCausewayDefinitionSnapshot
            {
                InitialCausewayIntegrity = frozen.InitialCausewayIntegrity,
                InitialWetlandHealth = frozen.InitialWetlandHealth,
                ReservedDryTimber = frozen.ReservedDryTimber,
                InitialCommunityTimber = frozen.InitialCommunityTimber,
                InitialStone = frozen.InitialStone,
                InitialReedBundles = frozen.InitialReedBundles,
                InitialPlayerLabor = frozen.InitialPlayerLabor,
                RequiredCausewayTimber = frozen.RequiredCausewayTimber,
                RequiredCausewayStone = frozen.RequiredCausewayStone,
                RequiredCausewayReeds = frozen.RequiredCausewayReeds,
                RequiredPlayerLabor = frozen.RequiredPlayerLabor,
                RequiredShelterTimber = frozen.RequiredShelterTimber,
                NightfallHour = frozen.NightfallHour,
                MorningHour = frozen.MorningHour,
                ScenarioStartHour = frozen.ScenarioStartHour,
                CausewayAnchor = frozen.CausewayAnchor,
                NurseryAnchor = frozen.NurseryAnchor,
                ShelterAnchor = frozen.ShelterAnchor
            };
            snapshot.Digest = ComputeDigest(snapshot);
            return snapshot;
        }

        internal static PrototypeCausewayDefinition PrepareFromSnapshot(
            PrototypeCausewayDefinitionSnapshot? snapshot)
        {
            if (snapshot == null || !string.Equals(snapshot.Schema, Schema, StringComparison.Ordinal) ||
                snapshot.SchemaVersion != SchemaVersion || snapshot.Digest == null || snapshot.Digest.Length != 64 ||
                snapshot.Digest.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            {
                throw new InvalidDataException("Causeway definition binding is missing or malformed.");
            }

            var definition = new PrototypeCausewayDefinition
            {
                InitialCausewayIntegrity = snapshot.InitialCausewayIntegrity,
                InitialWetlandHealth = snapshot.InitialWetlandHealth,
                ReservedDryTimber = snapshot.ReservedDryTimber,
                InitialCommunityTimber = snapshot.InitialCommunityTimber,
                InitialStone = snapshot.InitialStone,
                InitialReedBundles = snapshot.InitialReedBundles,
                InitialPlayerLabor = snapshot.InitialPlayerLabor,
                RequiredCausewayTimber = snapshot.RequiredCausewayTimber,
                RequiredCausewayStone = snapshot.RequiredCausewayStone,
                RequiredCausewayReeds = snapshot.RequiredCausewayReeds,
                RequiredPlayerLabor = snapshot.RequiredPlayerLabor,
                RequiredShelterTimber = snapshot.RequiredShelterTimber,
                NightfallHour = snapshot.NightfallHour,
                MorningHour = snapshot.MorningHour,
                ScenarioStartHour = snapshot.ScenarioStartHour,
                CausewayAnchor = snapshot.CausewayAnchor,
                NurseryAnchor = snapshot.NurseryAnchor,
                ShelterAnchor = snapshot.ShelterAnchor
            };
            try
            {
                definition = Freeze(definition, "persisted-definition");
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidDataException("Causeway definition binding contains invalid rules.", exception);
            }
            if (!string.Equals(snapshot.Digest, ComputeDigest(snapshot), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Causeway definition digest does not match its canonical fields.");
            }
            return definition;
        }

        internal static void ValidateBinding(
            PrototypeCausewayDefinition expected,
            PrototypeCausewayDefinitionSnapshot actual)
        {
            PrototypeCausewayDefinition frozenExpected = Freeze(expected, "active-scenario");
            _ = PrepareFromSnapshot(actual);
            string expectedDigest = CaptureSnapshot(frozenExpected).Digest;
            if (!string.Equals(expectedDigest, actual.Digest, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Causeway snapshot definition does not match the active scenario definition.");
            }
        }

        internal static PrototypeCausewayDefinitionSnapshot CloneSnapshot(
            PrototypeCausewayDefinitionSnapshot snapshot) => new()
        {
            Schema = snapshot.Schema,
            SchemaVersion = snapshot.SchemaVersion,
            InitialCausewayIntegrity = snapshot.InitialCausewayIntegrity,
            InitialWetlandHealth = snapshot.InitialWetlandHealth,
            ReservedDryTimber = snapshot.ReservedDryTimber,
            InitialCommunityTimber = snapshot.InitialCommunityTimber,
            InitialStone = snapshot.InitialStone,
            InitialReedBundles = snapshot.InitialReedBundles,
            InitialPlayerLabor = snapshot.InitialPlayerLabor,
            RequiredCausewayTimber = snapshot.RequiredCausewayTimber,
            RequiredCausewayStone = snapshot.RequiredCausewayStone,
            RequiredCausewayReeds = snapshot.RequiredCausewayReeds,
            RequiredPlayerLabor = snapshot.RequiredPlayerLabor,
            RequiredShelterTimber = snapshot.RequiredShelterTimber,
            NightfallHour = snapshot.NightfallHour,
            MorningHour = snapshot.MorningHour,
            ScenarioStartHour = snapshot.ScenarioStartHour,
            CausewayAnchor = snapshot.CausewayAnchor,
            NurseryAnchor = snapshot.NurseryAnchor,
            ShelterAnchor = snapshot.ShelterAnchor,
            Digest = snapshot.Digest
        };

        internal static bool SnapshotsEqual(
            PrototypeCausewayDefinitionSnapshot? first,
            PrototypeCausewayDefinitionSnapshot? second) =>
            first != null && second != null &&
            string.Equals(first.Schema, second.Schema, StringComparison.Ordinal) &&
            first.SchemaVersion == second.SchemaVersion &&
            string.Equals(first.Digest, second.Digest, StringComparison.Ordinal) &&
            first.InitialCausewayIntegrity == second.InitialCausewayIntegrity &&
            first.InitialWetlandHealth == second.InitialWetlandHealth &&
            first.ReservedDryTimber == second.ReservedDryTimber &&
            first.InitialCommunityTimber == second.InitialCommunityTimber &&
            first.InitialStone == second.InitialStone &&
            first.InitialReedBundles == second.InitialReedBundles &&
            first.InitialPlayerLabor == second.InitialPlayerLabor &&
            first.RequiredCausewayTimber == second.RequiredCausewayTimber &&
            first.RequiredCausewayStone == second.RequiredCausewayStone &&
            first.RequiredCausewayReeds == second.RequiredCausewayReeds &&
            first.RequiredPlayerLabor == second.RequiredPlayerLabor &&
            first.RequiredShelterTimber == second.RequiredShelterTimber &&
            BitConverter.SingleToInt32Bits(first.NightfallHour) == BitConverter.SingleToInt32Bits(second.NightfallHour) &&
            BitConverter.SingleToInt32Bits(first.MorningHour) == BitConverter.SingleToInt32Bits(second.MorningHour) &&
            BitConverter.SingleToInt32Bits(first.ScenarioStartHour) == BitConverter.SingleToInt32Bits(second.ScenarioStartHour) &&
            VectorEqual(first.CausewayAnchor, second.CausewayAnchor) &&
            VectorEqual(first.NurseryAnchor, second.NurseryAnchor) &&
            VectorEqual(first.ShelterAnchor, second.ShelterAnchor);

        private static string ComputeDigest(PrototypeCausewayDefinitionSnapshot snapshot)
        {
            string canonical = string.Join("|", new[]
            {
                snapshot.Schema, snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                snapshot.InitialCausewayIntegrity.ToString(CultureInfo.InvariantCulture),
                snapshot.InitialWetlandHealth.ToString(CultureInfo.InvariantCulture),
                snapshot.ReservedDryTimber.ToString(CultureInfo.InvariantCulture),
                snapshot.InitialCommunityTimber.ToString(CultureInfo.InvariantCulture),
                snapshot.InitialStone.ToString(CultureInfo.InvariantCulture),
                snapshot.InitialReedBundles.ToString(CultureInfo.InvariantCulture),
                snapshot.InitialPlayerLabor.ToString(CultureInfo.InvariantCulture),
                snapshot.RequiredCausewayTimber.ToString(CultureInfo.InvariantCulture),
                snapshot.RequiredCausewayStone.ToString(CultureInfo.InvariantCulture),
                snapshot.RequiredCausewayReeds.ToString(CultureInfo.InvariantCulture),
                snapshot.RequiredPlayerLabor.ToString(CultureInfo.InvariantCulture),
                snapshot.RequiredShelterTimber.ToString(CultureInfo.InvariantCulture),
                FloatBits(snapshot.NightfallHour), FloatBits(snapshot.MorningHour), FloatBits(snapshot.ScenarioStartHour),
                VectorBits(snapshot.CausewayAnchor), VectorBits(snapshot.NurseryAnchor), VectorBits(snapshot.ShelterAnchor)
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        private static bool IsFinite(PrototypeSerializableVector3 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        private static bool IsInsideVoxelAuthority(PrototypeSerializableVector3 value) =>
            value.X >= VoxelWorldModule.MinX && value.X < VoxelWorldModule.MaxXExclusive &&
            value.Y >= VoxelWorldModule.MinY && value.Y < VoxelWorldModule.MaxYExclusive &&
            value.Z >= VoxelWorldModule.MinZ && value.Z < VoxelWorldModule.MaxZExclusive;

        private static bool VectorEqual(PrototypeSerializableVector3 first, PrototypeSerializableVector3 second) =>
            BitConverter.SingleToInt32Bits(first.X) == BitConverter.SingleToInt32Bits(second.X) &&
            BitConverter.SingleToInt32Bits(first.Y) == BitConverter.SingleToInt32Bits(second.Y) &&
            BitConverter.SingleToInt32Bits(first.Z) == BitConverter.SingleToInt32Bits(second.Z);

        private static string FloatBits(float value) =>
            BitConverter.SingleToInt32Bits(value).ToString(CultureInfo.InvariantCulture);

        private static string VectorBits(PrototypeSerializableVector3 value) =>
            $"{FloatBits(value.X)},{FloatBits(value.Y)},{FloatBits(value.Z)}";
    }

    public sealed class PrototypeCausewayStateSnapshot
    {
        public long Revision { get; set; }
        public int MigrationSourceSchemaVersion { get; set; }
        public int CausewayIntegrity { get; set; }
        public int WetlandHealth { get; set; }
        public int ReservedDryTimber { get; set; }
        public int CommunityTimber { get; set; }
        public int Stone { get; set; }
        public int ReedBundles { get; set; }
        public int AvailablePlayerLabor { get; set; }
        public int PlayerLabor { get; set; }
        public int ShelterTimberSpent { get; set; }
        public int ShelterLaborSpent { get; set; }
        public int CausewayTimberCommitted { get; set; }
        public int CausewayStoneCommitted { get; set; }
        public int CausewayReedsCommitted { get; set; }
        public string WaterControl { get; set; } = PrototypeCausewayWaterControl.Unselected.ToString();
        public bool NightfallReached { get; set; }
        public bool MorningResolved { get; set; }
        public string MorningOutcome { get; set; } = PrototypeCausewayMorningOutcome.Unresolved.ToString();
        public bool PlayerShelterRepaired { get; set; }
        public bool RestorationRequired { get; set; }
        public int RestorationDueMorning { get; set; }
        public PrototypeCausewayDefinitionSnapshot? Definition { get; set; }
    }

    public readonly record struct PrototypeCausewayProjection(
        long Revision, int CausewayIntegrity, int WetlandHealth, int ReservedDryTimber, int CommunityTimber, int Stone,
        int ReedBundles, int PlayerLabor, int AvailablePlayerLabor, int ShelterTimberSpent, int ShelterLaborSpent,
        int CausewayTimberCommitted, int CausewayStoneCommitted, int CausewayReedsCommitted,
        PrototypeCausewayWaterControl WaterControl, bool NightfallReached, bool MorningResolved,
        PrototypeCausewayMorningOutcome MorningOutcome, bool PlayerShelterRepaired, bool RestorationRequired,
        int RestorationDueMorning, bool ShelterRepairAvailable, float CurrentHour, string TimePhase,
        Vector3 CausewayAnchor, Vector3 NurseryAnchor, Vector3 ShelterAnchor);
}
