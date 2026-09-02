using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeCausewayStateTests
    {
        [Fact]
        public void CausewayScenario_DeclaresClosedCitizenFreeWorldFacts()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);

            PrototypeCausewayProjection causeway = Assert.IsType<PrototypeCausewayProjection>(session.Causeway);
            Assert.Empty(session.Workers);
            Assert.Equal(32, causeway.CausewayIntegrity);
            Assert.Equal(84, causeway.WetlandHealth);
            Assert.Equal(2, causeway.ReservedDryTimber);
            Assert.True(causeway.ShelterRepairAvailable);
            Assert.NotEqual(causeway.CausewayAnchor, causeway.NurseryAnchor);
            Assert.NotEqual(causeway.CausewayAnchor, causeway.ShelterAnchor);
        }

        [Theory]
        [InlineData(18.0f)]
        [InlineData(2.0f)]
        [InlineData(7.0f)]
        public void InitializeRejectsInvalidNewRunPhaseBeforeMutatingAnExistingSession(float invalidStartHour)
        {
            PrototypeRuntimeSession fresh = CreateSession();
            Assert.Throws<ArgumentOutOfRangeException>(() => fresh.Initialize(invalidStartHour));
            fresh.Initialize(8.0f);
            Assert.Equal(0, Assert.IsType<PrototypeCausewayProjection>(fresh.Causeway).Revision);

            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0,
                Kind = PrototypeCausewayCommandKind.ContributeCommunityTimber
            }).Accepted);
            string before = PrototypePersistenceService.SerializeSnapshot(
                session.CaptureSnapshot(Vector3.Zero));
            string[] eventsBefore = session.EventLog.Entries.Select(EventIdentity).ToArray();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Initialize(invalidStartHour));

            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(
                session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(eventsBefore, session.EventLog.Entries.Select(EventIdentity));
        }

        [Fact]
        public void CausewayRulesAreFrozenAfterSessionConstructionAndChangedCatalogCannotRestore()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("snow_globe_voxel");
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);
            PrototypeRuntimeSnapshot original = session.CaptureSnapshot(Vector3.Zero);
            string originalJson = PrototypePersistenceService.SerializeSnapshot(original);

            scenario.Causeway!.InitialCausewayIntegrity = 91;
            scenario.Causeway.RequiredShelterTimber = 1;
            PrototypeSerializableVector3 changedAnchor = scenario.Causeway.CausewayAnchor;
            changedAnchor.X = 33.0f;
            scenario.Causeway.CausewayAnchor = changedAnchor;

            PrototypeCausewayProjection projection = Assert.IsType<PrototypeCausewayProjection>(session.Causeway);
            Assert.Equal(32, projection.CausewayIntegrity);
            Assert.Equal(-5.0f, projection.CausewayAnchor.X);
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0,
                Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            }).Accepted);
            Assert.Equal(0, Assert.IsType<PrototypeCausewayProjection>(session.Causeway).ReservedDryTimber);

            PrototypeRuntimeSession changedCatalog = new(scenario, bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            Assert.Throws<InvalidDataException>(() => changedCatalog.ApplySnapshot(
                PrototypePersistenceService.DeserializeSnapshot(originalJson)));

            PrototypeRuntimeSession matchingCatalog = CreateSession();
            matchingCatalog.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(originalJson));
            Assert.Equal(originalJson, PrototypePersistenceService.SerializeSnapshot(
                matchingCatalog.CaptureSnapshot(Vector3.Zero)));
        }

        [Theory]
        [InlineData("nonfinite_causeway")]
        [InlineData("nonfinite_nursery")]
        [InlineData("nonfinite_shelter")]
        [InlineData("duplicate_causeway_nursery")]
        [InlineData("duplicate_causeway_shelter")]
        [InlineData("duplicate_nursery_shelter")]
        [InlineData("outside_x")]
        [InlineData("outside_y")]
        [InlineData("outside_z")]
        public void CausewayCatalogRejectsInvalidAuthoritativeAnchors(string invalidity)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeCausewayDefinition causeway = bundle.Scenarios.Resolve("snow_globe_voxel").Causeway!;
            switch (invalidity)
            {
                case "nonfinite_causeway": causeway.CausewayAnchor = WithX(causeway.CausewayAnchor, float.NaN); break;
                case "nonfinite_nursery": causeway.NurseryAnchor = WithY(causeway.NurseryAnchor, float.PositiveInfinity); break;
                case "nonfinite_shelter": causeway.ShelterAnchor = WithZ(causeway.ShelterAnchor, float.NegativeInfinity); break;
                case "duplicate_causeway_nursery": causeway.NurseryAnchor = causeway.CausewayAnchor; break;
                case "duplicate_causeway_shelter": causeway.ShelterAnchor = causeway.CausewayAnchor; break;
                case "duplicate_nursery_shelter": causeway.ShelterAnchor = causeway.NurseryAnchor; break;
                case "outside_x": causeway.CausewayAnchor = WithX(causeway.CausewayAnchor, VoxelWorldModule.MaxXExclusive); break;
                case "outside_y": causeway.NurseryAnchor = WithY(causeway.NurseryAnchor, VoxelWorldModule.MaxYExclusive); break;
                case "outside_z": causeway.ShelterAnchor = WithZ(causeway.ShelterAnchor, VoxelWorldModule.MinZ - 0.01f); break;
                default: throw new ArgumentOutOfRangeException(nameof(invalidity));
            }

            Assert.Throws<InvalidOperationException>(() => bundle.Scenarios.Validate());
        }

        [Fact]
        public void CausewayCatalogAcceptsFiniteDistinctAnchorsInsideVoxelAuthority()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeCausewayDefinition causeway = bundle.Scenarios.Resolve("snow_globe_voxel").Causeway!;
            causeway.CausewayAnchor = new PrototypeSerializableVector3
            {
                X = VoxelWorldModule.MinX, Y = VoxelWorldModule.MinY, Z = VoxelWorldModule.MinZ
            };
            causeway.NurseryAnchor = new PrototypeSerializableVector3
            {
                X = VoxelWorldModule.MaxXExclusive - 0.01f,
                Y = VoxelWorldModule.MaxYExclusive - 0.01f,
                Z = VoxelWorldModule.MaxZExclusive - 0.01f
            };
            causeway.ShelterAnchor = new PrototypeSerializableVector3 { X = 0.0f, Y = 1.0f, Z = 0.0f };

            bundle.Scenarios.Validate();
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("duplicate")]
        [InlineData("wrong_order")]
        public void SchemaV12SnapshotCausewayObjectRequiresExactOrdinalFieldSet(string tamper)
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            string json = PrototypePersistenceService.SerializeSnapshot(
                session.CaptureSnapshot(Vector3.Zero));

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeSnapshot(TamperCausewayObject(json, tamper)));
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("duplicate")]
        [InlineData("wrong_order")]
        public void SchemaV12RunSummaryCausewayObjectRequiresExactOrdinalFieldSet(string tamper)
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            PrototypeRunSummary summary = PrototypeRunSummaryBuilder.Build(
                snapshot, session.EventLog.Entries, session.RunStartHour);
            string json = PrototypePersistenceService.SerializeRunSummary(summary);

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeRunSummary(TamperCausewayObject(json, tamper)));
        }

        [Fact]
        public void ReservedTimber_CanBeKeptForRealRepairOrSacrificedExactlyOnce()
        {
            PrototypeRuntimeSession keep = CreateSession();
            keep.Initialize(8.0f);
            Assert.True(keep.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            }).Accepted);
            PrototypeCausewayProjection repaired = Assert.IsType<PrototypeCausewayProjection>(keep.Causeway);
            Assert.True(repaired.PlayerShelterRepaired);
            Assert.Equal(0, repaired.ReservedDryTimber);
            Assert.False(repaired.ShelterRepairAvailable);

            PrototypeRuntimeSession sacrifice = CreateSession();
            sacrifice.Initialize(8.0f);
            Assert.True(sacrifice.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.ContributeReservedDryTimber, Quantity = 1
            }).Accepted);
            PrototypeCausewayProjection contributed = Assert.IsType<PrototypeCausewayProjection>(sacrifice.Causeway);
            Assert.Equal(1, contributed.ReservedDryTimber);
            Assert.Equal(1, contributed.CausewayTimberCommitted);
            Assert.False(contributed.ShelterRepairAvailable);
            Assert.False(sacrifice.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.ContributeReservedDryTimber, Quantity = 1
            }).Accepted);
            Assert.Equal("stale_revision", sacrifice.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.ContributeReservedDryTimber, Quantity = 1
            }).Rejection);
        }

        [Fact]
        public void RepairedShelter_RoundTripsAndReplaysWithExplicitSpentCustody()
        {
            PrototypeRuntimeSession original = CreateSession();
            original.Initialize(8.0f);
            Assert.True(original.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            }).Accepted);

            PrototypeRuntimeSnapshot captured = original.CaptureSnapshot(Vector3.Zero);
            Assert.Equal(2, captured.Causeway!.ShelterTimberSpent);
            Assert.Equal(1, captured.Causeway.ShelterLaborSpent);
            string serialized = PrototypePersistenceService.SerializeSnapshot(captured);

            PrototypeRuntimeSession restored = CreateSession();
            restored.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(serialized));
            restored.RestoreArtifacts(original.EventLog.Entries, null);

            PrototypeRuntimeSession replayed = CreateSession();
            replayed.Initialize(8.0f);
            Assert.True(replayed.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            }).Accepted);

            Assert.Equal(serialized, PrototypePersistenceService.SerializeSnapshot(restored.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(serialized, PrototypePersistenceService.SerializeSnapshot(replayed.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(
                original.EventLog.Entries.Select(EventIdentity),
                restored.EventLog.Entries.Select(EventIdentity));
            Assert.Equal(
                original.EventLog.Entries.Select(EventIdentity),
                replayed.EventLog.Entries.Select(EventIdentity));
            PrototypeCausewayProjection projection = Assert.IsType<PrototypeCausewayProjection>(restored.Causeway);
            Assert.Equal(2, projection.ShelterTimberSpent);
            Assert.Equal(1, projection.ShelterLaborSpent);
        }

        [Fact]
        public void PlayerOwnedCommands_RejectNonPlayerActorBeforeAppend()
        {
            foreach (PrototypeCausewayCommandKind kind in new[]
            {
                PrototypeCausewayCommandKind.ContributeReservedDryTimber,
                PrototypeCausewayCommandKind.ContributeLabor,
                PrototypeCausewayCommandKind.RepairPlayerShelter
            })
            {
                PrototypeRuntimeSession session = CreateSession();
                session.Initialize(8.0f);
                string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));

                PrototypeCausewayCommandResult result = session.ExecuteCausewayCommand(new PrototypeCausewayCommand
                {
                    ActorId = "invalid-actor", ExpectedRevision = 0, Kind = kind, Quantity = 1
                });

                Assert.False(result.Accepted);
                Assert.Equal("invalid_actor", result.Rejection);
                Assert.Empty(session.EventLog.Entries);
                Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            }
        }

        [Fact]
        public void Commands_ValidateBeforeAppendAndBoundPlayerLabor()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            int eventsBefore = session.EventLog.Entries.Count;

            PrototypeCausewayCommandResult rejected = session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.ContributeLabor, Quantity = 4
            });
            Assert.False(rejected.Accepted);
            Assert.Equal("insufficient_labor", rejected.Rejection);
            Assert.Equal(eventsBefore, session.EventLog.Entries.Count);
            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));

            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.ContributeLabor, Quantity = 2
            }).Accepted);
            Assert.Equal(new[] { "causeway.labor.committed" }, session.EventLog.Entries.Select(entry => entry.EventType));
            Assert.Equal(1, Assert.IsType<PrototypeCausewayProjection>(session.Causeway).AvailablePlayerLabor);
        }

        [Theory]
        [InlineData(PrototypeCausewayWaterControl.ProtectNursery, PrototypeCausewayMorningOutcome.StagedProtection, 68, 76, 2)]
        [InlineData(PrototypeCausewayWaterControl.DrawDownWetland, PrototypeCausewayMorningOutcome.DrawdownRepair, 76, 59, 1)]
        public void StagedMorningMatrix_ConsumesMaterialLaborAndCarriesEcologicalAndFutureRepairCost(
            PrototypeCausewayWaterControl waterControl,
            PrototypeCausewayMorningOutcome expectedOutcome,
            int expectedIntegrity,
            int expectedWetlandHealth,
            int expectedRestorationMorning)
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            PrototypeCausewayCommandKind[] orderedCommands =
            {
                PrototypeCausewayCommandKind.ContributeReservedDryTimber,
                PrototypeCausewayCommandKind.ContributeCommunityTimber,
                PrototypeCausewayCommandKind.ContributeStone,
                PrototypeCausewayCommandKind.ContributeReedBundles,
                PrototypeCausewayCommandKind.ContributeLabor
            };
            foreach ((PrototypeCausewayCommandKind kind, int index) in orderedCommands.Select((kind, index) => (kind, index)))
            {
                Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
                {
                    ActorId = "player", ExpectedRevision = index, Kind = kind, Quantity = kind == PrototypeCausewayCommandKind.ContributeLabor ? 2 : 1
                }).Accepted);
            }
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 5, Kind = PrototypeCausewayCommandKind.SelectWaterControl, WaterControl = waterControl
            }).Accepted);

            AdvanceToMorning(session);
            PrototypeCausewayProjection morning = Assert.IsType<PrototypeCausewayProjection>(session.Causeway);
            Assert.True(morning.MorningResolved);
            Assert.Equal(expectedOutcome, morning.MorningOutcome);
            Assert.Equal(expectedIntegrity, morning.CausewayIntegrity);
            Assert.Equal(expectedWetlandHealth, morning.WetlandHealth);
            Assert.Equal(1, morning.ReservedDryTimber);
            Assert.Equal(0, morning.CommunityTimber);
            Assert.Equal(1, morning.Stone);
            Assert.Equal(1, morning.ReedBundles);
            Assert.Equal(2, morning.PlayerLabor);
            Assert.True(morning.RestorationRequired);
            Assert.Equal(expectedRestorationMorning, morning.RestorationDueMorning);
            Assert.Equal(new[]
            {
                "causeway.timber.sacrificed", "causeway.material.committed", "causeway.material.committed",
                "causeway.material.committed", "causeway.labor.committed", "causeway.water_control.selected",
                "causeway.nightfall.reached", "causeway.morning.resolved"
            }, session.EventLog.Entries.Select(entry => entry.EventType));
        }

        [Fact]
        public void ZeroInputAndSaveRestoreReplay_ResolveTheSameCostlyMorning()
        {
            PrototypeRuntimeSession uninterrupted = CreateSession();
            uninterrupted.Initialize(8.0f);
            AdvanceToMorning(uninterrupted);
            PrototypeCausewayProjection morning = Assert.IsType<PrototypeCausewayProjection>(uninterrupted.Causeway);
            Assert.True(morning.MorningResolved);
            Assert.Equal(PrototypeCausewayMorningOutcome.CausewayBreach, morning.MorningOutcome);
            Assert.True(morning.RestorationRequired);
            Assert.True(morning.CausewayIntegrity < 32 && morning.WetlandHealth < 84);

            PrototypeRuntimeSession saved = CreateSession();
            saved.Initialize(8.0f);
            for (int index = 0; index < 22; index++) saved.Advance(1.0f, 48.0f);
            PrototypeRuntimeSnapshot checkpoint = PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(saved.CaptureSnapshot(Vector3.Zero)));
            PrototypeRuntimeSession resumed = CreateSession();
            resumed.ApplySnapshot(checkpoint);
            resumed.RestoreArtifacts(saved.EventLog.Entries, null);
            for (int index = 0; index < 22; index++) resumed.Advance(1.0f, 48.0f);

            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(uninterrupted.CaptureSnapshot(Vector3.Zero)),
                PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(
                uninterrupted.EventLog.Entries.Select(entry => entry.EventType),
                resumed.EventLog.Entries.Select(entry => entry.EventType));
        }

        [Fact]
        public void MalformedCausewayRestore_IsPrepareBeforeCommitAtomic()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            PrototypeRuntimeSnapshot valid = session.CaptureSnapshot(Vector3.Zero);
            string before = PrototypePersistenceService.SerializeSnapshot(valid);
            PrototypeRuntimeSnapshot malformed = PrototypePersistenceService.DeserializeSnapshot(before);
            malformed.Causeway!.ReservedDryTimber = 99;

            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(malformed));
            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
        }

        [Fact]
        public void RepairedShelterRestore_RejectsMalformedSpentCustodyAtomically()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            }).Accepted);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));

            foreach (Action<PrototypeCausewayStateSnapshot> tamper in new Action<PrototypeCausewayStateSnapshot>[]
            {
                causeway => causeway.ShelterTimberSpent--,
                causeway => causeway.ShelterLaborSpent = 0,
                causeway => causeway.PlayerShelterRepaired = false
            })
            {
                PrototypeRuntimeSnapshot malformed = PrototypePersistenceService.DeserializeSnapshot(before);
                tamper(malformed.Causeway!);
                Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(malformed));
                Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            }
        }

        [Fact]
        public void RevisionExhaustion_RejectsRestoreAndCommandWithoutEventOrStateMutation()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            string initial = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            PrototypeRuntimeSnapshot unrestoreable = PrototypePersistenceService.DeserializeSnapshot(initial);
            unrestoreable.Causeway!.Revision = long.MaxValue;
            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(unrestoreable));
            Assert.Equal(initial, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));

            PrototypeRuntimeSnapshot exhausted = PrototypePersistenceService.DeserializeSnapshot(initial);
            exhausted.Causeway!.Revision = PrototypeCausewayState.MaximumEventCount + 1;
            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(exhausted));
            PrototypeCausewayState state = Assert.IsType<PrototypeCausewayState>(
                typeof(PrototypeRuntimeSession).GetField("_causeway", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session));
            typeof(PrototypeCausewayState).GetProperty(nameof(PrototypeCausewayState.Revision))!.SetValue(state, PrototypeCausewayState.MaximumEventCount);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            PrototypeCausewayCommandResult rejected = session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = PrototypeCausewayState.MaximumEventCount,
                Kind = PrototypeCausewayCommandKind.ContributeReservedDryTimber, Quantity = 1
            });
            Assert.False(rejected.Accepted);
            Assert.Equal("causeway_history_full", rejected.Rejection);
            Assert.Empty(session.EventLog.Entries);
            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
        }

        [Fact]
        public void ClockInputsRejectBeforeMutationAndCrossedThresholdsCommitInOrderOnce()
        {
            foreach ((float tickSeconds, float daySeconds) in new[]
            {
                (float.NaN, 24.0f), (1.0f, float.NaN), (0.0f, 24.0f), (1.0f, 0.0f), (-1.0f, 24.0f)
            })
            {
                PrototypeRuntimeSession invalid = CreateSession();
                invalid.Initialize(17.0f);
                string before = PrototypePersistenceService.SerializeSnapshot(invalid.CaptureSnapshot(Vector3.Zero));
                Assert.Throws<ArgumentOutOfRangeException>(() => invalid.Advance(tickSeconds, daySeconds));
                Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(invalid.CaptureSnapshot(Vector3.Zero)));
                Assert.Empty(invalid.EventLog.Entries);
            }

            PrototypeRuntimeSession exact = CreateSession();
            exact.Initialize(17.0f);
            exact.Advance(1.0f, 24.0f);
            PrototypeCausewayProjection exactNightfall = Assert.IsType<PrototypeCausewayProjection>(exact.Causeway);
            Assert.True(exactNightfall.NightfallReached);
            Assert.False(exactNightfall.ShelterRepairAvailable);
            Assert.Equal(new[] { PrototypeEventTypes.CausewayNightfallReached }, exact.EventLog.Entries.Select(entry => entry.EventType));

            PrototypeRuntimeSession both = CreateSession();
            both.Initialize(17.0f);
            both.Advance(14.0f, 24.0f);
            Assert.Equal(
                new[] { PrototypeEventTypes.CausewayNightfallReached, PrototypeEventTypes.CausewayMorningResolved },
                both.EventLog.Entries.Select(entry => entry.EventType));
            Assert.True(Assert.IsType<PrototypeCausewayProjection>(both.Causeway).MorningResolved);
            both.Advance(24.0f, 24.0f);
            Assert.Equal(2, both.EventLog.Entries.Count);

            PrototypeRuntimeSession fullDay = CreateSession();
            fullDay.Initialize(8.0f);
            fullDay.Advance(24.0f, 24.0f);
            Assert.Equal(
                new[] { PrototypeEventTypes.CausewayNightfallReached, PrototypeEventTypes.CausewayMorningResolved },
                fullDay.EventLog.Entries.Select(entry => entry.EventType));
        }

        [Fact]
        public void PreparedTimeTransitions_DoNotMutateUntilCommitted()
        {
            PrototypeCausewayState state = new(new PrototypeCausewayDefinition());
            string before = JsonSerializer.Serialize(state.CaptureSnapshot());

            IReadOnlyList<PrototypeCausewayTransitionResult> transitions = state.PrepareAdvance(17.0, 14.0);

            Assert.Equal(2, transitions.Count);
            Assert.Equal(PrototypeEventTypes.CausewayNightfallReached, transitions[0].EventType);
            Assert.Equal(PrototypeEventTypes.CausewayMorningResolved, transitions[1].EventType);
            Assert.Equal(before, JsonSerializer.Serialize(state.CaptureSnapshot()));
        }

        [Fact]
        public void NightfallDeadline_RejectsCommandsAtAndAfterBoundaryAcrossRestore()
        {
            PrototypeRuntimeSession beforeNightfall = CreateSession();
            beforeNightfall.Initialize(17.0f);
            Assert.True(beforeNightfall.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.ContributeStone
            }).Accepted);

            PrototypeRuntimeSession deadline = CreateSession();
            deadline.Initialize(17.0f);
            deadline.Advance(1.0f, 24.0f);
            string atBoundary = PrototypePersistenceService.SerializeSnapshot(deadline.CaptureSnapshot(Vector3.Zero));
            int eventsBefore = deadline.EventLog.Entries.Count;
            PrototypeCausewayCommandResult rejected = deadline.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 1, Kind = PrototypeCausewayCommandKind.ContributeStone
            });
            Assert.False(rejected.Accepted);
            Assert.Equal("nightfall_deadline_passed", rejected.Rejection);
            Assert.Equal(eventsBefore, deadline.EventLog.Entries.Count);
            Assert.Equal(atBoundary, PrototypePersistenceService.SerializeSnapshot(deadline.CaptureSnapshot(Vector3.Zero)));

            PrototypeRuntimeSession resumed = CreateSession();
            resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(atBoundary));
            rejected = resumed.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 1, Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            });
            Assert.False(rejected.Accepted);
            Assert.Equal("nightfall_deadline_passed", rejected.Rejection);
            Assert.Empty(resumed.EventLog.Entries);
        }

        [Fact]
        public void V11AcceptedSceneSaveMigratesToInitialV12CausewayWithoutChangingVoxelState()
        {
            PrototypeRuntimeSession source = CreateSession();
            source.Initialize(8.0f);
            PrototypeRuntimeSnapshot native = source.CaptureSnapshot(Vector3.Zero);
            string json = PrototypePersistenceService.SerializeSnapshot(native);
            using JsonDocument document = JsonDocument.Parse(json);
            Dictionary<string, object?> legacy = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
            legacy[nameof(PrototypeRuntimeSnapshot.SchemaVersion)] = 11;
            legacy.Remove(nameof(PrototypeRuntimeSnapshot.Causeway));
            PrototypeRuntimeSnapshot v11 = PrototypePersistenceService.DeserializeSnapshot(JsonSerializer.Serialize(legacy));

            PrototypeRuntimeSession migrated = CreateSession();
            migrated.ApplySnapshot(v11);
            PrototypeRuntimeSnapshot upgraded = migrated.CaptureSnapshot(Vector3.Zero);

            Assert.Equal(12, upgraded.SchemaVersion);
            Assert.NotNull(upgraded.Causeway);
            Assert.Equal(0, upgraded.Causeway!.Revision);
            Assert.Equal(native.WorldHash, upgraded.WorldHash);
            Assert.Equal(native.VoxelWorld!.RootHash, upgraded.VoxelWorld!.RootHash);
            Assert.Equal(native.Construction!.Revision, upgraded.Construction!.Revision);
            Assert.Equal(native.Inventory, upgraded.Inventory);
        }

        [Theory]
        [InlineData(18.0f, true, false)]
        [InlineData(2.0f, true, false)]
        [InlineData(7.0f, true, true)]
        public void V11MigrationDerivesDeadlinePhaseAndProducesRestorableNativeV12(
            float currentHour,
            bool expectedNightfall,
            bool expectedMorning)
        {
            PrototypeRuntimeSession source = CreateSession();
            source.Initialize(8.0f);
            PrototypeRuntimeSnapshot v11 = ToV11(source.CaptureSnapshot(Vector3.Zero));
            v11.CurrentHour = currentHour;

            PrototypeRuntimeSession migrated = CreateSession();
            migrated.ApplySnapshot(v11);
            PrototypeCausewayProjection projection = Assert.IsType<PrototypeCausewayProjection>(migrated.Causeway);
            Assert.Equal(expectedNightfall, projection.NightfallReached);
            Assert.Equal(expectedMorning, projection.MorningResolved);
            Assert.False(projection.ShelterRepairAvailable);
            Assert.False(migrated.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = projection.Revision,
                Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            }).Accepted);

            PrototypeRuntimeSnapshot v12 = migrated.CaptureSnapshot(Vector3.Zero);
            Assert.Equal(11, v12.Causeway!.MigrationSourceSchemaVersion);
            PrototypeRuntimeSession restored = CreateSession();
            restored.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(v12)));
            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(v12),
                PrototypePersistenceService.SerializeSnapshot(restored.CaptureSnapshot(Vector3.Zero)));
        }

        [Theory]
        [InlineData(18.0f)]
        [InlineData(2.0f)]
        [InlineData(7.0f)]
        public void NativeV12RestoreRejectsPhaseThatContradictsCurrentHourAtomically(float currentHour)
        {
            PrototypeRuntimeSession source = CreateSession();
            source.Initialize(8.0f);
            string serialized = PrototypePersistenceService.SerializeSnapshot(
                source.CaptureSnapshot(Vector3.Zero));
            using JsonDocument document = JsonDocument.Parse(serialized);
            Dictionary<string, object?> tampered = JsonSerializer.Deserialize<Dictionary<string, object?>>(serialized)!;
            tampered[nameof(PrototypeRuntimeSnapshot.CurrentHour)] = currentHour;
            PrototypeRuntimeSnapshot inconsistent = PrototypePersistenceService.DeserializeSnapshot(
                JsonSerializer.Serialize(tampered));

            PrototypeRuntimeSession target = CreateSession();
            target.Initialize(8.0f);
            string before = PrototypePersistenceService.SerializeSnapshot(target.CaptureSnapshot(Vector3.Zero));
            Assert.Throws<InvalidDataException>(() => target.ApplySnapshot(inconsistent));
            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(target.CaptureSnapshot(Vector3.Zero)));
            Assert.Empty(target.EventLog.Entries);
        }

        [Fact]
        public void RepeatedWaterSelectionIsIdempotentlyBoundedAndTheSixtyFifthAttemptIsAtomic()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0,
                Kind = PrototypeCausewayCommandKind.SelectWaterControl,
                WaterControl = PrototypeCausewayWaterControl.ProtectNursery
            }).Accepted);

            for (int attempt = 2; attempt <= 65; attempt++)
            {
                string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
                int eventsBefore = session.EventLog.Entries.Count;
                PrototypeCausewayCommandResult rejected = session.ExecuteCausewayCommand(new PrototypeCausewayCommand
                {
                    ActorId = "player", ExpectedRevision = 1,
                    Kind = PrototypeCausewayCommandKind.SelectWaterControl,
                    WaterControl = attempt % 2 == 0
                        ? PrototypeCausewayWaterControl.DrawDownWetland
                        : PrototypeCausewayWaterControl.ProtectNursery
                });
                Assert.False(rejected.Accepted);
                Assert.Equal("water_control_already_selected", rejected.Rejection);
                Assert.Equal(eventsBefore, session.EventLog.Entries.Count);
                Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            }

            Assert.Single(session.EventLog.Entries);
        }

        [Fact]
        public void RevisionBoundaryAllowsTheLastPersistableRevisionAndNoFurtherAdvance()
        {
            Assert.True(PrototypeCausewayState.TryGetNextRevision(
                PrototypeCausewayState.MaximumEventCount - 1, out long last));
            Assert.Equal(PrototypeCausewayState.MaximumEventCount, last);
            Assert.False(PrototypeCausewayState.TryGetNextRevision(last, out _));

            PrototypeCausewayState state = new(new PrototypeCausewayDefinition());
            typeof(PrototypeCausewayState).GetProperty(nameof(PrototypeCausewayState.Revision))!
                .SetValue(state, PrototypeCausewayState.MaximumEventCount - 1);
            PrototypeCausewayCommandResult accepted = state.Execute(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = PrototypeCausewayState.MaximumEventCount - 1,
                Kind = PrototypeCausewayCommandKind.SelectWaterControl,
                WaterControl = PrototypeCausewayWaterControl.ProtectNursery
            });
            Assert.True(accepted.Accepted);
            state.Commit(accepted);
            PrototypeCausewayStateSnapshot snapshot = state.CaptureSnapshot();
            Assert.Equal(last, snapshot.Revision);
            PrototypeCausewayState.ValidateSnapshot(snapshot);

            PrototypeCausewayCommandResult rejected = state.Execute(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = last,
                Kind = PrototypeCausewayCommandKind.ContributeStone
            });
            Assert.False(rejected.Accepted);
            Assert.Equal("causeway_history_full", rejected.Rejection);
            Assert.Equal(last, state.Revision);
        }

        [Fact]
        public void NativeV12RestoreRejectsNoncanonicalEnumAndFabricatedMorningStateAtomically()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Initialize(8.0f);
            AdvanceToMorning(session);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));

            PrototypeRuntimeSnapshot numericEnum = PrototypePersistenceService.DeserializeSnapshot(before);
            numericEnum.Causeway!.WaterControl = "1";
            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(numericEnum));

            PrototypeRuntimeSnapshot fabricatedOutcome = PrototypePersistenceService.DeserializeSnapshot(before);
            fabricatedOutcome.Causeway!.CausewayIntegrity = 32;
            fabricatedOutcome.Causeway.WetlandHealth = 84;
            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(fabricatedOutcome));

            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
        }

        private static void AdvanceToMorning(PrototypeRuntimeSession session)
        {
            for (int index = 0; index < 44; index++) session.Advance(1.0f, 48.0f);
        }

        private static PrototypeRuntimeSession CreateSession()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            return new PrototypeRuntimeSession(bundle.Scenarios.Resolve("snow_globe_voxel"), bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate)) return PrototypeCatalogLoader.LoadFromDirectory(candidate);
                current = Directory.GetParent(current)?.FullName;
            }
            throw new DirectoryNotFoundException("Could not locate prototype catalogs.");
        }

        private static PrototypeRuntimeSnapshot ToV11(PrototypeRuntimeSnapshot native)
        {
            string json = PrototypePersistenceService.SerializeSnapshot(native);
            Dictionary<string, object?> legacy = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
            legacy[nameof(PrototypeRuntimeSnapshot.SchemaVersion)] = 11;
            legacy.Remove(nameof(PrototypeRuntimeSnapshot.Causeway));
            return PrototypePersistenceService.DeserializeSnapshot(JsonSerializer.Serialize(legacy));
        }

        private static string EventIdentity(PrototypeEventRecord entry) =>
            $"{entry.Tick}|{entry.EventType}|{entry.Message}";

        private static PrototypeSerializableVector3 WithX(PrototypeSerializableVector3 value, float x)
        {
            value.X = x;
            return value;
        }

        private static PrototypeSerializableVector3 WithY(PrototypeSerializableVector3 value, float y)
        {
            value.Y = y;
            return value;
        }

        private static PrototypeSerializableVector3 WithZ(PrototypeSerializableVector3 value, float z)
        {
            value.Z = z;
            return value;
        }

        private static string TamperCausewayObject(string json, string tamper)
        {
            const string start = "\"Causeway\": {\r\n    \"Revision\": 0,\r\n    \"MigrationSourceSchemaVersion\": 0,";
            const string alternateStart = "\"Causeway\": {\n    \"Revision\": 0,\n    \"MigrationSourceSchemaVersion\": 0,";
            string matched = json.Contains(start, StringComparison.Ordinal) ? start : alternateStart;
            Assert.Contains(matched, json, StringComparison.Ordinal);
            string newline = matched.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string replacement = tamper switch
            {
                "unknown" => $"\"Causeway\": {{{newline}    \"UnknownAuthorityField\": 7,{newline}    \"Revision\": 0,{newline}    \"MigrationSourceSchemaVersion\": 0,",
                "duplicate" => $"\"Causeway\": {{{newline}    \"Revision\": 0,{newline}    \"Revision\": 0,{newline}    \"MigrationSourceSchemaVersion\": 0,",
                "wrong_order" => $"\"Causeway\": {{{newline}    \"MigrationSourceSchemaVersion\": 0,{newline}    \"Revision\": 0,",
                _ => throw new ArgumentOutOfRangeException(nameof(tamper))
            };
            return json.Replace(matched, replacement, StringComparison.Ordinal);
        }
    }
}
