using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Pure authoritative resource state. Scene nodes are projections of this ledger.
    /// </summary>
    internal sealed class PrototypeResourceLedger
    {
        private readonly Dictionary<string, Site> _sites;

        public long Revision { get; private set; }

        private PrototypeResourceLedger(IEnumerable<Site> sites)
        {
            _sites = sites.ToDictionary(site => site.SiteId, StringComparer.Ordinal);
        }

        public static PrototypeResourceLedger Create(WorldGenerationResult world)
        {
            return new PrototypeResourceLedger(world.ResourceSpawns.Select(spawn => new Site(
                RequireSiteId(spawn),
                spawn.ResourceId,
                spawn.Position,
                spawn.UnitsRemaining,
                spawn.ClusterId)));
        }

        public static PrototypeResourceLedger Restore(WorldGenerationResult world, PrototypeRuntimeSnapshot snapshot)
        {
            if (snapshot.SchemaVersion is not (5 or 6))
            {
                throw new InvalidDataException($"Unsupported runtime snapshot schema {snapshot.SchemaVersion}; expected 5 or 6.");
            }

            if (snapshot.Resources == null)
            {
                throw new InvalidDataException("Runtime snapshot resources cannot be null.");
            }

            PrototypeResourceLedger baseline = Create(world);
            if (snapshot.SchemaVersion == 6)
            {
                if (snapshot.Resources.Count != baseline._sites.Count)
                {
                    throw new InvalidDataException("Schema v6 resource ledger must contain every generated site.");
                }

                HashSet<string> seen = new(StringComparer.Ordinal);
                foreach (PrototypeResourceSnapshot resource in snapshot.Resources)
                {
                    if (resource == null)
                    {
                        throw new InvalidDataException("Runtime snapshot contains a null resource row.");
                    }
                    ValidateResource(resource);
                    if (string.IsNullOrWhiteSpace(resource.SiteId) || !seen.Add(resource.SiteId))
                    {
                        throw new InvalidDataException($"Duplicate or missing resource site id '{resource.SiteId}'.");
                    }

                    if (!baseline._sites.TryGetValue(resource.SiteId, out Site? site) || !Matches(site, resource))
                    {
                        throw new InvalidDataException($"Resource site '{resource.SiteId}' does not match the regenerated world.");
                    }

                    site.UnitsRemaining = resource.UnitsRemaining;
                }
            }
            else
            {
                foreach (Site site in baseline._sites.Values)
                {
                    site.UnitsRemaining = 0;
                }

                HashSet<string> matched = new(StringComparer.Ordinal);
                foreach (PrototypeResourceSnapshot resource in snapshot.Resources)
                {
                    if (resource == null)
                    {
                        throw new InvalidDataException("Runtime snapshot contains a null resource row.");
                    }
                    ValidateResource(resource);
                    List<Site> candidates = baseline._sites.Values
                        .Where(site => MatchesLegacy(site, resource))
                        .ToList();
                    if (candidates.Count != 1 || !matched.Add(candidates[0].SiteId))
                    {
                        throw new InvalidDataException("Schema v5 resource row is ambiguous, duplicated, or does not match the regenerated world.");
                    }

                    candidates[0].UnitsRemaining = resource.UnitsRemaining;
                }
            }

            ValidateWorkerTargets(snapshot, baseline._sites);
            return baseline;
        }

        public IReadOnlyList<PrototypeResourceSnapshot> CaptureSnapshots(bool includeDepleted = true)
        {
            return _sites.Values
                .Where(site => includeDepleted || site.UnitsRemaining > 0)
                .OrderBy(site => site.SiteId, StringComparer.Ordinal)
                .Select(site => new PrototypeResourceSnapshot
                {
                    SiteId = site.SiteId,
                    ResourceId = site.ResourceId,
                    UnitsRemaining = site.UnitsRemaining,
                    Position = PrototypeSerializableVector3.FromVector3(site.Position),
                    ClusterId = site.ClusterId
                })
                .ToList();
        }

        public IReadOnlyList<PrototypeResourceSiteState> CaptureActiveSites()
        {
            return _sites.Values
                .Where(site => site.UnitsRemaining > 0)
                .OrderBy(site => site.SiteId, StringComparer.Ordinal)
                .Select(site => new PrototypeResourceSiteState(
                    site.SiteId,
                    site.ResourceId,
                    site.Position,
                    site.UnitsRemaining,
                    site.ClusterId))
                .ToList();
        }

        public PrototypeHarvestResult Apply(in PrototypeHarvestCommand command)
        {
            if (command.RequestedQuantity <= 0)
            {
                return Failure(command, "invalid_amount");
            }

            if (!_sites.TryGetValue(command.SiteId, out Site? site))
            {
                return Failure(command, "site_missing");
            }

            if (!string.Equals(site.ResourceId, command.ResourceId, StringComparison.Ordinal))
            {
                return Failure(command, "resource_mismatch");
            }

            if (site.UnitsRemaining < command.RequestedQuantity)
            {
                return Failure(command, site.UnitsRemaining == 0 ? "site_depleted" : "insufficient_units");
            }

            site.UnitsRemaining -= command.RequestedQuantity;
            Revision++;
            return new PrototypeHarvestResult(
                command.ActorId,
                command.SiteId,
                site.ResourceId,
                command.RequestedQuantity,
                command.RequestedQuantity,
                true,
                string.Empty);
        }

        private static PrototypeHarvestResult Failure(in PrototypeHarvestCommand command, string reason)
        {
            return new PrototypeHarvestResult(
                command.ActorId,
                command.SiteId,
                command.ResourceId,
                command.RequestedQuantity,
                0,
                false,
                reason);
        }

        private static string RequireSiteId(PrototypeResourceSpawn spawn)
        {
            if (string.IsNullOrWhiteSpace(spawn.SiteId))
            {
                throw new InvalidOperationException("Generated resource spawn is missing its stable site id.");
            }

            return spawn.SiteId;
        }

        private static void ValidateResource(PrototypeResourceSnapshot resource)
        {
            Vector3 position = resource.Position.ToVector3();
            if (resource.UnitsRemaining < 0 || string.IsNullOrWhiteSpace(resource.ResourceId) ||
                !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new InvalidDataException("Runtime snapshot contains a malformed resource row.");
            }
        }

        private static bool Matches(Site site, PrototypeResourceSnapshot resource)
        {
            return MatchesLegacy(site, resource) && string.Equals(site.SiteId, resource.SiteId, StringComparison.Ordinal);
        }

        private static bool MatchesLegacy(Site site, PrototypeResourceSnapshot resource)
        {
            Vector3 position = resource.Position.ToVector3();
            return string.Equals(site.ResourceId, resource.ResourceId, StringComparison.Ordinal) &&
                   string.Equals(site.ClusterId, resource.ClusterId, StringComparison.Ordinal) &&
                   site.Position.X.Equals(position.X) && site.Position.Y.Equals(position.Y) && site.Position.Z.Equals(position.Z);
        }

        private static void ValidateWorkerTargets(PrototypeRuntimeSnapshot snapshot, IReadOnlyDictionary<string, Site> sites)
        {
            IEnumerable<PrototypeWorkerSnapshot> workers = (snapshot.Workers ?? new List<PrototypeWorkerSnapshot>())
                .Concat(snapshot.Settlement?.Citizens ?? new List<PrototypeWorkerSnapshot>());
            foreach (PrototypeWorkerSnapshot worker in workers)
            {
                if (!string.IsNullOrWhiteSpace(worker.TargetResourceNodeName) && !sites.ContainsKey(worker.TargetResourceNodeName))
                {
                    throw new InvalidDataException($"Worker '{worker.WorkerId}' targets unknown resource site '{worker.TargetResourceNodeName}'.");
                }
            }
        }

        private sealed class Site
        {
            public Site(string siteId, string resourceId, Vector3 position, int unitsRemaining, string clusterId)
            {
                SiteId = siteId;
                ResourceId = resourceId;
                Position = position;
                UnitsRemaining = unitsRemaining;
                ClusterId = clusterId;
            }

            public string SiteId { get; }
            public string ResourceId { get; }
            public Vector3 Position { get; }
            public int UnitsRemaining { get; set; }
            public string ClusterId { get; }
        }
    }
}
