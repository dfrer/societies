using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class AcceptedSceneBaselineProfileTests
    {
        [Fact]
        public void SharedRunnerProfilesPinPacket01V4AndPacket02V5WithoutTupleMixing()
        {
            string repositoryRoot = FindRepositoryRoot();
            string script = Path.Combine(repositoryRoot, "scripts", "run-accepted-scene-baseline.ps1");

            JsonElement v4 = RunProfile(script, "packet01-v4", "artifacts/profile-contract-v4");
            Assert.Equal("31ea1d6012d6fd932d0bfe0dbc621e668fd58c80", v4.GetProperty("baseSha").GetString());
            Assert.Equal("feature/social-kernel-01-baseline", v4.GetProperty("expectedBranch").GetString());
            Assert.Equal("societies_accepted_scene_baseline/v4", v4.GetProperty("trialSchema").GetString());
            Assert.Equal("societies_accepted_scene_baseline_bundle/v4", v4.GetProperty("bundleSchema").GetString());
            Assert.Equal("snow-globe-voxel-four-leg-edit-reload-replay/v4", v4.GetProperty("routeId").GetString());
            Assert.Equal("accepted-scene-baseline-trial-v4.json", v4.GetProperty("trialArtifactFileName").GetString());
            Assert.Equal("accepted-scene-baseline-v4.json", v4.GetProperty("bundleFileName").GetString());
            Assert.False(v4.GetProperty("requireCauseway").GetBoolean());
            Assert.False(v4.GetProperty("includeSameRouteComparison").GetBoolean());
            Assert.Equal("fresh_export_required", v4.GetProperty("exportMode").GetString());
            Assert.DoesNotContain("sameRouteComparison", v4.GetProperty("bundleProperties").EnumerateArray().Select(value => value.GetString()));

            JsonElement v5 = RunProfile(script, "packet02-v5", "artifacts/profile-contract-v5");
            Assert.Equal("1745896535124bd39ca6321fe6430d93de81bf43", v5.GetProperty("baseSha").GetString());
            Assert.Equal("feature/social-kernel-02a-causeway-substrate", v5.GetProperty("expectedBranch").GetString());
            Assert.Equal("societies_accepted_scene_baseline/v5", v5.GetProperty("trialSchema").GetString());
            Assert.Equal("societies_accepted_scene_baseline_bundle/v5", v5.GetProperty("bundleSchema").GetString());
            Assert.Equal("snow-globe-voxel-causeway-state-edit-reload-replay/v5", v5.GetProperty("routeId").GetString());
            Assert.Equal("accepted-scene-baseline-trial-v5.json", v5.GetProperty("trialArtifactFileName").GetString());
            Assert.Equal("accepted-scene-baseline-v5.json", v5.GetProperty("bundleFileName").GetString());
            Assert.True(v5.GetProperty("requireCauseway").GetBoolean());
            Assert.True(v5.GetProperty("includeSameRouteComparison").GetBoolean());
            Assert.Equal("fresh_export_required", v5.GetProperty("exportMode").GetString());
            Assert.Contains("sameRouteComparison", v5.GetProperty("bundleProperties").EnumerateArray().Select(value => value.GetString()));
            Assert.True(double.IsFinite(v5.GetProperty("baselineProcessP95Milliseconds").GetDouble()) && v5.GetProperty("baselineProcessP95Milliseconds").GetDouble() > 0);
            Assert.True(double.IsFinite(v5.GetProperty("baselinePhysicsP95Milliseconds").GetDouble()) && v5.GetProperty("baselinePhysicsP95Milliseconds").GetDouble() > 0);

            string[] sourceLines = File.ReadAllLines(script);
            Assert.DoesNotContain(sourceLines, line => line.StartsWith("    sameRouteComparison =", StringComparison.Ordinal));
            Assert.Contains(sourceLines, line => line.Trim().Equals(
                "if ([bool]$profileContract.includeSameRouteComparison) {", StringComparison.Ordinal));

            PowerShellResult mixed = RunPowerShell(script, "-ProfileContractOnly", "-BaseSha", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            Assert.NotEqual(0, mixed.ExitCode);
            PowerShellResult arbitrary = RunPowerShell(script, "-ProfileContractOnly", "-Profile", "custom-v5");
            Assert.NotEqual(0, arbitrary.ExitCode);
        }

        [Fact]
        public void ExistingExportReuseIsExplicitRepositoryContainedAndFailClosedOnIdentityDrift()
        {
            string repositoryRoot = FindRepositoryRoot();
            string script = Path.Combine(repositoryRoot, "scripts", "run-accepted-scene-baseline.ps1");
            string wrapper = Path.Combine(repositoryRoot, "scripts", "run-causeway-packet-02-route.ps1");
            const string relative = "artifacts/profile-contract-reuse";

            PowerShellResult defaultContract = RunPowerShell(
                wrapper, "-ProfileContractOnly", "-OutputDirectory", relative);
            Assert.Equal(0, defaultContract.ExitCode);
            using JsonDocument defaultDocument = JsonDocument.Parse(defaultContract.StandardOutput.Trim());
            JsonElement contract = defaultDocument.RootElement;
            Assert.Equal("fresh_export_required", contract.GetProperty("exportMode").GetString());
            Assert.False(contract.GetProperty("reuseValidationOnly").GetBoolean());
            string[] requirements = contract.GetProperty("reuseValidationRequirements").EnumerateArray()
                .Select(value => value.GetString()!).ToArray();
            Assert.Contains("repository-contained-output", requirements);
            Assert.Contains("pre-existing-completed-attestation", requirements);
            Assert.Contains("exact-current-worktree-content-identity", requirements);
            Assert.Contains("exact-full-project-input-manifest-and-digest", requirements);
            Assert.Contains("complete-release-runner-layout", requirements);
            Assert.Contains("packaged-exportrelease-managed-input-digests", requirements);
            Assert.Contains("godot-export-cache-source-digests", requirements);
            Assert.Contains("exact-release-file-manifest", requirements);

            PowerShellResult implicitReuse = RunPowerShell(
                script, "-ReuseValidationOnly");
            Assert.NotEqual(0, implicitReuse.ExitCode);
            Assert.Contains("requires the explicit -ReuseExistingExport opt-in", implicitReuse.StandardError + implicitReuse.StandardOutput);

            string outside = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "outside-reuse-contract"));
            PowerShellResult outsideContract = RunPowerShell(
                wrapper, "-ProfileContractOnly", "-OutputDirectory", outside);
            Assert.NotEqual(0, outsideContract.ExitCode);
            Assert.Contains("must remain inside the repository", outsideContract.StandardError + outsideContract.StandardOutput);

            string source = File.ReadAllText(script);
            Assert.Contains("Explicit reusable export output is missing", source, StringComparison.Ordinal);
            Assert.Contains("requires a pre-existing completed identity manifest", source, StringComparison.Ordinal);
            Assert.Contains("was not finalized by a successful fresh export", source, StringComparison.Ordinal);
            Assert.Contains("completed identity manifest is stale or mismatched", source, StringComparison.Ordinal);
            Assert.Contains("does not match the current ExportRelease input", source, StringComparison.Ordinal);
            Assert.Contains("Reusable export cache source digest is stale", source, StringComparison.Ordinal);
            Assert.Contains("Reusable export output already contains trial, bundle, or unknown files", source, StringComparison.Ordinal);
            Assert.Contains("'--quit'", source, StringComparison.Ordinal);
        }

        [Fact]
        public void ExecutionModesRejectConflictsAndIncompleteAttestationParameterPairsBeforeEarlyReturn()
        {
            string repositoryRoot = FindRepositoryRoot();
            string wrapper = Path.Combine(repositoryRoot, "scripts", "run-causeway-packet-02-route.ps1");
            string missing = Path.Combine(repositoryRoot, "artifacts", "mode-conflict-does-not-exist.json");

            PowerShellResult profileCauseway = RunPowerShell(
                wrapper, "-ProfileContractOnly", "-VerifyTrialCausewayArtifactOnly", missing);
            AssertAllEnginesFailedWith(profileCauseway, "modes are mutually exclusive");

            PowerShellResult profileAttestation = RunPowerShell(
                wrapper, "-ProfileContractOnly", "-VerifyExportAttestationOnly", missing,
                "-ExpectedAttestationRequestPath", missing);
            AssertAllEnginesFailedWith(profileAttestation, "modes are mutually exclusive");

            PowerShellResult bothVerifiers = RunPowerShell(
                wrapper, "-VerifyTrialCausewayArtifactOnly", missing,
                "-VerifyExportAttestationOnly", missing, "-ExpectedAttestationRequestPath", missing);
            AssertAllEnginesFailedWith(bothVerifiers, "modes are mutually exclusive");

            PowerShellResult missingRequest = RunPowerShell(wrapper, "-VerifyExportAttestationOnly", missing);
            AssertAllEnginesFailedWith(missingRequest, "must be supplied together");

            PowerShellResult requestWithoutVerifier = RunPowerShell(wrapper, "-ExpectedAttestationRequestPath", missing);
            AssertAllEnginesFailedWith(requestWithoutVerifier, "must be supplied together");

            PowerShellResult validationWithReuse = RunPowerShell(
                wrapper, "-VerifyTrialCausewayArtifactOnly", missing, "-ReuseExistingExport");
            AssertAllEnginesFailedWith(validationWithReuse, "cannot be combined with normal run");
        }

        [Fact]
        public void ExecutableAttestationVerifierAcceptsCompletedExactManifestAndRejectsMissingOrIncomplete()
        {
            string repositoryRoot = FindRepositoryRoot();
            string wrapper = Path.Combine(repositoryRoot, "scripts", "run-causeway-packet-02-route.ps1");
            string fixtureRoot = Path.Combine(repositoryRoot, "artifacts", $"attestation-verifier-{Guid.NewGuid():N}");
            try
            {
                AttestationFixture fixture = CreateAttestationFixture(fixtureRoot);
                PowerShellResult valid = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesSucceeded(valid);
                Assert.All(valid.EngineResults,
                    engine => Assert.Contains("EXPORT_ATTESTATION_VALID", engine.StandardOutput));

                File.Delete(fixture.IdentityPath);
                PowerShellResult missing = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(missing, "pre-existing completed identity manifest");

                WriteAttestation(fixture, "pending");
                PowerShellResult pending = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(pending, "was not finalized by a successful fresh export");

                fixture = CreateAttestationFixture(fixtureRoot, replace: true);
                JsonObject missingExportCache = JsonNode.Parse(File.ReadAllText(fixture.IdentityPath))!.AsObject();
                missingExportCache["completion"]!.AsObject().Remove("exportCache");
                File.WriteAllText(fixture.IdentityPath, missingExportCache.ToJsonString());
                PowerShellResult exportCacheMissing = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(exportCacheMissing, "completion property set is invalid");

                fixture = CreateAttestationFixture(fixtureRoot, replace: true);
                JsonObject missingManagedInputs = JsonNode.Parse(File.ReadAllText(fixture.IdentityPath))!.AsObject();
                missingManagedInputs["completion"]!.AsObject().Remove("managedInputs");
                File.WriteAllText(fixture.IdentityPath, missingManagedInputs.ToJsonString());
                PowerShellResult managedInputsMissing = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(managedInputsMissing, "completion property set is invalid");
            }
            finally
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        [Fact]
        public void ExecutableAttestationVerifierRejectsSourceProjectPckAndReleaseFileDrift()
        {
            string repositoryRoot = FindRepositoryRoot();
            string wrapper = Path.Combine(repositoryRoot, "scripts", "run-causeway-packet-02-route.ps1");
            string fixtureRoot = Path.Combine(repositoryRoot, "artifacts", $"attestation-drift-{Guid.NewGuid():N}");
            try
            {
                AttestationFixture fixture = CreateAttestationFixture(fixtureRoot);
                JsonObject request = JsonNode.Parse(File.ReadAllText(fixture.RequestPath))!.AsObject();
                request["repositoryContentIdentity"] = "source-drift";
                File.WriteAllText(fixture.RequestPath, request.ToJsonString());
                PowerShellResult sourceDrift = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(
                    sourceDrift, "source, project-input, preset, profile, runner, or build-input drift");

                fixture = CreateAttestationFixture(fixtureRoot, replace: true);
                request = JsonNode.Parse(File.ReadAllText(fixture.RequestPath))!.AsObject();
                request["projectInputs"]!["aggregateSha256"] = "project-input-drift";
                File.WriteAllText(fixture.RequestPath, request.ToJsonString());
                PowerShellResult projectDrift = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(
                    projectDrift, "source, project-input, preset, profile, runner, or build-input drift");

                fixture = CreateAttestationFixture(fixtureRoot, replace: true);
                File.AppendAllText(fixture.PckPath, "pck-drift");
                PowerShellResult pckDrift = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(pckDrift, "PCK is missing or drifted");

                fixture = CreateAttestationFixture(fixtureRoot, replace: true);
                File.AppendAllText(fixture.PayloadPath, "release-drift");
                PowerShellResult releaseDrift = VerifyAttestation(wrapper, fixture);
                AssertAllEnginesFailedWith(releaseDrift, "release-file manifest is missing or drifted");
            }
            finally
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        [Fact]
        public void ExecutableCausewayVerifierAcceptsRealtimeAndFixedReplayEvidence()
        {
            string repositoryRoot = FindRepositoryRoot();
            string wrapper = Path.Combine(repositoryRoot, "scripts", "run-causeway-packet-02-route.ps1");
            string fixtureRoot = Path.Combine(repositoryRoot, "artifacts", $"causeway-verifier-{Guid.NewGuid():N}");
            Directory.CreateDirectory(fixtureRoot);
            try
            {
                foreach (string mode in new[] { "realtime_performance", "fixed_delta_identity" })
                {
                    string path = Path.Combine(fixtureRoot, $"{mode}.json");
                    File.WriteAllText(path, CreateCausewayArtifact(mode).ToJsonString());
                    PowerShellResult result = RunPowerShell(wrapper, "-VerifyTrialCausewayArtifactOnly", path);
                    AssertAllEnginesSucceeded(result);
                    Assert.All(result.EngineResults,
                        engine => Assert.Contains("CAUSEWAY_TRIAL_EVIDENCE_VALID", engine.StandardOutput));
                }
            }
            finally
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        [Fact]
        public void ExecutableCausewayVerifierRejectsMissingMalformedAndCorruptedEvidence()
        {
            string repositoryRoot = FindRepositoryRoot();
            string wrapper = Path.Combine(repositoryRoot, "scripts", "run-causeway-packet-02-route.ps1");
            string fixtureRoot = Path.Combine(repositoryRoot, "artifacts", $"causeway-rejection-{Guid.NewGuid():N}");
            Directory.CreateDirectory(fixtureRoot);
            try
            {
                JsonObject missing = CreateCausewayArtifact("realtime_performance");
                missing.Remove("causeway");
                AssertVerifierRejects(wrapper, fixtureRoot, "missing.json", missing.ToJsonString(), "Causeway command/event/revision evidence");
                AssertVerifierRejects(wrapper, fixtureRoot, "malformed.json", "{", "artifact JSON is malformed");

                string[] causewayProperties =
                {
                    "commandKind", "commandQuantity", "accepted", "eventType", "previousRevision", "revision",
                    "beforeCommandStateIdentity", "afterCommandStateIdentity", "afterVoxelEditStateIdentity",
                    "reloadedStateIdentity", "replayedAfterCommandStateIdentity", "replayedAfterVoxelEditStateIdentity"
                };
                foreach (string property in causewayProperties)
                {
                    JsonObject omitted = CreateCausewayArtifact("realtime_performance");
                    omitted["causeway"]!.AsObject().Remove(property);
                    AssertVerifierRejects(
                        wrapper, fixtureRoot, $"omitted-{property}.json", omitted.ToJsonString(), "property set is invalid");

                    JsonObject nullValue = CreateCausewayArtifact("realtime_performance");
                    nullValue["causeway"]![property] = null;
                    string expectedTypeMessage = property is "commandQuantity" or "previousRevision" or "revision"
                        ? "must have native JSON integer type"
                        : "must have native JSON type";
                    AssertVerifierRejects(
                        wrapper, fixtureRoot, $"null-{property}.json", nullValue.ToJsonString(), expectedTypeMessage);
                }

                JsonObject extra = CreateCausewayArtifact("realtime_performance");
                extra["causeway"]!["unexpected"] = 1;
                AssertVerifierRejects(wrapper, fixtureRoot, "extra.json", extra.ToJsonString(), "property set is invalid");

                foreach (string property in new[] { "commandQuantity", "previousRevision", "revision" })
                {
                    JsonObject stringNumber = CreateCausewayArtifact("realtime_performance");
                    stringNumber["causeway"]![property] = "1";
                    AssertVerifierRejects(
                        wrapper, fixtureRoot, $"string-{property}.json", stringNumber.ToJsonString(),
                        "must have native JSON integer type");
                }
                JsonObject stringBoolean = CreateCausewayArtifact("realtime_performance");
                stringBoolean["causeway"]!["accepted"] = "true";
                AssertVerifierRejects(
                    wrapper, fixtureRoot, "string-accepted.json", stringBoolean.ToJsonString(), "must have native JSON type");

                string decimalQuantity = CreateCausewayArtifact("realtime_performance").ToJsonString()
                    .Replace("\"commandQuantity\":1,", "\"commandQuantity\":1.0,", StringComparison.Ordinal);
                AssertVerifierRejects(
                    wrapper, fixtureRoot, "decimal-quantity.json", decimalQuantity, "must have native JSON integer type");

                JsonObject wrongCommand = CreateCausewayArtifact("realtime_performance");
                wrongCommand["causeway"]!["commandQuantity"] = 2;
                AssertVerifierRejects(wrapper, fixtureRoot, "quantity.json", wrongCommand.ToJsonString(), "command/event/revision evidence");

                JsonObject wrongEvent = CreateCausewayArtifact("realtime_performance");
                wrongEvent["causeway"]!["eventType"] = "causeway.material.rejected";
                AssertVerifierRejects(wrapper, fixtureRoot, "event.json", wrongEvent.ToJsonString(), "command/event/revision evidence");

                JsonObject editDrift = CreateCausewayArtifact("realtime_performance");
                editDrift["causeway"]!["afterVoxelEditStateIdentity"] = new string('c', 64);
                AssertVerifierRejects(wrapper, fixtureRoot, "edit.json", editDrift.ToJsonString(), "edit/reload equality");

                JsonObject reloadDrift = CreateCausewayArtifact("realtime_performance");
                reloadDrift["causeway"]!["reloadedStateIdentity"] = new string('c', 64);
                AssertVerifierRejects(wrapper, fixtureRoot, "reload.json", reloadDrift.ToJsonString(), "edit/reload equality");

                JsonObject realtimeReplay = CreateCausewayArtifact("realtime_performance");
                realtimeReplay["causeway"]!["replayedAfterCommandStateIdentity"] = new string('b', 64);
                AssertVerifierRejects(wrapper, fixtureRoot, "realtime-replay.json", realtimeReplay.ToJsonString(), "must not contain fixed-delta");

                JsonObject fixedMissingReplay = CreateCausewayArtifact("fixed_delta_identity");
                fixedMissingReplay["causeway"]!["replayedAfterVoxelEditStateIdentity"] = "";
                AssertVerifierRejects(wrapper, fixtureRoot, "fixed-replay.json", fixedMissingReplay.ToJsonString(), "replay equality is missing or invalid");
            }
            finally
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        [Fact]
        public void Packet02WrapperPinsV5AndResolvesRelativeOutputInsideRepository()
        {
            string repositoryRoot = FindRepositoryRoot();
            string wrapper = Path.Combine(repositoryRoot, "scripts", "run-causeway-packet-02-route.ps1");
            const string relative = "artifacts/profile-contract-wrapper";

            PowerShellResult result = RunPowerShell(wrapper, "-ProfileContractOnly", "-OutputDirectory", relative);
            Assert.Equal(0, result.ExitCode);
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput.Trim());
            JsonElement profile = document.RootElement;
            Assert.Equal("packet02-v5", profile.GetProperty("profile").GetString());
            Assert.Equal(
                Path.GetFullPath(Path.Combine(repositoryRoot, relative)),
                profile.GetProperty("outputRoot").GetString());

            string absolute = Path.GetFullPath(Path.Combine(repositoryRoot, "artifacts", "profile-contract-wrapper-absolute"));
            result = RunPowerShell(wrapper, "-ProfileContractOnly", "-OutputDirectory", absolute);
            Assert.Equal(0, result.ExitCode);
            using JsonDocument absoluteDocument = JsonDocument.Parse(result.StandardOutput.Trim());
            Assert.Equal(absolute, absoluteDocument.RootElement.GetProperty("outputRoot").GetString());
        }

        private static JsonElement RunProfile(string script, string profile, string outputDirectory)
        {
            PowerShellResult result = RunPowerShell(
                script, "-ProfileContractOnly", "-Profile", profile, "-OutputDirectory", outputDirectory);
            Assert.True(result.ExitCode == 0, result.StandardError + result.StandardOutput);
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput.Trim());
            return document.RootElement.Clone();
        }

        private static PowerShellResult VerifyAttestation(string wrapper, AttestationFixture fixture) =>
            RunPowerShell(wrapper,
                "-OutputDirectory", fixture.Root,
                "-VerifyExportAttestationOnly", fixture.IdentityPath,
                "-ExpectedAttestationRequestPath", fixture.RequestPath);

        private static AttestationFixture CreateAttestationFixture(string root, bool replace = false)
        {
            if (replace && Directory.Exists(root)) Directory.Delete(root, recursive: true);
            string release = Path.Combine(root, "release-runner");
            string projectRoot = Path.Combine(root, "project");
            Directory.CreateDirectory(release);
            Directory.CreateDirectory(projectRoot);
            string pck = Path.Combine(release, "SocietiesAcceptedSceneBaseline.pck");
            string payload = Path.Combine(release, "payload.bin");
            string executable = Path.Combine(release, "SocietiesAcceptedSceneBaseline.exe");
            string console = Path.Combine(release, "SocietiesAcceptedSceneBaseline.console.exe");
            File.WriteAllText(pck, "exact-pck");
            File.WriteAllText(payload, "exact-release-payload");
            File.WriteAllText(executable, "exact-executable");
            File.WriteAllText(console, "exact-console-wrapper");

            string exportRelease = Path.Combine(
                projectRoot, ".godot", "mono", "temp", "bin", "ExportRelease", "win-x64");
            Directory.CreateDirectory(exportRelease);
            foreach (string name in new[]
                     {
                         "Societies.dll", "Societies.pdb", "Societies.deps.json", "Societies.runtimeconfig.json"
                     })
            {
                string content = $"exact-managed-input:{name}";
                File.WriteAllText(Path.Combine(exportRelease, name), content);
                File.WriteAllText(Path.Combine(release, name), content);
            }

            string runnerScene = Path.Combine(projectRoot, "tests", "AcceptedSceneBaselineRunner.tscn");
            Directory.CreateDirectory(Path.GetDirectoryName(runnerScene)!);
            File.WriteAllText(runnerScene, "[gd_scene format=3]");
            string cache = Path.Combine(projectRoot, ".godot", "exported", "fixture", "file_cache");
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
            File.WriteAllText(cache, $"res://tests/AcceptedSceneBaselineRunner.tscn::{Md5(runnerScene)}::0::0{Environment.NewLine}");

            JsonObject request = new()
            {
                ["repositoryRoot"] = Path.GetFullPath(root),
                ["projectRoot"] = Path.GetFullPath(projectRoot),
                ["outputRoot"] = Path.GetFullPath(root),
                ["releaseDirectory"] = Path.GetFullPath(release),
                ["releaseExecutable"] = Path.GetFullPath(executable),
                ["repositoryContentIdentity"] = "source-exact",
                ["profile"] = "packet02-v5",
                ["preset"] = "Windows Accepted Scene Baseline Release",
                ["projectInputs"] = new JsonObject
                {
                    ["fileCount"] = 2,
                    ["aggregateSha256"] = "project-exact",
                    ["files"] = new JsonArray(
                        new JsonObject { ["path"] = "src/societies/data/prototype-scenarios.json", ["sha256"] = "scenario" },
                        new JsonObject { ["path"] = "src/societies/tests/AcceptedSceneBaselineRunner.cs", ["sha256"] = "runner" })
                },
                ["runnerSourceSha256"] = "runner",
                ["exportPresetsSha256"] = "preset",
                ["managedAssemblyConfiguration"] = "ExportRelease"
            };
            string requestPath = Path.Combine(root, "expected-request.json");
            File.WriteAllText(requestPath, request.ToJsonString());
            AttestationFixture fixture = new(root, projectRoot, release, requestPath,
                Path.Combine(root, "accepted-scene-export-identity.json"), pck, payload, cache);
            WriteAttestation(fixture, "completed");
            return fixture;
        }

        private static void WriteAttestation(AttestationFixture fixture, string state)
        {
            JsonNode request = JsonNode.Parse(File.ReadAllText(fixture.RequestPath))!;
            JsonObject managedInputs = new();
            string exportRelease = Path.Combine(
                fixture.ProjectRoot, ".godot", "mono", "temp", "bin", "ExportRelease", "win-x64");
            foreach (string name in new[]
                     {
                         "Societies.dll", "Societies.pdb", "Societies.deps.json", "Societies.runtimeconfig.json"
                     })
            {
                managedInputs[name] = Sha256(Path.Combine(exportRelease, name));
            }
            JsonArray releaseFiles = new(
                Directory.GetFiles(fixture.ReleaseDirectory, "*", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => (JsonNode)new JsonObject
                    {
                        ["path"] = Path.GetRelativePath(fixture.ReleaseDirectory, path).Replace('\\', '/'),
                        ["length"] = new FileInfo(path).Length,
                        ["sha256"] = Sha256(path)
                    }).ToArray());
            JsonObject attestation = new()
            {
                ["schema"] = "societies_accepted_scene_export_attestation/v2",
                ["state"] = state,
                ["request"] = request,
                ["completion"] = state == "completed" ? new JsonObject
                {
                    ["pck"] = new JsonObject
                    {
                        ["path"] = "SocietiesAcceptedSceneBaseline.pck",
                        ["length"] = new FileInfo(fixture.PckPath).Length,
                        ["sha256"] = Sha256(fixture.PckPath)
                    },
                    ["exportCache"] = new JsonObject
                    {
                        ["path"] = Path.GetRelativePath(fixture.ProjectRoot, fixture.CachePath).Replace('\\', '/'),
                        ["sha256"] = Sha256(fixture.CachePath),
                        ["sourceCount"] = 1
                    },
                    ["managedInputs"] = managedInputs,
                    ["releaseFiles"] = releaseFiles
                } : null
            };
            File.WriteAllText(fixture.IdentityPath, attestation.ToJsonString());
        }

        private static JsonObject CreateCausewayArtifact(string mode)
        {
            string before = new('a', 64);
            string after = new('b', 64);
            bool fixedDelta = mode == "fixed_delta_identity";
            return new JsonObject
            {
                ["route"] = new JsonObject { ["trialMode"] = mode, ["trialIndex"] = 1 },
                ["causeway"] = new JsonObject
                {
                    ["commandKind"] = "ContributeCommunityTimber",
                    ["commandQuantity"] = 1,
                    ["accepted"] = true,
                    ["eventType"] = "causeway.material.committed",
                    ["previousRevision"] = 0,
                    ["revision"] = 1,
                    ["beforeCommandStateIdentity"] = before,
                    ["afterCommandStateIdentity"] = after,
                    ["afterVoxelEditStateIdentity"] = after,
                    ["reloadedStateIdentity"] = after,
                    ["replayedAfterCommandStateIdentity"] = fixedDelta ? after : "",
                    ["replayedAfterVoxelEditStateIdentity"] = fixedDelta ? after : ""
                }
            };
        }

        private static void AssertVerifierRejects(
            string wrapper, string root, string fileName, string json, string expectedMessage)
        {
            string path = Path.Combine(root, fileName);
            File.WriteAllText(path, json);
            PowerShellResult result = RunPowerShell(wrapper, "-VerifyTrialCausewayArtifactOnly", path);
            AssertAllEnginesFailedWith(result, expectedMessage);
        }

        private static string Sha256(string path) =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

        private static string Md5(string path) =>
            Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

        private static PowerShellResult RunPowerShell(string script, params string[] arguments)
        {
            string[] executables = OperatingSystem.IsWindows()
                ? new[] { "pwsh", "powershell.exe" }
                : new[] { "pwsh" };
            List<EnginePowerShellResult> results = new();
            foreach (string executable in executables)
            {
                try
                {
                    ProcessStartInfo start = new(executable)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    start.ArgumentList.Add("-NoProfile");
                    start.ArgumentList.Add("-File");
                    start.ArgumentList.Add(script);
                    foreach (string argument in arguments) start.ArgumentList.Add(argument);
                    using Process process = Process.Start(start)!;
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    results.Add(new EnginePowerShellResult(executable, process.ExitCode, stdout, stderr));
                }
                catch (Win32Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Required advertised PowerShell engine '{executable}' is unavailable.", exception);
                }
            }
            Assert.NotEmpty(results);
            Assert.All(results, result => Assert.Equal(results[0].ExitCode, result.ExitCode));
            if (results[0].ExitCode == 0)
            {
                Assert.All(results, result =>
                    Assert.Equal(results[0].StandardOutput.Trim(), result.StandardOutput.Trim()));
            }
            return new PowerShellResult(results);
        }

        private static void AssertAllEnginesSucceeded(PowerShellResult result)
        {
            Assert.All(result.EngineResults, engine => Assert.True(
                engine.ExitCode == 0,
                $"{engine.Engine}: {engine.StandardError}{engine.StandardOutput}"));
        }

        private static void AssertAllEnginesFailedWith(PowerShellResult result, string expectedMessage)
        {
            Assert.All(result.EngineResults, engine =>
            {
                Assert.NotEqual(0, engine.ExitCode);
                Assert.Contains(expectedMessage, engine.StandardError + engine.StandardOutput);
            });
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "project-governance.json"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the repository root.");
        }

        private sealed record EnginePowerShellResult(
            string Engine, int ExitCode, string StandardOutput, string StandardError);

        private sealed record PowerShellResult(IReadOnlyList<EnginePowerShellResult> EngineResults)
        {
            public int ExitCode => EngineResults[0].ExitCode;
            public string StandardOutput => EngineResults[0].StandardOutput;
            public string StandardError => EngineResults[0].StandardError;
        }
        private sealed record AttestationFixture(
            string Root, string ProjectRoot, string ReleaseDirectory, string RequestPath, string IdentityPath,
            string PckPath, string PayloadPath, string CachePath);
    }
}
