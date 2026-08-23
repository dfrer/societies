using System.Buffers;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumStateGenerationStoreTests
{
    private const long Now = 1_777_118_400_000L;
    private static int _evidenceNonce;

    [Fact]
    public void StoreSurfaceIsInternalPathOpaqueAndHasNoScanRepairOrRetryApi()
    {
        Type store = typeof(OpenRouterPremiumStateGenerationStore);
        Assert.False(store.IsPublic);
        Assert.All(store.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            constructor => Assert.False(constructor.IsPublic));
        string[] forbidden = ["delete", "move", "rename", "archive", "repair", "enumerate", "scan", "compact", "retry", "resume", "import", "migrate"];
        Assert.DoesNotContain(store.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => forbidden.Any(token => method.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));
        foreach (Type capability in new[]
                 { typeof(OpenRouterPremiumGenerationWriter), typeof(OpenRouterPremiumExecutionGeneration),
                     typeof(OpenRouterPremiumValidationGeneration), typeof(OpenRouterPremiumResolvedGeneration) })
        {
            Assert.DoesNotContain(capability.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("FileIdentity", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void TwoCommittedGenerationsRemainCreateNewStableAndResolveByAuthorizationWithoutEnumeration()
    {
        string root = Temp(); TrackingIoObserver observer = new();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            SequenceGenerationIdSource ids = new(Hex('a'), Hex('b'));
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, ids, observer: observer);
            (string firstId, string firstAuthority) = Commit(store, "first", Now);
            Dictionary<string, string> firstInventory = HashInventory(Path.Combine(root, "generations", firstId));

            (string secondId, string secondAuthority) = Commit(store, "second", Now + 1);
            Dictionary<string, string> afterSecond = HashInventory(Path.Combine(root, "generations", firstId));

            Assert.NotEqual(firstId, secondId);
            Assert.NotEqual(firstAuthority, secondAuthority);
            Assert.Equal(firstInventory, afterSecond);
            OpenRouterPremiumStateGenerationStore restarted = new(root, anchor,
                new SequenceGenerationIdSource(Hex('c')), observer: observer);
            observer.ExactPaths.Clear();
            using OpenRouterPremiumResolvedGeneration resolved = restarted.ResolveAuthority(firstAuthority);
            Assert.Equal(firstId, resolved.GenerationId);
            Assert.Equal(firstAuthority, resolved.AuthorizationDigestSha256);
            Assert.Equal(0, observer.DirectoryEnumerationCount);
            Assert.DoesNotContain(observer.ExactPaths, path => path.Contains(secondId, StringComparison.Ordinal));
            Assert.DoesNotContain(observer.ExactPaths, path => path.Contains("v1", StringComparison.OrdinalIgnoreCase));
        }
        finally { Delete(root); }
    }

    [Fact]
    public void GenerationIdAndAuthorizationLocatorCollisionsFailWithoutRetryOrOverwrite()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            SequenceGenerationIdSource collision = new(Hex('a'), Hex('a'));
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, collision);
            using (OpenRouterPremiumGenerationWriter ignored = store.BeginPreflight(Now)) { }
            OpenRouterPremiumProductionException generationError = Assert.Throws<OpenRouterPremiumProductionException>(
                () => store.BeginPreflight(Now + 1));
            Assert.Equal("generation_collision", generationError.Code);
            Assert.Equal(2, collision.CallCount);

            string otherRoot = Temp();
            try
            {
                using TestTrustAnchor otherAnchor = Anchor('d');
                byte[] fixedCiphertext = Encoding.ASCII.GetBytes("same-protected-authorization");
                OpenRouterPremiumStateGenerationStore duplicateStore = new(otherRoot, otherAnchor,
                    new SequenceGenerationIdSource(Hex('b'), Hex('c')));
                string first = Commit(duplicateStore, fixedCiphertext, Now).Authority;
                OpenRouterPremiumProductionException locatorError = Assert.Throws<OpenRouterPremiumProductionException>(
                    () => Commit(duplicateStore, fixedCiphertext, Now + 1));
                Assert.Equal("authority_locator_collision", locatorError.Code);
                using OpenRouterPremiumResolvedGeneration resolved = duplicateStore.ResolveAuthority(first);
                Assert.Equal("g2-" + Hex('b'), resolved.GenerationId);
            }
            finally { Delete(otherRoot); }
        }
        finally { Delete(root); }
    }

    [Fact]
    public void RootClaimsSurviveGenerationOnlyRollbackAndRemainConsumedWhileClaimsSurvive()
    {
        string root = Temp(); string snapshot = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "rollback", Now);
            CopyDirectoryExact(Path.Combine(root, "generations", generationId), snapshot);

            using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
            {
                execution.WriteEvidence(CanonicalEvidence(generationId, execution.Journal));
            }

            string generation = Path.Combine(root, "generations", generationId);
            Directory.Delete(generation, recursive: true);
            CopyDirectoryExact(snapshot, generation);

            OpenRouterPremiumStateGenerationStore restarted = new(root, anchor, new SequenceGenerationIdSource(Hex('b')));
            OpenRouterPremiumProductionException executionError = Assert.Throws<OpenRouterPremiumProductionException>(
                () => restarted.OpenForExecution(authority, Now + 2));
            Assert.Equal("execution_consumed_indeterminate", executionError.Code);
            OpenRouterPremiumProductionException validationError = Assert.Throws<OpenRouterPremiumProductionException>(
                () => restarted.OpenForValidation(authority, Now + 2));
            Assert.Equal("execution_consumed_indeterminate", validationError.Code);
        }
        finally { Delete(root); Delete(snapshot); }
    }

    [Fact]
    public void ExecutionAndValidationClaimsAreDurableBeforeCapabilitiesAndNeverReopen()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "claims", Now);
            using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
            {
                Assert.True(File.Exists(Path.Combine(root, "execution-consumed", authority + ".json")));
                execution.WriteEvidence(CanonicalEvidence(generationId, execution.Journal));
            }
            Assert.Equal("execution_already_consumed", Assert.Throws<OpenRouterPremiumProductionException>(
                () => store.OpenForExecution(authority, Now + 2)).Code);

            using (OpenRouterPremiumValidationGeneration validation = store.OpenForValidation(authority, Now + 2))
            {
                Assert.True(File.Exists(Path.Combine(root, "validation-consumed", authority + ".json")));
                Assert.Contains("evidence", Encoding.UTF8.GetString(validation.EvidenceBytes.Span), StringComparison.Ordinal);
            }
            Assert.Equal("validation_consumed_failed", Assert.Throws<OpenRouterPremiumProductionException>(
                () => store.OpenForValidation(authority, Now + 3)).Code);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void CompletedValidationReceiptRemainsStableAndValidationRemainsConsumed()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "receipt", Now);
            using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
                execution.WriteEvidence(CanonicalEvidence(generationId, execution.Journal));
            using (OpenRouterPremiumValidationGeneration validation = store.OpenForValidation(authority, Now + 2))
                validation.WriteReceipt();

            Assert.Equal("validation_already_consumed", Assert.Throws<OpenRouterPremiumProductionException>(
                () => store.OpenForValidation(authority, Now + 3)).Code);
            Assert.ThrowsAny<Exception>(() => store.OpenForExecution(authority, Now + 3));
            string generation = Assert.Single(Directory.EnumerateDirectories(Path.Combine(root, "generations")));
            File.Delete(Path.Combine(generation, "validation-receipt.binding.json"));
            Assert.Equal("validation_consumed_failed", Assert.Throws<OpenRouterPremiumProductionException>(
                () => new OpenRouterPremiumStateGenerationStore(root, anchor).OpenForValidation(authority, Now + 4)).Code);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ClaimAndPostClaimCrashBoundariesRemainConsumedWhileClaimsSurvive()
    {
        foreach (string boundary in new[]
                 {
                     OpenRouterPremiumStateGenerationStore.BoundaryExecutionClaim,
                     OpenRouterPremiumStateGenerationStore.BoundaryEvidence,
                     OpenRouterPremiumStateGenerationStore.BoundaryEvidenceBinding,
                     OpenRouterPremiumStateGenerationStore.BoundaryValidationClaim,
                     OpenRouterPremiumStateGenerationStore.BoundaryValidationReceipt,
                     OpenRouterPremiumStateGenerationStore.BoundaryReceiptBinding
                 })
        {
            string root = Temp();
            try
            {
                using TestTrustAnchor anchor = Anchor();
                ThrowAfterBoundary fault = new(boundary);
                OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                    new SequenceGenerationIdSource(Hex('a')), fault);
                (string generationId, string authority) = Commit(store, "post-claim-crash", Now);
                if (boundary == OpenRouterPremiumStateGenerationStore.BoundaryExecutionClaim)
                {
                    Assert.Throws<InjectedCrashException>(() => store.OpenForExecution(authority, Now + 1));
                    Assert.Equal("execution_consumed_indeterminate", Assert.Throws<OpenRouterPremiumProductionException>(
                        () => new OpenRouterPremiumStateGenerationStore(root, anchor).OpenForExecution(authority, Now + 2)).Code);
                    continue;
                }

                if (boundary is OpenRouterPremiumStateGenerationStore.BoundaryEvidence
                    or OpenRouterPremiumStateGenerationStore.BoundaryEvidenceBinding)
                {
                    using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
                        Assert.Throws<InjectedCrashException>(() => execution.WriteEvidence(
                            CanonicalEvidence(generationId, execution.Journal)));
                    OpenRouterPremiumStateGenerationStore restarted = new(root, anchor);
                    if (boundary == OpenRouterPremiumStateGenerationStore.BoundaryEvidence)
                    {
                        Assert.Equal("execution_consumed_indeterminate", Assert.Throws<OpenRouterPremiumProductionException>(
                            () => restarted.OpenForValidation(authority, Now + 2)).Code);
                    }
                    else
                    {
                        using OpenRouterPremiumValidationGeneration validation = restarted.OpenForValidation(authority, Now + 2);
                        validation.WriteReceipt();
                    }
                    continue;
                }

                using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
                    execution.WriteEvidence(CanonicalEvidence(generationId, execution.Journal));

                if (boundary == OpenRouterPremiumStateGenerationStore.BoundaryValidationClaim)
                {
                    Assert.Throws<InjectedCrashException>(() => store.OpenForValidation(authority, Now + 2));
                }
                else
                {
                    using OpenRouterPremiumValidationGeneration validation = store.OpenForValidation(authority, Now + 2);
                    Assert.Throws<InjectedCrashException>(() => validation.WriteReceipt());
                }
                string expected = boundary == OpenRouterPremiumStateGenerationStore.BoundaryReceiptBinding
                    ? "validation_already_consumed" : "validation_consumed_failed";
                Assert.Equal(expected, Assert.Throws<OpenRouterPremiumProductionException>(
                    () => new OpenRouterPremiumStateGenerationStore(root, anchor).OpenForValidation(authority, Now + 3)).Code);
            }
            finally { Delete(root); }
        }
    }

    [Fact]
    public void CrossGenerationCopyAndGenerationPathSwapAreRejected()
    {
        string root = Temp(); string moved = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                new SequenceGenerationIdSource(Hex('a'), Hex('b')));
            (string firstId, string firstAuthority) = Commit(store, "first-copy", Now);
            (string secondId, _) = Commit(store, "second-copy", Now + 1);
            string firstPreflight = Path.Combine(root, "generations", firstId, "preflight-artifact.json");
            File.Delete(firstPreflight);
            File.Copy(Path.Combine(root, "generations", secondId, "preflight-artifact.json"), firstPreflight);
            Assert.ThrowsAny<Exception>(() => store.ResolveAuthority(firstAuthority));

            string firstGeneration = Path.Combine(root, "generations", firstId);
            Directory.Delete(firstGeneration, recursive: true);
            CopyDirectoryExact(Path.Combine(root, "generations", secondId), firstGeneration);
            Assert.ThrowsAny<Exception>(() => new OpenRouterPremiumStateGenerationStore(root, anchor).ResolveAuthority(firstAuthority));
        }
        finally { Delete(root); Delete(moved); }
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("truncated")]
    [InlineData("oversized")]
    [InlineData("deep")]
    [InlineData("duplicate")]
    [InlineData("mixed-schema")]
    public void MalformedOrMixedGenerationManifestFailsClosed(string attack)
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "manifest", Now);
            string manifest = Path.Combine(root, "generations", generationId, "generation-manifest.json");
            byte[] original = File.ReadAllBytes(manifest);
            byte[] hostile = attack switch
            {
                "corrupt" => Encoding.UTF8.GetBytes("not-json"),
                "truncated" => original[..^1],
                "oversized" => new byte[OpenRouterPremiumStateGenerationStore.MaximumManifestBytes + 1],
                "deep" => Encoding.UTF8.GetBytes("{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{}}}}}}"),
                "duplicate" => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original).Replace("{",
                    "{\"schema_version\":\"" + OpenRouterPremiumStateGenerationStore.GenerationManifestSchema + "\",",
                    StringComparison.Ordinal)),
                _ => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original).Replace(
                    "snow_globe_openrouter_authenticated_state/v2",
                    "snow_globe_openrouter_authenticated_state/v1", StringComparison.Ordinal))
            };
            File.WriteAllBytes(manifest, hostile);
            Assert.ThrowsAny<Exception>(() => store.ResolveAuthority(authority));
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ReparseGenerationAndCaseChangedRootAreRejected()
    {
        string root = Temp(); string target = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "reparse", Now);
            string generation = Path.Combine(root, "generations", generationId);
            Directory.Delete(generation, recursive: true);
            Directory.CreateSymbolicLink(generation, target);
            Assert.ThrowsAny<Exception>(() => store.ResolveAuthority(authority));
            Assert.Equal("state_root_invalid", Assert.Throws<OpenRouterPremiumProductionException>(() =>
                new OpenRouterPremiumStateGenerationStore(root.ToUpperInvariant(), anchor)).Code);
        }
        finally { Delete(root); Delete(target); }
    }

    [Fact]
    public void ReparseArtifactAndMalformedRootClaimsAreTerminal()
    {
        string root = Temp(); string outside = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore reparseStore = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(reparseStore, "artifact-reparse", Now);
            string outsideFile = Path.Combine(outside, "outside.dpapi");
            File.WriteAllBytes(outsideFile, Encoding.ASCII.GetBytes("outside"));
            string runtime = Path.Combine(root, "generations", generationId, "runtime-authorization.dpapi");
            File.Delete(runtime);
            File.CreateSymbolicLink(runtime, outsideFile);
            Assert.ThrowsAny<Exception>(() => reparseStore.ResolveAuthority(authority));

            string claimsRoot = Temp();
            try
            {
                using TestTrustAnchor claimAnchor = Anchor('d');
                OpenRouterPremiumStateGenerationStore claimStore = new(claimsRoot, claimAnchor, new SequenceGenerationIdSource(Hex('b')));
                (string claimGenerationId, string claimAuthority) = Commit(claimStore, "claim-corrupt", Now);
                using (OpenRouterPremiumExecutionGeneration execution = claimStore.OpenForExecution(claimAuthority, Now + 1))
                    execution.WriteEvidence(CanonicalEvidence(claimGenerationId, execution.Journal));
                File.WriteAllText(Path.Combine(claimsRoot, "execution-consumed", claimAuthority + ".json"),
                    "{\"malformed\":true}", Encoding.UTF8);
                Assert.Equal("execution_consumed_indeterminate", Assert.Throws<OpenRouterPremiumProductionException>(
                    () => claimStore.OpenForExecution(claimAuthority, Now + 2)).Code);
                Assert.Equal("execution_consumed_indeterminate", Assert.Throws<OpenRouterPremiumProductionException>(
                    () => claimStore.OpenForValidation(claimAuthority, Now + 2)).Code);
            }
            finally { Delete(claimsRoot); }
        }
        finally { Delete(root); Delete(outside); }
    }

    [Fact]
    public void RootWriterLeaseRejectsConcurrentAndStaleOrCorruptLockState()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                new SequenceGenerationIdSource(Hex('a'), Hex('b')));
            using OpenRouterPremiumGenerationWriter held = store.BeginPreflight(Now);
            OpenRouterPremiumProductionException busy = Assert.Throws<OpenRouterPremiumProductionException>(
                () => store.BeginPreflight(Now + 1));
            Assert.Equal("state_writer_busy", busy.Code);
            held.Dispose();

            File.WriteAllText(Path.Combine(root, "root-writer.lock"), "stale-or-corrupt", Encoding.ASCII);
            OpenRouterPremiumProductionException stale = Assert.Throws<OpenRouterPremiumProductionException>(
                () => new OpenRouterPremiumStateGenerationStore(root, anchor, new SequenceGenerationIdSource(Hex('c'))));
            Assert.Equal("state_writer_lock_invalid", stale.Code);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("truncated")]
    [InlineData("oversized")]
    [InlineData("deep")]
    [InlineData("duplicate")]
    [InlineData("mixed-schema")]
    [InlineData("wrong-generation")]
    public void MalformedOrMixedLocatorStateFailsClosed(string attack)
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (_, string authority) = Commit(store, "locator", Now);
            string locator = Path.Combine(root, "authorities", authority + ".json");
            byte[] original = File.ReadAllBytes(locator);
            byte[] hostile = attack switch
            {
                "corrupt" => Encoding.UTF8.GetBytes("not-json"),
                "truncated" => original[..^1],
                "oversized" => new byte[OpenRouterPremiumStateGenerationStore.MaximumLocatorBytes + 1],
                "deep" => Encoding.UTF8.GetBytes("{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{}}}}}}}}"),
                "duplicate" => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original).Replace("{",
                    "{\"schema_version\":\"" + OpenRouterPremiumStateGenerationStore.AuthorityLocatorSchema + "\",",
                    StringComparison.Ordinal)),
                "mixed-schema" => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original).Replace(
                    "snow_globe_openrouter_authenticated_state/v2",
                    "snow_globe_openrouter_authenticated_state/v1", StringComparison.Ordinal)),
                _ => original.ToArray()
            };
            if (attack == "wrong-generation") hostile[hostile.Length / 2] ^= 1;
            File.WriteAllBytes(locator, hostile);
            Assert.ThrowsAny<Exception>(() => store.ResolveAuthority(authority));
        }
        finally { Delete(root); }
    }

    [Fact]
    public void HardLinksPathSwapsAndNonCanonicalRootsAreRejected()
    {
        string root = Temp(); string aliases = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "identity", Now);
            string runtime = Path.Combine(root, "generations", generationId, "runtime-authorization.dpapi");
            CreateHardLinkExact(Path.Combine(aliases, "runtime.alias"), runtime);
            Assert.ThrowsAny<Exception>(() => store.OpenForExecution(authority, Now + 1));

            Assert.Equal("state_root_invalid", Assert.Throws<OpenRouterPremiumProductionException>(() =>
                new OpenRouterPremiumStateGenerationStore("relative-v2", anchor, new SequenceGenerationIdSource(Hex('b')))).Code);
            Assert.Equal("state_root_invalid", Assert.Throws<OpenRouterPremiumProductionException>(() =>
                new OpenRouterPremiumStateGenerationStore(root + ":ads", anchor, new SequenceGenerationIdSource(Hex('b')))).Code);
        }
        finally { Delete(root); Delete(aliases); }
    }

    [Fact]
    public void CrashAfterEachDurableStoreBoundaryNeverResumesOrReusesAPartialGeneration()
    {
        foreach (string boundary in OpenRouterPremiumStateGenerationStore.DurableBoundaryNames)
        {
            string root = Temp();
            try
            {
                using TestTrustAnchor anchor = Anchor();
                ThrowAfterBoundary fault = new(boundary);
                OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                    new SequenceGenerationIdSource(Hex('a'), Hex('b')), fault);
                Assert.Throws<InjectedCrashException>(() => Commit(store, "crash", Now));

                OpenRouterPremiumStateGenerationStore restarted = new(root, anchor,
                    new SequenceGenerationIdSource(Hex('b')));
                if (boundary == OpenRouterPremiumStateGenerationStore.BoundaryAuthorityLocator)
                {
                    string authority = OpenRouterPremiumCanonical.Digest(Encoding.ASCII.GetBytes("protected-crash"));
                    using OpenRouterPremiumResolvedGeneration committed = restarted.ResolveAuthority(authority);
                    Assert.Equal("g2-" + Hex('a'), committed.GenerationId);
                }
                else
                {
                    using OpenRouterPremiumGenerationWriter next = restarted.BeginPreflight(Now + 1);
                    Assert.Equal("g2-" + Hex('b'), next.GenerationId);
                }
            }
            finally { Delete(root); }
        }
    }

    [Fact]
    public void WrongExternalAnchorAndCoordinatedArtifactLocatorRewriteFailClosedAcrossRestart()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            using TestTrustAnchor wrongAnchor = Anchor('b');
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "anchored", Now);

            Assert.Equal("state_writer_lock_invalid", Assert.Throws<OpenRouterPremiumProductionException>(() =>
                new OpenRouterPremiumStateGenerationStore(root, wrongAnchor)).Code);

            string activation = Path.Combine(root, "generations", generationId, "activation-bundle.json");
            byte[] changedActivation = File.ReadAllBytes(activation);
            changedActivation[^2] ^= 1;
            File.WriteAllBytes(activation, changedActivation);
            string newDigest = OpenRouterPremiumCanonical.Digest(changedActivation);
            string locator = Path.Combine(root, "authorities", authority + ".json");
            RewriteEnvelope(locator, payload => ReplaceDigest(payload,
                ReadEnvelopePayload(locator, "activation_bundle_digest_sha256"), newDigest), resignWith: null);

            Assert.ThrowsAny<Exception>(() => new OpenRouterPremiumStateGenerationStore(root, anchor)
                .ResolveAuthority(authority));
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ExternalAnchorSecretMaterialIsNeverPersistedUnderTheStateRoot()
    {
        string root = Temp();
        byte[] sentinel = Enumerable.Range(1, 32).Select(value => checked((byte)value)).ToArray();
        try
        {
            using TestTrustAnchor anchor = new(sentinel);
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            _ = Commit(store, "secret-sentinel", Now);
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                Assert.Equal(-1, File.ReadAllBytes(file).AsSpan().IndexOf(sentinel));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sentinel);
            Delete(root);
        }
    }

    [Fact]
    public void CoordinatedEvidenceAndBindingRewriteCannotForgeCompletedExecution()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "evidence-anchor", Now);
            byte[] first;
            using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
            {
                first = CanonicalEvidence(generationId, execution.Journal);
                execution.WriteEvidence(first);
            }
            byte[] second = CanonicalEvidence(generationId);
            Assert.NotEqual(OpenRouterPremiumCanonical.Digest(first), OpenRouterPremiumCanonical.Digest(second));
            string generation = Path.Combine(root, "generations", generationId);
            File.WriteAllBytes(Path.Combine(generation, "live-evidence.json"), second);
            string binding = Path.Combine(generation, "live-evidence.binding.json");
            RewriteEnvelope(binding, payload => ReplaceDigest(payload,
                OpenRouterPremiumCanonical.Digest(first), OpenRouterPremiumCanonical.Digest(second)), resignWith: null);

            Assert.Equal("execution_consumed_indeterminate", Assert.Throws<OpenRouterPremiumProductionException>(() =>
                new OpenRouterPremiumStateGenerationStore(root, anchor).OpenForValidation(authority, Now + 2)).Code);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void SelectedProfileDigestIsRevalidatedEvenForCorrectlyAuthenticatedState()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "profile", Now);
            string manifest = Path.Combine(root, "generations", generationId, "generation-manifest.json");
            string oldManifestDigest = OpenRouterPremiumCanonical.Digest(File.ReadAllBytes(manifest));
            RewriteEnvelope(manifest, payload => ReplaceDigest(payload,
                OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256, Hex('0')), anchor);
            string newManifestDigest = OpenRouterPremiumCanonical.Digest(File.ReadAllBytes(manifest));
            string locator = Path.Combine(root, "authorities", authority + ".json");
            RewriteEnvelope(locator, payload => ReplaceDigest(payload, oldManifestDigest, newManifestDigest), anchor);

            Assert.ThrowsAny<Exception>(() => new OpenRouterPremiumStateGenerationStore(root, anchor)
                .ResolveAuthority(authority));
        }
        finally { Delete(root); }
    }

    [Fact]
    public void LocatorCommitFreezesReturnedJournalAndValidationHasNoCallerReceiptInput()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor, new SequenceGenerationIdSource(Hex('a')));
            using OpenRouterPremiumGenerationWriter writer = store.BeginPreflight(Now);
            writer.WriteActivationBundle(Encoding.UTF8.GetBytes("{\"fake\":\"activation\"}"));
            FileOpenRouterPremiumJournal journal = writer.CreateJournal(Header(writer.GenerationId));
            journal.Dispose();
            Assert.Throws<ObjectDisposedException>(() => journal.Snapshot());
            Assert.ThrowsAny<IOException>(() => FileOpenRouterPremiumJournal.OpenForAppend(
                Path.Combine(root, "generations", writer.GenerationId, "journal")));
            writer.WritePreflightArtifact(Encoding.UTF8.GetBytes("{\"fake\":\"preflight\"}"));
            _ = writer.PublishAuthorization(Encoding.ASCII.GetBytes("protected-freeze"));

            Assert.Throws<InvalidDataException>(() => FileOpenRouterPremiumJournal.OpenForAppend(
                Path.Combine(root, "generations", writer.GenerationId, "journal")));
            Assert.All(typeof(OpenRouterPremiumValidationGeneration).GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == "WriteReceipt"), method => Assert.Empty(method.GetParameters()));
        }
        finally { Delete(root); }
    }

    [Fact]
    public void CanonicalEvidenceIsRequiredAfterClaimAndFixedDirectoryMutationIsBlocked()
    {
        string root = Temp(); string moved = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                new SequenceGenerationIdSource(Hex('a'), Hex('b')));
            string authorities = Path.Combine(root, "authorities");
            string movedAuthorities = Path.Combine(moved, "authorities");
            using (OpenRouterPremiumGenerationWriter held = store.BeginPreflight(Now))
            {
                Exception? mutationFailure = Record.Exception(() =>
                    Directory.Move(authorities, movedAuthorities));
                Assert.NotNull(mutationFailure);
                Assert.True(mutationFailure is IOException or UnauthorizedAccessException);
                Assert.True(Directory.Exists(authorities));
                Assert.False(Directory.Exists(movedAuthorities));
            }
            (string generationId, string authority) = Commit(store, "canonical", Now + 1);
            using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 2))
                Assert.Equal("artifact_rejected", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
                    execution.WriteEvidence(Encoding.UTF8.GetBytes("{\"fake\":true}"))).Code);
            Assert.Equal("execution_consumed_indeterminate", Assert.Throws<OpenRouterPremiumProductionException>(() =>
                store.OpenForValidation(authority, Now + 3)).Code);
            Assert.NotEmpty(generationId);
        }
        finally { Delete(root); Delete(moved); }
    }

    [Fact]
    public async Task DisposeCannotReleaseWriterLeaseWhileEvidencePublicationIsInFlight()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            BlockingBoundary fault = new(OpenRouterPremiumStateGenerationStore.BoundaryEvidence);
            OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                new SequenceGenerationIdSource(Hex('a'), Hex('b')), fault);
            (string generationId, string authority) = Commit(store, "dispose-race", Now);
            OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1);
            Task publish = Task.Run(() => execution.WriteEvidence(CanonicalEvidence(generationId, execution.Journal)));
            Assert.True(fault.Entered.Wait(TimeSpan.FromSeconds(5)));
            ManualResetEventSlim disposeStarted = new();
            Task dispose = Task.Run(() => { disposeStarted.Set(); execution.Dispose(); });
            Assert.True(disposeStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal("state_writer_busy", Assert.Throws<OpenRouterPremiumProductionException>(() =>
                store.BeginPreflight(Now + 2)).Code);
            fault.Release.Set();
            await Task.WhenAll(publish, dispose);
            using OpenRouterPremiumGenerationWriter next = store.BeginPreflight(Now + 3);
            Assert.Equal("g2-" + Hex('b'), next.GenerationId);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ClaimedExecutionCanUsePinnedFrozenJournalWhilePublicReopenRemainsBlocked()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "claimed-journal", Now);
            string journalPath = Path.Combine(root, "generations", generationId, "journal");
            Assert.Throws<InvalidDataException>(() => FileOpenRouterPremiumJournal.OpenForAppend(journalPath));
            using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
            {
                Assert.True(execution.Journal.RestartEvidence.RestartVerified);
                execution.WriteEvidence(CanonicalEvidence(generationId, execution.Journal));
            }
            Assert.Throws<InvalidDataException>(() => FileOpenRouterPremiumJournal.OpenForAppend(journalPath));
            using OpenRouterPremiumValidationGeneration validation = store.OpenForValidation(authority, Now + 2);
            validation.WriteReceipt();
        }
        finally { Delete(root); }
    }

    [Fact]
    public void FinalJournalMutationAfterEvidencePublicationFailsAuthenticatedRestartValidation()
    {
        string root = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                new SequenceGenerationIdSource(Hex('a')));
            (string generationId, string authority) = Commit(store, "final-journal-mutation", Now);
            using (OpenRouterPremiumExecutionGeneration execution = store.OpenForExecution(authority, Now + 1))
                execution.WriteEvidence(CanonicalEvidence(generationId, execution.Journal));
            string records = Path.Combine(root, "generations", generationId, "journal",
                FileOpenRouterPremiumJournal.RecordsFileName);
            byte[] bytes = File.ReadAllBytes(records);
            bytes[^2] ^= 1;
            File.WriteAllBytes(records, bytes);
            Assert.Equal("execution_consumed_indeterminate",
                Assert.Throws<OpenRouterPremiumProductionException>(() =>
                    new OpenRouterPremiumStateGenerationStore(root, anchor)
                        .OpenForValidation(authority, Now + 2)).Code);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void GenerationsParentRenameAtCreationFailsCleanlyWithoutAcceptedAuthority()
    {
        string root = Temp(); string movedRoot = Temp();
        try
        {
            using TestTrustAnchor anchor = Anchor();
            RenameGenerationsParent fault = new(root, movedRoot);
            OpenRouterPremiumStateGenerationStore store = new(root, anchor,
                new SequenceGenerationIdSource(Hex('a')), fault);
            OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
                () => store.BeginPreflight(Now));
            Assert.Equal("generation_identity_invalid", error.Code);
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "authorities")));
            Assert.True(fault.MutationBlocked);
            Assert.True(Directory.Exists(Path.Combine(root, "generations")));
            Assert.False(Directory.Exists(Path.Combine(movedRoot, "generations")));
        }
        finally { Delete(root); Delete(movedRoot); }
    }

    [Fact]
    public void InitializationRaceCannotRedirectChildCreationThroughAReparsePoint()
    {
        string root = Temp(); string outside = Temp();
        string racedPath = Path.Combine(root, "generations");
        try
        {
            using TestTrustAnchor anchor = Anchor();
            CreateReparseOnExactPath observer = new(racedPath, outside);
            OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(() =>
                new OpenRouterPremiumStateGenerationStore(root, anchor,
                    new SequenceGenerationIdSource(Hex('a')), observer: observer));
            Assert.Equal("state_directory_invalid", error.Code);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
            Assert.False(Directory.Exists(Path.Combine(root, "authorities")));
            Assert.False(File.Exists(Path.Combine(root, "root-writer.lock")));
            Assert.Equal(1, observer.InjectionCount);
        }
        finally
        {
            try { if (Directory.Exists(racedPath)) Directory.Delete(racedPath); } catch { }
            Delete(root); Delete(outside);
        }
    }

    [Fact]
    public void FixedChildInitializationLeaseRejectsInPlaceRootReparseMutationBeforeCreate()
    {
        const int ErrorSharingViolation = 32;
        string root = Temp(); string outside = Temp();
        string generations = Path.Combine(root, "generations");
        try
        {
            using TestTrustAnchor anchor = Anchor();
            AttemptDirectoryMutationOnExactPath observer = new(generations, root);
            _ = new OpenRouterPremiumStateGenerationStore(root, anchor,
                new SequenceGenerationIdSource(Hex('a')), observer: observer);
            Assert.Equal(1, observer.AttemptCount);
            Assert.True(observer.MutationHandleWasInvalid);
            Assert.Equal(ErrorSharingViolation, observer.MutationOpenError);
            Assert.True(Directory.Exists(generations));
            Assert.False(Directory.Exists(Path.Combine(outside, "generations")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
        }
        finally { Delete(root); Delete(outside); }
    }

    private static (string GenerationId, string Authority) Commit(
        OpenRouterPremiumStateGenerationStore store, string label, long now) =>
        Commit(store, Encoding.ASCII.GetBytes("protected-" + label), now);

    private static (string GenerationId, string Authority) Commit(
        OpenRouterPremiumStateGenerationStore store, byte[] ciphertext, long now)
    {
        using OpenRouterPremiumGenerationWriter writer = store.BeginPreflight(now);
        writer.WriteActivationBundle(Encoding.UTF8.GetBytes("{\"fake\":\"activation\"}"));
        using (FileOpenRouterPremiumJournal journal = writer.CreateJournal(Header(label: writer.GenerationId))) { }
        writer.WritePreflightArtifact(Encoding.UTF8.GetBytes("{\"fake\":\"preflight\"}"));
        string authority = writer.PublishAuthorization(ciphertext);
        return (writer.GenerationId, authority);
    }

    private static OpenRouterPremiumJournalHeader Header(string label)
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        return OpenRouterPremiumJournalHeader.Create(
            "openrouter-premium-production-journal/v1", "run-" + label, profile,
            new ByokAccountBindingIdentity("byok-account-sha256-" + Hex('c')));
    }

    private static byte[] CanonicalEvidence(string generationId, IOpenRouterPremiumJournal? targetJournal = null)
    {
        OpenRouterPremiumJournalHeader header = Header(generationId);
        IOpenRouterPremiumJournal journal = targetJournal ?? new InMemoryOpenRouterPremiumJournal(header);
        FakeCredentialLeaseSource leases = new();
        ScriptedOpenRouterPremiumExchange exchange = ScriptedOpenRouterPremiumExchange.CreateSuccessful();
        OfflineOpenRouterPremiumClock clock = new(1_000);
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        OpenRouterPremiumAuthorization authorization = new(profile.Identity,
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
            OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
            new ByokAccountBindingIdentity(header.AccountBindingIdentity), header.JournalIdentity,
            header.HeaderChecksumSha256, exchange.Identity, exchange.ContractDigestSha256, leases.Identity,
            "openrouter-premium-generation-store-test/" + Interlocked.Increment(ref _evidenceNonce),
            1_000, 1_000 + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds);
        OpenRouterPremiumEvidenceArtifact artifact = OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            OpenRouterPremiumEvidenceModule.Authorize(authorization), exchange, leases, journal, clock,
            CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return artifact.CanonicalUtf8.ToArray();
    }

    private static TestTrustAnchor Anchor(char value = 'a')
    {
        byte[] key = Enumerable.Repeat((byte)value, 32).ToArray();
        try { return new TestTrustAnchor(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static string ReadEnvelopePayload(string path, string propertyName)
    {
        byte[] envelope = File.ReadAllBytes(path);
        byte[] payload = [];
        try
        {
            using JsonDocument outer = JsonDocument.Parse(envelope);
            payload = Convert.FromBase64String(outer.RootElement.GetProperty("payload_base64").GetString()!);
            using JsonDocument inner = JsonDocument.Parse(payload);
            return inner.RootElement.GetProperty(propertyName).GetString()!;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(envelope);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static byte[] ReplaceDigest(byte[] payload, string oldDigest, string newDigest)
    {
        string original = Encoding.UTF8.GetString(payload);
        string changed = original.Replace(oldDigest, newDigest, StringComparison.Ordinal);
        Assert.NotEqual(original, changed);
        return Encoding.UTF8.GetBytes(changed);
    }

    private static void RewriteEnvelope(string path, Func<byte[], byte[]> rewritePayload,
        TestTrustAnchor? resignWith)
    {
        byte[] original = File.ReadAllBytes(path);
        byte[] payload = [];
        byte[] changed = [];
        byte[] authenticated = [];
        byte[] rewritten = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(original);
            JsonElement root = document.RootElement;
            string schema = root.GetProperty("schema_version").GetString()!;
            string kind = root.GetProperty("artifact_kind").GetString()!;
            string anchorIdentity = root.GetProperty("trust_anchor_identity_sha256").GetString()!;
            string originalAuthenticator = root.GetProperty("authenticator_sha256").GetString()!;
            payload = Convert.FromBase64String(root.GetProperty("payload_base64").GetString()!);
            changed = rewritePayload(payload);
            string payloadDigest = OpenRouterPremiumCanonical.Digest(changed);
            string payloadBase64 = Convert.ToBase64String(changed);
            authenticated = WriteJson(writer =>
            {
                writer.WriteString("schema_version", schema);
                writer.WriteString("artifact_kind", kind);
                writer.WriteString("trust_anchor_identity_sha256", anchorIdentity);
                writer.WriteString("payload_digest_sha256", payloadDigest);
                writer.WriteString("payload_base64", payloadBase64);
            });
            string authenticator = resignWith?.Authenticate(authenticated) ?? originalAuthenticator;
            rewritten = WriteJson(writer =>
            {
                writer.WriteString("schema_version", schema);
                writer.WriteString("artifact_kind", kind);
                writer.WriteString("trust_anchor_identity_sha256", anchorIdentity);
                writer.WriteString("payload_digest_sha256", payloadDigest);
                writer.WriteString("payload_base64", payloadBase64);
                writer.WriteString("authenticator_sha256", authenticator);
            });
            File.WriteAllBytes(path, rewritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(original);
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(changed);
            CryptographicOperations.ZeroMemory(authenticated);
            CryptographicOperations.ZeroMemory(rewritten);
        }
    }

    private static byte[] WriteJson(Action<Utf8JsonWriter> properties)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        properties(writer);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static string Hex(char value) => new(value, 64);
    private static string Temp()
    {
        string path = Path.Combine(Path.GetTempPath(), "snow-globe-generation-v2-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static Dictionary<string, string> HashInventory(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal)
        .ToDictionary(path => Path.GetRelativePath(root, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    private static void CopyDirectoryExact(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: false);
    }

    private sealed class TestTrustAnchor : IOpenRouterPremiumStateTrustAnchor, IDisposable
    {
        private readonly object _gate = new();
        private byte[]? _key;

        internal TestTrustAnchor(byte[] key)
        {
            _key = key.ToArray();
            IdentitySha256 = OpenRouterPremiumCanonical.Digest(_key);
        }

        public string IdentitySha256 { get; }

        public string Authenticate(ReadOnlySpan<byte> canonicalBytes)
        {
            lock (_gate)
            {
                if (_key is null) throw new ObjectDisposedException(nameof(TestTrustAnchor));
                return Convert.ToHexString(HMACSHA256.HashData(_key, canonicalBytes)).ToLowerInvariant();
            }
        }

        public bool Verify(ReadOnlySpan<byte> canonicalBytes, string authenticatorSha256)
        {
            if (authenticatorSha256 is not { Length: 64 }) return false;
            byte[] expected = Convert.FromHexString(Authenticate(canonicalBytes));
            byte[] actual;
            try { actual = Convert.FromHexString(authenticatorSha256); }
            catch { CryptographicOperations.ZeroMemory(expected); return false; }
            try { return CryptographicOperations.FixedTimeEquals(expected, actual); }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(actual);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                byte[]? owned = _key;
                _key = null;
                if (owned is not null) CryptographicOperations.ZeroMemory(owned);
            }
        }
    }

    private sealed class SequenceGenerationIdSource(params string[] values) : IOpenRouterPremiumGenerationIdSource
    {
        private readonly Queue<string> _values = new(values);
        public int CallCount { get; private set; }
        public string NextGenerationHex()
        {
            CallCount++;
            return _values.Dequeue();
        }
    }

    private sealed class TrackingIoObserver : IOpenRouterPremiumStateIoObserver
    {
        public int DirectoryEnumerationCount { get; private set; }
        public List<string> ExactPaths { get; } = [];
        public void OnExactPath(string path) => ExactPaths.Add(path);
        public void OnDirectoryEnumeration() => DirectoryEnumerationCount++;
    }

    private sealed class CreateReparseOnExactPath(string targetPath, string outside)
        : IOpenRouterPremiumStateIoObserver
    {
        public int InjectionCount { get; private set; }
        public void OnExactPath(string path)
        {
            if (InjectionCount == 0 && string.Equals(path, targetPath, StringComparison.Ordinal))
            {
                Directory.CreateSymbolicLink(targetPath, outside);
                InjectionCount++;
            }
        }
        public void OnDirectoryEnumeration() =>
            throw new InvalidOperationException("Directory enumeration is forbidden.");
    }

    private sealed class AttemptDirectoryMutationOnExactPath(string targetPath, string mutationPath)
        : IOpenRouterPremiumStateIoObserver
    {
        public int AttemptCount { get; private set; }
        public bool MutationHandleWasInvalid { get; private set; }
        public int MutationOpenError { get; private set; }

        public void OnExactPath(string path)
        {
            if (AttemptCount != 0 || !string.Equals(path, targetPath, StringComparison.Ordinal)) return;
            using SafeFileHandle mutationHandle = CreateFileForMutationAttempt(mutationPath,
                0x40000000u, // GENERIC_WRITE is required by FSCTL_SET_REPARSE_POINT.
                FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero, 3,
                0x02000000u | 0x00200000u, IntPtr.Zero);
            MutationHandleWasInvalid = mutationHandle.IsInvalid;
            MutationOpenError = Marshal.GetLastWin32Error();
            AttemptCount++;
        }

        public void OnDirectoryEnumeration() =>
            throw new InvalidOperationException("Directory enumeration is forbidden.");
    }

    private sealed class ThrowAfterBoundary(string target) : IOpenRouterPremiumStateFaultInjector
    {
        public void AfterDurableBoundary(string boundary)
        {
            if (boundary == target) throw new InjectedCrashException();
        }
    }

    private sealed class BlockingBoundary(string target) : IOpenRouterPremiumStateFaultInjector
    {
        internal ManualResetEventSlim Entered { get; } = new();
        internal ManualResetEventSlim Release { get; } = new();
        public void AfterDurableBoundary(string boundary)
        {
            if (boundary != target) return;
            Entered.Set();
            if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
        }
    }

    private sealed class RenameGenerationsParent(string root, string movedRoot)
        : IOpenRouterPremiumStateFaultInjector
    {
        internal bool MutationBlocked { get; private set; }

        public void AfterDurableBoundary(string boundary)
        {
            if (boundary == OpenRouterPremiumStateGenerationStore.BoundaryGenerationDirectory)
            {
                try { Directory.Move(Path.Combine(root, "generations"), Path.Combine(movedRoot, "generations")); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    MutationBlocked = true;
                    throw new OpenRouterPremiumProductionException("generation_identity_invalid");
                }
            }
        }
    }

    private sealed class InjectedCrashException : Exception;

    private static void CreateHardLinkExact(string link, string target)
    {
        if (!CreateHardLinkW(link, target, IntPtr.Zero))
            throw new InvalidOperationException("Unable to create test hard link.",
                new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileForMutationAttempt(
        string fileName, uint desiredAccess, FileShare shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
}
