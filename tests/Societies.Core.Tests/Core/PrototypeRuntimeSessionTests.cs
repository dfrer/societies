using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeRuntimeSessionTests
    {
        [Fact]
        public void RouteDistanceMode_FreshRuntimeExecutesConfiguredImplementation()
        {
            PrototypeCatalogBundle optimizedBundle = LoadCatalogs();
            PrototypeCatalogBundle referenceBundle = LoadCatalogs();
            PrototypeScenarioDefinition optimizedScenario = optimizedBundle.Scenarios.Resolve("balanced_basin");
            PrototypeScenarioDefinition referenceScenario = referenceBundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession optimized = new(
                optimizedScenario,
                optimizedBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.CachedDistanceOnly);
            PrototypeRuntimeSession reference = new(
                referenceScenario,
                referenceBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.FullMaterializationReference);
            optimized.Initialize(8.0f);
            reference.Initialize(8.0f);

            AdvanceTwice(optimized);
            AdvanceTwice(reference);

            Assert.Equal(PrototypeRouteDistanceMode.CachedDistanceOnly, optimized.RouteDistanceMode);
            Assert.Equal(PrototypeRouteDistanceMode.FullMaterializationReference, reference.RouteDistanceMode);
            Assert.True(optimized.CachedRouteDistanceFastPathHits > 0);
            Assert.Equal(0, reference.CachedRouteDistanceFastPathHits);
        }

        [Fact]
        public void RouteDistanceMode_SnapshotRestoreExecutesConfiguredImplementation()
        {
            PrototypeCatalogBundle sourceBundle = LoadCatalogs();
            PrototypeScenarioDefinition sourceScenario = sourceBundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession source = new(sourceScenario, sourceBundle.RoleQuotas.Roles);
            source.Initialize(8.0f);
            AdvanceTwice(source);
            PrototypeRuntimeSnapshot snapshot = source.CaptureSnapshot(Vector3.Zero);

            PrototypeCatalogBundle optimizedBundle = LoadCatalogs();
            PrototypeCatalogBundle referenceBundle = LoadCatalogs();
            PrototypeRuntimeSession optimized = new(
                optimizedBundle.Scenarios.Resolve("balanced_basin"),
                optimizedBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.CachedDistanceOnly);
            PrototypeRuntimeSession reference = new(
                referenceBundle.Scenarios.Resolve("balanced_basin"),
                referenceBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.FullMaterializationReference);
            optimized.ApplySnapshot(snapshot);
            reference.ApplySnapshot(snapshot);

            AdvanceTwice(optimized);
            AdvanceTwice(reference);

            Assert.True(optimized.CachedRouteDistanceFastPathHits > 0);
            Assert.Equal(0, reference.CachedRouteDistanceFastPathHits);
        }

        [Fact]
        public void PerformanceProbe_RuntimeSessionForwardsCacheAndForcedInvalidationControls()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            PrototypePerformanceProbeSnapshot startup = session.CapturePerformanceProbeState();
            Assert.Equal(0, startup.SimulationTick);
            Assert.Equal(0, startup.PathCacheEntryCount);

            _ = session.Advance(1.0f / 20.0f, 600.0f);
            PrototypePerformanceProbeSnapshot naturallyWarm = session.CapturePerformanceProbeState();
            Assert.Equal(1, naturallyWarm.SimulationTick);
            Assert.True(naturallyWarm.PathCacheEntryCount > 0);

            Assert.Equal(naturallyWarm.PathCacheEntryCount, session.ClearDerivedPathCacheForPerformance());
            Assert.Equal(0, session.CapturePerformanceProbeState().PathCacheEntryCount);
            Assert.True(session.TryPrepareForcedPathCompletionForPerformance(out string structureId));

            PrototypeRuntimeTickResult result = session.Advance(1.0f / 20.0f, 600.0f);
            PrototypePerformanceProbeSnapshot completed = session.CapturePerformanceProbeState();
            Assert.Equal(2, completed.SimulationTick);
            Assert.True(completed.ForcedInvalidation.Prepared);
            Assert.True(completed.ForcedInvalidation.Committed);
            Assert.Equal(structureId, completed.ForcedInvalidation.PathSegmentStructureId);
            Assert.True(completed.ForcedInvalidation.PathSegmentIsBuiltAfter);
            Assert.True(completed.ForcedInvalidation.FirstPostChangeLookupUsedNewVersion);
            Assert.Contains(result.SettlementResult.Events, entry => entry.EventType == PrototypeEventTypes.SettlementPathBuilt);
        }

        [Fact]
        public void BalancedBasin_ReachesEconomyStateWithMealsFuelAndBeds()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);

            for (int tick = 0; tick < 1400; tick++)
            {
                _ = session.Advance(1.0f / 20.0f, 600.0f);
            }

            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);

            Assert.True(
                session.Stockpile.GetCount("meals") > 0 ||
                session.Stockpile.GetCount("berries") > 0 ||
                snapshot.Settlement!.ProducedResources.GetValueOrDefault("meals") > 0);
            Assert.True(
                session.Stockpile.GetCount("firewood") > 0 ||
                session.Stockpile.GetCount("hearth_fuel") > 0 ||
                snapshot.Settlement!.ProducedResources.GetValueOrDefault("firewood") > 0 ||
                snapshot.Settlement.HearthLitTicks > 0);
            Assert.True(session.Stockpile.GetCount("beds") >= 2 || snapshot.Settlement!.Structures.Any(structure => structure.StructureKindId == "hut" && structure.IsBuilt));
            Assert.NotNull(snapshot.Settlement);
            Assert.True(snapshot.Settlement!.Citizens.Count == session.Workers.Count);
            Assert.True(snapshot.Settlement.PathSegments.Count > 0);
            Assert.True(snapshot.Settlement.LogisticsMetrics.TotalCompletedRouteDistanceMeters >= 0.0f);
        }

        [Fact]
        public void ResourceLedger_AssignsStableLegacyCompatibleIdsInOrdinalWorldOrder()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);

            List<string> logIds = session.World!.ResourceSpawns
                .Where(spawn => spawn.ResourceId == "logs")
                .Select(spawn => spawn.SiteId)
                .ToList();

            Assert.Equal(Enumerable.Range(1, logIds.Count).Select(index => $"logs_{index}"), logIds);
            Assert.Contains("logs_10", logIds);
            Assert.Equal(new[] { "logs_1", "logs_10", "logs_2" }, session.ResourceSnapshots
                .Where(resource => resource.SiteId is "logs_1" or "logs_10" or "logs_2")
                .Select(resource => resource.SiteId));
        }

        [Fact]
        public void ResourceLedger_PlayerHarvestMutatesLedgerAndInventoryExactlyOnce()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            PrototypeResourceSnapshot site = session.ResourceSnapshots.First(resource => resource.UnitsRemaining >= 2);

            PrototypeHarvestResult success = session.HarvestForPlayer(site.SiteId, 1);
            Assert.True(success.Succeeded);
            Assert.Equal("player", success.ActorId);
            Assert.Equal(site.SiteId, success.SiteId);
            Assert.Equal(1, success.RequestedQuantity);
            Assert.Equal(1, success.AppliedQuantity);
            Assert.Equal(1, session.Inventory.GetCount(success.ResourceId));
            Assert.Equal(site.UnitsRemaining - 1, session.ResourceSnapshots.Single(resource => resource.SiteId == site.SiteId).UnitsRemaining);
            PrototypeHarvestResult missing = session.HarvestForPlayer("missing_site", 1);
            PrototypeHarvestResult invalid = session.HarvestForPlayer(site.SiteId, 0);
            Assert.False(missing.Succeeded);
            Assert.Equal("site_missing", missing.FailureReason);
            Assert.False(invalid.Succeeded);
            Assert.Equal("invalid_command", invalid.FailureReason);
            Assert.Equal(1, session.Inventory.GetCount(success.ResourceId));
            Assert.Single(session.EventLog.Entries.Where(entry => entry.EventType == PrototypeEventTypes.PlayerHarvestSucceeded));
        }

        [Fact]
        public void ResourceLedger_ContestedLastUnitUsesCommandOrderWithoutDuplicateMutation()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession source = new(scenario, bundle.RoleQuotas.Roles);
            source.Initialize(8.0f);
            PrototypeRuntimeSnapshot prepared = source.CaptureSnapshot(Vector3.Zero);
            PrototypeResourceSnapshot contested = prepared.Resources.First();
            contested.UnitsRemaining = 1;

            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles);
            session.ApplySnapshot(prepared);

            PrototypeHarvestResult first = session.HarvestForPlayer(contested.SiteId, 1);
            PrototypeHarvestResult second = session.HarvestForPlayer(contested.SiteId, 1);
            Assert.True(first.Succeeded);
            Assert.False(second.Succeeded);
            Assert.Equal("site_depleted", second.FailureReason);
            Assert.Equal(1, first.AppliedQuantity);
            Assert.Equal(0, second.AppliedQuantity);
            Assert.Equal(0, session.ResourceSnapshots.Single(resource => resource.SiteId == contested.SiteId).UnitsRemaining);
            Assert.Equal(1, session.Inventory.GetCount(first.ResourceId));
            Assert.Single(session.EventLog.Entries.Where(entry => entry.EventType == PrototypeEventTypes.PlayerHarvestSucceeded));
        }

        [Fact]
        public void ResourceLedger_AiContestedLastUnitReturnsOrderedResultsRollsBackAndOrdersEvents()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            PrototypeRuntimeSnapshot prepared = session.CaptureSnapshot(Vector3.Zero);
            PrototypeResourceSnapshot site = prepared.Resources.First();
            site.UnitsRemaining = 1;
            session.ApplySnapshot(prepared);
            PrototypeWorkerState firstWorker = session.Workers[0];
            PrototypeWorkerState secondWorker = session.Workers[1];
            PrototypeHarvestRequest[] requests =
            {
                new(firstWorker.WorkerId, firstWorker.DisplayName, site.SiteId, site.ResourceId, 1, site.ClusterId),
                new(secondWorker.WorkerId, secondWorker.DisplayName, site.SiteId, site.ResourceId, 1, site.ClusterId)
            };

            MethodInfo apply = typeof(PrototypeRuntimeSession).GetMethod(
                "ApplyAiHarvestRequests",
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing private AI harvest transaction method.");
            IReadOnlyList<PrototypeHarvestResult> results = (IReadOnlyList<PrototypeHarvestResult>)apply.Invoke(
                session,
                new object?[] { requests, null })!;

            Assert.Equal(new[] { firstWorker.WorkerId, secondWorker.WorkerId }, results.Select(result => result.ActorId));
            Assert.True(results[0].Succeeded);
            Assert.False(results[1].Succeeded);
            Assert.Equal("site_depleted", results[1].FailureReason);
            Assert.Equal(0, session.ResourceSnapshots.Single(resource => resource.SiteId == site.SiteId).UnitsRemaining);
            Assert.Equal("harvest.failed", session.Workers.Single(worker => worker.WorkerId == secondWorker.WorkerId).LastFailureReason);
            Assert.Equal(
                new[] { PrototypeEventTypes.AiHarvestSucceeded, PrototypeEventTypes.AiHarvestFailed },
                session.EventLog.Entries.Select(entry => entry.EventType));
        }

        [Fact]
        public void ResourceLedger_V5MigrationRestoresFullIdentityAndMarksOmittedSiteDepleted()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession source = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            source.Initialize(8.0f);
            PrototypeRuntimeSnapshot legacy = source.CaptureSnapshot(Vector3.Zero);
            string inFlightTarget = legacy.Resources.First().SiteId;
            PrototypeWorkerSnapshot inFlightWorker = legacy.Workers.First();
            inFlightWorker.TargetResourceNodeName = inFlightTarget;
            legacy.Settlement!.Citizens.Single(worker => worker.WorkerId == inFlightWorker.WorkerId).TargetResourceNodeName = inFlightTarget;
            PrototypeResourceSnapshot omitted = legacy.Resources.First(resource => resource.SiteId != inFlightTarget);
            legacy.SchemaVersion = 5;
            legacy.Resources.Remove(omitted);
            legacy.Resources.Reverse();
            foreach (PrototypeResourceSnapshot resource in legacy.Resources)
            {
                resource.SiteId = string.Empty;
            }

            PrototypeRuntimeSession restored = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            restored.ApplySnapshot(legacy);

            Assert.Equal(source.ResourceSnapshots.Count, restored.ResourceSnapshots.Count);
            PrototypeResourceSnapshot migratedOmission = restored.ResourceSnapshots.Single(resource =>
                resource.ResourceId == omitted.ResourceId &&
                resource.ClusterId == omitted.ClusterId &&
                resource.Position.X == omitted.Position.X && resource.Position.Y == omitted.Position.Y && resource.Position.Z == omitted.Position.Z);
            Assert.Equal(0, migratedOmission.UnitsRemaining);
            Assert.False(string.IsNullOrWhiteSpace(migratedOmission.SiteId));
            Assert.Contains(restored.Workers, worker => worker.TargetResourceNodeName == inFlightTarget);
            Assert.Equal(6, restored.CaptureSnapshot(Vector3.Zero).SchemaVersion);
        }

        [Fact]
        public void ResourceLedger_V6RoundTripAndHeadlessAdvanceAreExact()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession continuous = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            continuous.Initialize(8.0f);
            for (int tick = 0; tick < 300; tick++)
            {
                _ = continuous.Advance(1.0f / 20.0f, 600.0f);
            }

            PrototypeRuntimeSnapshot snapshot = continuous.CaptureSnapshot(Vector3.Zero);
            PrototypeRuntimeSession restored = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            restored.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(snapshot)));

            Assert.Equal(6, snapshot.SchemaVersion);
            Assert.Equal(JsonSerializer.Serialize(snapshot.Resources), JsonSerializer.Serialize(restored.ResourceSnapshots));
            Assert.Equal(JsonSerializer.Serialize(snapshot.Settlement), JsonSerializer.Serialize(restored.CaptureSnapshot(Vector3.Zero).Settlement));
        }

        [Fact]
        public void ResourceLedger_InvalidRestoreDoesNotMutateLiveSession()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            _ = session.Advance(1.0f / 20.0f, 600.0f);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            PrototypeRuntimeSnapshot invalid = PrototypePersistenceService.DeserializeSnapshot(before);
            invalid.WorldHash = "wrong";

            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(invalid));
            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot("{}"));
            Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot("{\"SchemaVersion\":7}"));
            Assert.ThrowsAny<JsonException>(() => PrototypePersistenceService.DeserializeSnapshot("{"));
        }

        [Fact]
        public void ResourceLedger_StrictMalformedSnapshotsAreRejectedBeforeCommit()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            _ = session.Advance(1.0f / 20.0f, 600.0f);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));

            PrototypeRuntimeSnapshot negativeInventory = CloneSnapshot(before);
            negativeInventory.Inventory["logs"] = -1;
            AssertRejectedWithoutMutation(session, negativeInventory, before);

            PrototypeRuntimeSnapshot blankItem = CloneSnapshot(before);
            blankItem.Stockpile[" "] = 1;
            AssertRejectedWithoutMutation(session, blankItem, before);

            PrototypeRuntimeSnapshot unknownWeather = CloneSnapshot(before);
            unknownWeather.CurrentWeather = "clear";
            AssertRejectedWithoutMutation(session, unknownWeather, before);

            PrototypeRuntimeSnapshot nullNestedCollection = CloneSnapshot(before);
            nullNestedCollection.Settlement!.SiteCaches = null!;
            AssertRejectedWithoutMutation(session, nullNestedCollection, before);

            PrototypeRuntimeSnapshot duplicateWorker = CloneSnapshot(before);
            duplicateWorker.Workers.Add(duplicateWorker.Workers[0]);
            AssertRejectedWithoutMutation(session, duplicateWorker, before);

            PrototypeRuntimeSnapshot mismatchedWorker = CloneSnapshot(before);
            mismatchedWorker.Workers[0].TargetResourceNodeName = mismatchedWorker.Resources[0].SiteId;
            AssertRejectedWithoutMutation(session, mismatchedWorker, before);

            PrototypeRuntimeSnapshot mismatchedWorkerPosition = CloneSnapshot(before);
            PrototypeSerializableVector3 mismatchedPosition = mismatchedWorkerPosition.Workers[0].Position;
            mismatchedPosition.X += 1.0f;
            mismatchedWorkerPosition.Workers[0].Position = mismatchedPosition;
            AssertRejectedWithoutMutation(session, mismatchedWorkerPosition, before);

            PrototypeRuntimeSnapshot nullResource = CloneSnapshot(before);
            nullResource.Resources[0] = null!;
            AssertRejectedWithoutMutation(session, nullResource, before);

            PrototypeRuntimeSnapshot negativeCounter = CloneSnapshot(before);
            negativeCounter.Settlement!.TotalTicks = -1;
            AssertRejectedWithoutMutation(session, negativeCounter, before);

            PrototypeRuntimeSnapshot invalidRole = CloneSnapshot(before);
            string workerId = invalidRole.Workers[0].WorkerId;
            invalidRole.Workers[0].RoleId = "NotARole";
            invalidRole.Settlement!.Citizens.Single(worker => worker.WorkerId == workerId).RoleId = "NotARole";
            AssertRejectedWithoutMutation(session, invalidRole, before);

            PrototypeRuntimeSnapshot duplicateStructure = CloneSnapshot(before);
            duplicateStructure.Settlement!.Structures.Add(duplicateStructure.Settlement.Structures[0]);
            AssertRejectedWithoutMutation(session, duplicateStructure, before);

            PrototypeRuntimeSnapshot inconsistentNavigationVersion = CloneSnapshot(before);
            inconsistentNavigationVersion.Settlement!.NavigationRulesVersion++;
            AssertRejectedWithoutMutation(session, inconsistentNavigationVersion, before);

            PrototypeRuntimeSnapshot nonFiniteStorePosition = CloneSnapshot(before);
            PrototypeSerializableVector3 invalidStorePosition = nonFiniteStorePosition.Settlement!.CentralDepot.Position;
            invalidStorePosition.X = float.NaN;
            nonFiniteStorePosition.Settlement.CentralDepot.Position = invalidStorePosition;
            AssertRejectedWithoutMutation(session, nonFiniteStorePosition, before);
        }

        [Fact]
        public void ResourceLedger_V5InfersNavigationVersionFromBuiltPathsAndResumesExactly()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession source = new(scenario, bundle.RoleQuotas.Roles);
            source.Initialize(8.0f);
            PrototypeRuntimeSnapshot preparedV6 = source.CaptureSnapshot(Vector3.Zero);
            PrototypePathSegmentSnapshot builtPath = preparedV6.Settlement!.PathSegments.First();
            builtPath.IsBuilt = true;
            int derivedVersion = 1 + preparedV6.Settlement.PathSegments.Count(segment => segment.IsBuilt);
            preparedV6.Settlement.NavigationRulesVersion = derivedVersion;
            foreach (PrototypeWorkerSnapshot worker in preparedV6.Workers)
            {
                worker.CachedRouteVersion = derivedVersion;
            }
            foreach (PrototypeWorkerSnapshot worker in preparedV6.Settlement.Citizens)
            {
                worker.CachedRouteVersion = derivedVersion;
            }

            PrototypeRuntimeSnapshot legacyV5 = CloneSnapshot(PrototypePersistenceService.SerializeSnapshot(preparedV6));
            legacyV5.SchemaVersion = 5;
            legacyV5.Settlement!.NavigationRulesVersion = 1;
            foreach (PrototypeResourceSnapshot resource in legacyV5.Resources)
            {
                resource.SiteId = string.Empty;
            }

            PrototypeCatalogBundle v6Bundle = LoadCatalogs();
            PrototypeCatalogBundle v5Bundle = LoadCatalogs();
            PrototypeRuntimeSession restoredV6 = new(v6Bundle.Scenarios.Resolve("balanced_basin"), v6Bundle.RoleQuotas.Roles);
            PrototypeRuntimeSession migratedV5 = new(v5Bundle.Scenarios.Resolve("balanced_basin"), v5Bundle.RoleQuotas.Roles);
            restoredV6.ApplySnapshot(preparedV6);
            migratedV5.ApplySnapshot(legacyV5);

            Assert.Equal(derivedVersion, migratedV5.CaptureSnapshot(Vector3.Zero).Settlement!.NavigationRulesVersion);
            Assert.Equal(
                JsonSerializer.Serialize(restoredV6.CaptureSnapshot(Vector3.Zero).Settlement),
                JsonSerializer.Serialize(migratedV5.CaptureSnapshot(Vector3.Zero).Settlement));
            for (int tick = 0; tick < 20; tick++)
            {
                _ = restoredV6.Advance(1.0f / 20.0f, 600.0f);
                _ = migratedV5.Advance(1.0f / 20.0f, 600.0f);
            }
            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(restoredV6.CaptureSnapshot(Vector3.Zero)),
                PrototypePersistenceService.SerializeSnapshot(migratedV5.CaptureSnapshot(Vector3.Zero)));
        }

        [Fact]
        public void ResourceLedger_WorldSummaryCountsOnlyActiveSites()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            PrototypeResourceSnapshot depleted = session.ResourceSnapshots.First();
            PrototypeRuntimeSnapshot prepared = session.CaptureSnapshot(Vector3.Zero);
            prepared.Resources.Single(resource => resource.SiteId == depleted.SiteId).UnitsRemaining = 0;
            session.ApplySnapshot(prepared);

            PrototypeWorldSummary summary = PrototypeWorldSummaryBuilder.Build(session, null, session.ActiveResourceSnapshots);

            Assert.Equal(session.ActiveResourceSnapshots.Count, summary.ResourceNodeCounts.Values.Sum());
            Assert.Equal(
                session.ActiveResourceSnapshots.Where(resource => resource.ResourceId == depleted.ResourceId).Count(),
                summary.ResourceNodeCounts[depleted.ResourceId]);
            Assert.Equal(
                session.ActiveResourceSnapshots.Where(resource => resource.ResourceId == depleted.ResourceId).Sum(resource => resource.UnitsRemaining),
                summary.RemainingResourceUnits[depleted.ResourceId]);
        }

        [Fact]
        public void ResourceLedger_Continuous1000TicksMatchesCheckpointResumeAt500Exactly()
        {
            PrototypeCatalogBundle continuousBundle = LoadCatalogs();
            PrototypeCatalogBundle checkpointBundle = LoadCatalogs();
            PrototypeRuntimeSession continuous = new(continuousBundle.Scenarios.Resolve("balanced_basin"), continuousBundle.RoleQuotas.Roles);
            PrototypeRuntimeSession checkpoint = new(checkpointBundle.Scenarios.Resolve("balanced_basin"), checkpointBundle.RoleQuotas.Roles);
            continuous.Initialize(8.0f);
            checkpoint.Initialize(8.0f);

            for (int tick = 0; tick < 500; tick++)
            {
                _ = continuous.Advance(1.0f / 20.0f, 600.0f);
                _ = checkpoint.Advance(1.0f / 20.0f, 600.0f);
            }

            PrototypeRuntimeSnapshot checkpointSnapshot = checkpoint.CaptureSnapshot(Vector3.Zero);
            List<PrototypeEventRecord> checkpointEvents = checkpoint.EventLog.Entries
                .Select(entry => new PrototypeEventRecord { Tick = entry.Tick, EventType = entry.EventType, Message = entry.Message })
                .ToList();
            PrototypeCatalogBundle resumedBundle = LoadCatalogs();
            PrototypeRuntimeSession resumed = new(resumedBundle.Scenarios.Resolve("balanced_basin"), resumedBundle.RoleQuotas.Roles);
            resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(checkpointSnapshot)));
            resumed.RestoreArtifacts(checkpointEvents, null);

            for (int tick = 500; tick < 1000; tick++)
            {
                _ = continuous.Advance(1.0f / 20.0f, 600.0f);
                _ = resumed.Advance(1.0f / 20.0f, 600.0f);
            }

            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(continuous.CaptureSnapshot(Vector3.Zero)),
                PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(
                PrototypePersistenceService.SerializeEventLog(continuous.EventLog),
                PrototypePersistenceService.SerializeEventLog(resumed.EventLog));
        }

        [Fact]
        public void ResourceAuthority_ProductionHasNoSceneMutationAndPreservesMetricsAttribution()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(GetCatalogDirectoryPath(), "..", "..", ".."));
            string gameManager = File.ReadAllText(Path.Combine(repositoryRoot, "src", "societies", "scripts", "core", "GameManager.cs"));
            string session = File.ReadAllText(Path.Combine(repositoryRoot, "src", "societies", "scripts", "core", "PrototypeRuntimeSession.cs"));
            string presenter = File.ReadAllText(Path.Combine(repositoryRoot, "src", "societies", "scripts", "presentation", "PrototypeSettlementScenePresenter.cs"));
            string resourceNode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "societies", "scripts", "core", "ResourceNode.cs"));

            Assert.DoesNotContain("CaptureResourceSites", gameManager + presenter, StringComparison.Ordinal);
            Assert.DoesNotContain("ApplyHarvestRequest", gameManager + presenter, StringComparison.Ordinal);
            Assert.DoesNotContain("TryHarvest(", resourceNode, StringComparison.Ordinal);
            Assert.DoesNotContain("RuntimeMetricsPhase.HarvestApply", gameManager, StringComparison.Ordinal);
            Assert.Contains("BeginPhase(RuntimeMetricsPhase.HarvestApply)", session, StringComparison.Ordinal);
            Assert.Contains("_runtimeSession.ActiveResourceSnapshots", gameManager, StringComparison.Ordinal);
        }

        private static PrototypeRuntimeSnapshot CloneSnapshot(string json)
        {
            return PrototypePersistenceService.DeserializeSnapshot(json);
        }

        private static void AssertRejectedWithoutMutation(
            PrototypeRuntimeSession session,
            PrototypeRuntimeSnapshot invalid,
            string expectedSnapshotJson)
        {
            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(invalid));
            Assert.Equal(expectedSnapshotJson, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
        }

        private static void AdvanceTwice(PrototypeRuntimeSession session)
        {
            _ = session.Advance(1.0f / 20.0f, 600.0f);
            _ = session.Advance(1.0f / 20.0f, 600.0f);
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
        }

        private static string GetCatalogDirectoryPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string? current = baseDirectory;

            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent?.FullName;
            }

            throw new DirectoryNotFoundException($"Could not find src/societies/data from '{baseDirectory}'.");
        }
    }
}
