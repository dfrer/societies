using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ProviderAdapterPreflightTests
{
    [Fact]
    public void ApprovedProfile_IsStableFullyBoundAndCannotBePubliclyConstructed()
    {
        FixedProviderProfile first = FixedProviderProfileRegistry.ApprovedOfflineFixture;
        FixedProviderProfile second = FixedProviderProfileRegistry.Resolve(first.Identity);

        Assert.Same(first, second);
        Assert.Equal(Hash(first.CanonicalDescriptor), first.FullProfileDigest);
        Assert.Equal("provider-profile-sha256-" + first.FullProfileDigest, first.Identity.Value);
        Assert.EndsWith(".invalid", first.Host, StringComparison.Ordinal);
        Assert.StartsWith("https://", first.EffectiveUri, StringComparison.Ordinal);
        Assert.Equal(443, first.Port);
        Assert.True(first.IsOfflineFixture);
        Assert.False(first.RedirectsAllowed);
        Assert.False(first.AutomaticRetriesAllowed);
        Assert.False(first.ProxyAllowed);
        Assert.False(first.CookiesAllowed);
        Assert.False(first.AmbientAuthenticationAllowed);
        Assert.True(first.ExactModelRequired);
        Assert.True(first.ExactUsageRequired);
        Assert.True(first.ResponseDisposalRequired);
        Assert.Empty(typeof(FixedProviderProfile).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        string[] boundValues =
        {
            first.Schema, first.Scheme, first.Host, first.Port.ToString(), first.RoutePath, first.EffectiveUri,
            first.ProviderIdentity, first.ModelIdentity, first.ModelRevisionIdentity, first.PromptRevisionIdentity,
            first.OutputSchemaIdentity, first.AccountAudienceIdentity, first.AuthenticationSchemeIdentity,
            first.Limits.MaximumRequestBytes.ToString(), first.Limits.MaximumResponseBytes.ToString(),
            first.Limits.MaximumJsonDepth.ToString(), first.Limits.MaximumInputTokens.ToString(),
            first.Limits.MaximumOutputTokens.ToString(), first.Limits.TimeoutMilliseconds.ToString(),
            first.Limits.LeaseLifetimeMilliseconds.ToString()
        };
        Assert.All(boundValues, value => Assert.Contains(value, first.CanonicalDescriptor, StringComparison.Ordinal));
        Assert.Contains("redirects=false", first.CanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("automatic-retries=false", first.CanonicalDescriptor, StringComparison.Ordinal);

        ProviderProfileIdentity unknown = new("provider-profile-sha256-" + Hash("unknown-profile"));
        Assert.Equal(ProviderPreflightReasonCode.ProfileMismatch,
            Assert.Throws<ProviderPreflightException>(() => FixedProviderProfileRegistry.Resolve(unknown)).ReasonCode);
    }

    [Fact]
    public void CallerInterfaceAndSource_AreCredentialFreeAndContainNoIoImplementation()
    {
        Type[] callerTypes =
        {
            typeof(ProviderPreflightAuthorization), typeof(ProviderExecutionBounds),
            typeof(CredentialLeaseRequest), typeof(ProviderExecutionCapability)
        };
        string[] forbiddenExact =
        {
            "Url", "Uri", "Header", "Key", "Token", "Secret", "Credential", "Price", "Retry",
            "ModelSelector", "AuthenticationMode", "Proxy", "Cookie", "Redirect"
        };
        foreach (PropertyInfo property in callerTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
            Assert.DoesNotContain(forbiddenExact, name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(typeof(CredentialLease).GetMembers(BindingFlags.Public | BindingFlags.Instance), member =>
            member switch
            {
                PropertyInfo property => property.PropertyType == typeof(byte[]) || property.PropertyType == typeof(ReadOnlyMemory<byte>),
                MethodInfo method => method.ReturnType == typeof(byte[]) || method.ReturnType == typeof(ReadOnlyMemory<byte>),
                _ => false
            });
        Assert.DoesNotContain(typeof(ProviderExecutionCapability).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => !method.IsSpecialName
                && (method.Name.Contains("Execute", StringComparison.Ordinal) || method.Name.Contains("Dispatch", StringComparison.Ordinal)));

        string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "labs", "Societies.SnowGlobe", "ProviderAdapterPreflight.cs"));
        string source = File.ReadAllText(sourcePath);
        string[] forbiddenApis =
        {
            "HttpClient", "Socket", "WebRequest", "Dns.", "Environment.", "CredentialManager",
            "File.", "Directory.", "PaymentIntent", "Process.Start"
        };
        Assert.All(forbiddenApis, api => Assert.DoesNotContain(api, source, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EveryBindingMismatchAndInvalidIdentity_FailsBeforeLeaseAcquisition()
    {
        ProviderPreflightAuthorization baseline = Authorization();
        ProviderPreflightAuthorization[] mismatches =
        {
            baseline with { ProfileIdentity = new ProviderProfileIdentity("provider-profile-sha256-" + Hash("other-profile")) },
            baseline with { ModelPolicyDigest = Hash("other-policy") },
            baseline with { FinancialJournalChecksum = Hash("other-journal") },
            baseline with { AccountBinding = Account("other-account") },
            baseline with { JobDigest = Hash("other-job") },
            baseline with { HardBounds = baseline.HardBounds with { MaximumResponseBytes = baseline.HardBounds.MaximumResponseBytes - 1 } }
        };

        foreach (ProviderPreflightAuthorization mismatch in mismatches)
        {
            OfflineProviderPreflightClock clock = new(1000);
            FakeCredentialLeaseSource source = new();
            ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(baseline, source, clock);
            await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
                await new FakeProviderExecutionProbe().ExecuteOnceAsync(capability, mismatch, source, clock));
            Assert.Equal(0, source.CallCount);
        }

        {
            OfflineProviderPreflightClock clock = new(1000);
            FakeCredentialLeaseSource primary = new();
            FakeCredentialLeaseSource wrong = new(FakeCredentialLeaseSource.SecondaryIdentity);
            ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(baseline, primary, clock);
            ProviderPreflightException error = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
                await new FakeProviderExecutionProbe().ExecuteOnceAsync(capability, baseline, wrong, clock));
            Assert.Equal(ProviderPreflightReasonCode.LeaseSourceMismatch, error.ReasonCode);
            Assert.Equal(0, primary.CallCount);
            Assert.Equal(0, wrong.CallCount);
        }

        {
            OfflineProviderPreflightClock clock = new(1000);
            FakeCredentialLeaseSource primary = new();
            FakeCredentialLeaseSource sameIdentityImpostor = new();
            ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(baseline, primary, clock);
            ProviderPreflightException error = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
                await new FakeProviderExecutionProbe().ExecuteOnceAsync(capability, baseline, sameIdentityImpostor, clock));
            Assert.Equal(ProviderPreflightReasonCode.LeaseSourceMismatch, error.ReasonCode);
            Assert.Equal(0, primary.CallCount);
            Assert.Equal(0, sameIdentityImpostor.CallCount);
        }

        {
            OfflineProviderPreflightClock clock = new(1000);
            FakeCredentialLeaseSource source = new();
            ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(baseline, source, clock);
            ProviderPreflightException error = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
                await new FakeProviderExecutionProbe().ExecuteOnceAsync(capability, baseline, source, new OfflineProviderPreflightClock(1000)));
            Assert.Equal(ProviderPreflightReasonCode.BindingMismatch, error.ReasonCode);
            Assert.Equal(0, source.CallCount);
        }

        FakeCredentialLeaseSource neverCalled = new();
        ProviderPreflightException invalid = Assert.Throws<ProviderPreflightException>(() =>
            ProviderAdapterPreflight.Authorize(baseline with { AuthorizationNonce = "INVALID NONCE" }, neverCalled, new OfflineProviderPreflightClock(0)));
        Assert.Equal(ProviderPreflightReasonCode.InvalidIdentity, invalid.ReasonCode);
        Assert.Equal(0, neverCalled.CallCount);
        Assert.Throws<ArgumentException>(() => new ProviderProfileIdentity("https://attacker.example"));

        ProviderExecutionBounds bounds = baseline.HardBounds;
        ProviderExecutionBounds[] belowMinimum =
        {
            bounds with { MaximumRequestBytes = ProviderAdapterPreflight.MinimumRequestBytes - 1 },
            bounds with { MaximumResponseBytes = ProviderAdapterPreflight.MinimumResponseBytes - 1 },
            bounds with { MaximumJsonDepth = ProviderAdapterPreflight.MinimumJsonDepth - 1 },
            bounds with { MaximumInputTokens = ProviderAdapterPreflight.MinimumInputTokens - 1 },
            bounds with { MaximumOutputTokens = ProviderAdapterPreflight.MinimumOutputTokens - 1 }
        };
        foreach (ProviderExecutionBounds invalidBounds in belowMinimum)
        {
            FakeCredentialLeaseSource source = new();
            ProviderPreflightException error = Assert.Throws<ProviderPreflightException>(() =>
                ProviderAdapterPreflight.Authorize(baseline with { HardBounds = invalidBounds }, source, new OfflineProviderPreflightClock(0)));
            Assert.Equal(ProviderPreflightReasonCode.InvalidBounds, error.ReasonCode);
            Assert.Equal(0, source.CallCount);
        }
    }

    [Fact]
    public async Task Capability_IsSingleUseConcurrentSafeExpiredAndCancelledBeforeLease()
    {
        ProviderPreflightAuthorization authorization = Authorization();
        OfflineProviderPreflightClock clock = new(1000);
        FakeCredentialLeaseSource source = new();
        FakeProviderExecutionProbe probe = new();
        ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(authorization, source, clock);

        ProviderExecutionEvidence evidence = await probe.ExecuteOnceAsync(capability, authorization, source, clock);
        Assert.Equal(ProviderProbeOutcomeCode.OfflineSuccess, evidence.OutcomeCode);
        Assert.False(capability.IsNetworkAuthorization);
        Assert.False(capability.CanDispatch);
        Assert.Equal(1, source.CallCount);
        Assert.Equal(1, probe.CallCount);

        ProviderPreflightAuthorization minimumAuthorization = authorization with
        {
            AuthorizationNonce = "offline-nonce-exact-minimum",
            HardBounds = new ProviderExecutionBounds(
                ProviderAdapterPreflight.MinimumRequestBytes,
                ProviderAdapterPreflight.MinimumResponseBytes,
                ProviderAdapterPreflight.MinimumJsonDepth,
                ProviderAdapterPreflight.MinimumInputTokens,
                ProviderAdapterPreflight.MinimumOutputTokens,
                1,
                1)
        };
        FakeCredentialLeaseSource minimumSource = new();
        ProviderExecutionCapability minimumCapability = ProviderAdapterPreflight.Authorize(minimumAuthorization, minimumSource, clock);
        ProviderExecutionEvidence minimumEvidence = await new FakeProviderExecutionProbe().ExecuteOnceAsync(minimumCapability, minimumAuthorization, minimumSource, clock);
        Assert.Equal(ProviderAdapterPreflight.MinimumRequestBytes, minimumEvidence.RequestBytes);
        Assert.Equal(ProviderAdapterPreflight.MinimumResponseBytes, minimumEvidence.ResponseBytes);
        Assert.Equal(ProviderAdapterPreflight.MinimumJsonDepth, minimumEvidence.JsonDepth);
        Assert.Equal(ProviderAdapterPreflight.MinimumInputTokens, minimumEvidence.InputTokens);
        Assert.Equal(ProviderAdapterPreflight.MinimumOutputTokens, minimumEvidence.OutputTokens);

        ProviderExecutionBounds[] digestVariants =
        {
            authorization.HardBounds with { MaximumRequestBytes = authorization.HardBounds.MaximumRequestBytes + 1 },
            authorization.HardBounds with { MaximumResponseBytes = authorization.HardBounds.MaximumResponseBytes + 1 },
            authorization.HardBounds with { MaximumJsonDepth = authorization.HardBounds.MaximumJsonDepth + 1 },
            authorization.HardBounds with { MaximumInputTokens = authorization.HardBounds.MaximumInputTokens + 1 },
            authorization.HardBounds with { MaximumOutputTokens = authorization.HardBounds.MaximumOutputTokens + 1 },
            authorization.HardBounds with { TimeoutMilliseconds = authorization.HardBounds.TimeoutMilliseconds + 1 },
            authorization.HardBounds with { LeaseLifetimeMilliseconds = authorization.HardBounds.LeaseLifetimeMilliseconds + 1 }
        };
        HashSet<string> capabilityDigests = new(StringComparer.Ordinal) { capability.CapabilityDigest };
        Assert.All(digestVariants, variant => capabilityDigests.Add(
            ProviderAdapterPreflight.Authorize(authorization with { HardBounds = variant }, new FakeCredentialLeaseSource(), new OfflineProviderPreflightClock(1000)).CapabilityDigest));
        Assert.Equal(8, capabilityDigests.Count);
        ProviderPreflightException reused = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
            await probe.ExecuteOnceAsync(capability, authorization, source, clock));
        Assert.Equal(ProviderPreflightReasonCode.CapabilityReused, reused.ReasonCode);
        Assert.Equal(1, source.CallCount);

        FakeCredentialLeaseSource concurrentSource = new();
        FakeProviderExecutionProbe concurrentProbe = new();
        ProviderExecutionCapability concurrentCapability = ProviderAdapterPreflight.Authorize(authorization with { AuthorizationNonce = "offline-nonce-concurrent" }, concurrentSource, clock);
        Task<(bool Success, ProviderPreflightReasonCode? Code)>[] attempts = Enumerable.Range(0, 16).Select(async _ =>
        {
            try
            {
                await concurrentProbe.ExecuteOnceAsync(concurrentCapability, concurrentCapability.ProfileIdentity == authorization.ProfileIdentity
                    ? authorization with { AuthorizationNonce = "offline-nonce-concurrent" }
                    : authorization, concurrentSource, clock);
                return (true, (ProviderPreflightReasonCode?)null);
            }
            catch (ProviderPreflightException error) { return (false, error.ReasonCode); }
        }).ToArray();
        (bool Success, ProviderPreflightReasonCode? Code)[] outcomes = await Task.WhenAll(attempts);
        Assert.Equal(1, outcomes.Count(outcome => outcome.Success));
        Assert.All(outcomes.Where(outcome => !outcome.Success), outcome => Assert.Equal(ProviderPreflightReasonCode.CapabilityReused, outcome.Code));
        Assert.Equal(1, concurrentSource.CallCount);
        Assert.Equal(1, concurrentProbe.CallCount);

        FakeCredentialLeaseSource nonceSource = new();
        ProviderPreflightAuthorization duplicateNonce = authorization with { AuthorizationNonce = "offline-nonce-duplicate" };
        ProviderExecutionCapability firstNonceCapability = ProviderAdapterPreflight.Authorize(duplicateNonce, nonceSource, clock);
        ProviderExecutionCapability secondNonceCapability = ProviderAdapterPreflight.Authorize(duplicateNonce, nonceSource, clock);
        await new FakeProviderExecutionProbe().ExecuteOnceAsync(firstNonceCapability, duplicateNonce, nonceSource, clock);
        ProviderPreflightException duplicateNonceError = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
            await new FakeProviderExecutionProbe().ExecuteOnceAsync(secondNonceCapability, duplicateNonce, nonceSource, clock));
        Assert.Equal(ProviderPreflightReasonCode.LeaseMisuse, duplicateNonceError.ReasonCode);
        Assert.Equal(2, nonceSource.CallCount);
        Assert.Equal(1, nonceSource.ZeroObservationCount);

        FakeCredentialLeaseSource expiredSource = new();
        ProviderExecutionCapability expired = ProviderAdapterPreflight.Authorize(authorization with { AuthorizationNonce = "offline-nonce-expired" }, expiredSource, clock);
        clock.Advance(expired.ExpiresAtMilliseconds > clock.NowMilliseconds ? (int)(expired.ExpiresAtMilliseconds - clock.NowMilliseconds) : 0);
        ProviderPreflightException expiry = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
            await new FakeProviderExecutionProbe().ExecuteOnceAsync(expired, authorization with { AuthorizationNonce = "offline-nonce-expired" }, expiredSource, clock));
        Assert.Equal(ProviderPreflightReasonCode.CapabilityExpired, expiry.ReasonCode);
        Assert.Equal(0, expiredSource.CallCount);

        FakeCredentialLeaseSource cancelledSource = new();
        ProviderPreflightAuthorization cancelledAuthorization = authorization with { AuthorizationNonce = "offline-nonce-cancelled" };
        ProviderExecutionCapability cancelled = ProviderAdapterPreflight.Authorize(cancelledAuthorization, cancelledSource, clock);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ProviderPreflightException cancelledError = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
            await new FakeProviderExecutionProbe().ExecuteOnceAsync(cancelled, cancelledAuthorization, cancelledSource, clock, cancellation.Token));
        Assert.Equal(ProviderPreflightReasonCode.Cancelled, cancelledError.ReasonCode);
        Assert.Equal(0, cancelledSource.CallCount);
    }

    [Fact]
    public async Task ExactSecretBuffer_IsZeroedOnEveryLeaseTerminalAndMisusePath()
    {
        await AssertZeroed(OfflineProviderProbeBehavior.Success, OfflineCredentialLeaseBehavior.Normal, null);
        await AssertZeroed(OfflineProviderProbeBehavior.CallbackException, OfflineCredentialLeaseBehavior.Normal, ProviderPreflightReasonCode.ProbeFailed);
        await AssertZeroed(OfflineProviderProbeBehavior.CallbackCancellation, OfflineCredentialLeaseBehavior.Normal, ProviderPreflightReasonCode.Cancelled);
        await AssertZeroed(OfflineProviderProbeBehavior.Success, OfflineCredentialLeaseBehavior.Expired, ProviderPreflightReasonCode.LeaseExpired);
        await AssertZeroed(OfflineProviderProbeBehavior.Success, OfflineCredentialLeaseBehavior.DisposedBeforeReturn, ProviderPreflightReasonCode.LeaseMisuse);
        await AssertZeroed(OfflineProviderProbeBehavior.ReentrantLeaseUse, OfflineCredentialLeaseBehavior.Normal, ProviderPreflightReasonCode.LeaseMisuse);

        OfflineProviderPreflightClock clock = new(1000);
        FakeCredentialLeaseSource source = new();
        ProviderPreflightAuthorization authorization = Authorization() with { AuthorizationNonce = "offline-nonce-reuse-zero" };
        ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(authorization, source, clock);
        FakeProviderExecutionProbe probe = new();
        await probe.ExecuteOnceAsync(capability, authorization, source, clock);
        await Assert.ThrowsAsync<ProviderPreflightException>(async () => await probe.ExecuteOnceAsync(capability, authorization, source, clock));
        Assert.True(source.LastLeaseZeroed);
        Assert.Equal(1, source.ZeroObservationCount);
        Assert.Equal(1, source.CallCount);

        await AssertZeroed(OfflineProviderProbeBehavior.Success, OfflineCredentialLeaseBehavior.AcquireException,
            ProviderPreflightReasonCode.LeaseAcquisitionFailed);

        OfflineProviderPreflightClock cancellationClock = new(2000);
        FakeCredentialLeaseSource cancellationSource = new(behavior: OfflineCredentialLeaseBehavior.CancelDuringAcquire);
        ProviderPreflightAuthorization cancellationAuthorization = Authorization() with { AuthorizationNonce = "offline-nonce-mid-acquire-cancel" };
        ProviderExecutionCapability cancellationCapability = ProviderAdapterPreflight.Authorize(cancellationAuthorization, cancellationSource, cancellationClock);
        FakeProviderExecutionProbe cancellationProbe = new();
        using CancellationTokenSource cancellation = new();
        Task<ProviderExecutionEvidence> pending = cancellationProbe.ExecuteOnceAsync(
            cancellationCapability, cancellationAuthorization, cancellationSource, cancellationClock, cancellation.Token).AsTask();
        await cancellationSource.AcquisitionStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        ProviderPreflightException cancellationError = await Assert.ThrowsAsync<ProviderPreflightException>(async () => await pending);
        Assert.Equal(ProviderPreflightReasonCode.Cancelled, cancellationError.ReasonCode);
        Assert.True(cancellationSource.LastLeaseZeroed);
        Assert.Equal(1, cancellationSource.ZeroObservationCount);
        Assert.Equal(1, cancellationSource.CallCount);
        ProviderPreflightException cancellationRetry = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
            await cancellationProbe.ExecuteOnceAsync(cancellationCapability, cancellationAuthorization, cancellationSource, cancellationClock));
        Assert.Equal(ProviderPreflightReasonCode.CapabilityReused, cancellationRetry.ReasonCode);
        Assert.Equal(1, cancellationSource.CallCount);

        ExternalLeaseSource external = new();
        OfflineProviderPreflightClock externalClock = new(3000);
        ProviderPreflightAuthorization externalAuthorization = Authorization() with { AuthorizationNonce = "offline-nonce-external-source" };
        ProviderExecutionCapability externalCapability = ProviderAdapterPreflight.Authorize(externalAuthorization, external, externalClock);
        ProviderExecutionEvidence externalEvidence = await new FakeProviderExecutionProbe().ExecuteOnceAsync(
            externalCapability, externalAuthorization, external, externalClock);
        Assert.True(external.OwnedBufferZeroed);
        Assert.DoesNotContain(ExternalLeaseSource.MaterialSentinel, externalEvidence.ToString(), StringComparison.Ordinal);

        ExternalLeaseSource faultingExternal = new();
        ProviderPreflightAuthorization faultingAuthorization = Authorization() with { AuthorizationNonce = "offline-nonce-external-fault" };
        ProviderExecutionCapability faultingCapability = ProviderAdapterPreflight.Authorize(faultingAuthorization, faultingExternal, externalClock);
        ProviderPreflightException externalError = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
            await new FakeProviderExecutionProbe(OfflineProviderProbeBehavior.CallbackException).ExecuteOnceAsync(
                faultingCapability, faultingAuthorization, faultingExternal, externalClock));
        Assert.True(faultingExternal.OwnedBufferZeroed);
        Assert.DoesNotContain(ExternalLeaseSource.MaterialSentinel, externalError.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LeaseRequestAndEvidenceAreDetachedWhileRecordedReplayRemainsSeparate()
    {
        OfflineProviderPreflightClock clock = new(4242);
        FakeCredentialLeaseSource source = new();
        ProviderPreflightAuthorization authorization = Authorization();
        ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(authorization, source, clock);
        ProviderExecutionEvidence evidence = await new FakeProviderExecutionProbe().ExecuteOnceAsync(capability, authorization, source, clock);
        CredentialLeaseRequest request = Assert.IsType<CredentialLeaseRequest>(source.LastRequest);

        FixedProviderProfile profile = FixedProviderProfileRegistry.ApprovedOfflineFixture;
        Assert.Equal(authorization.AccountBinding, request.AccountBinding);
        Assert.Equal(profile.FullProfileDigest, request.ProfileDigest);
        Assert.Equal(profile.AccountAudienceIdentity, request.AccountAudienceIdentity);
        Assert.Equal("offline-fixture-proposal-once/v1", request.ScopeIdentity);
        Assert.Equal(authorization.ModelPolicyDigest, request.ModelPolicyDigest);
        Assert.Equal(authorization.FinancialJournalChecksum, request.FinancialJournalChecksum);
        Assert.Equal(authorization.JobDigest, request.JobDigest);
        Assert.Equal(authorization.AuthorizationNonce, request.AuthorizationNonce);
        Assert.Equal(authorization.HardBounds.LeaseLifetimeMilliseconds, request.LifetimeMilliseconds);
        Assert.Equal(capability.IssuedAtMilliseconds, request.IssuedAtMilliseconds);
        Assert.Equal(capability.ExpiresAtMilliseconds, request.ExpiresAtMilliseconds);
        Assert.Equal(64, request.RequestDigest.Length);
        Assert.False(evidence.HasWorldAuthority);
        Assert.True(evidence.ResponseDisposed);
        Assert.NotNull(evidence.Proposal);
        SnowGlobeActionProposal changed = evidence.Proposal! with { Quantity = 99 };
        Assert.NotEqual(changed, evidence.Proposal);

        Assert.Equal(new[]
        {
            SnowGlobeLedgerKind.Response, SnowGlobeLedgerKind.Proposal, SnowGlobeLedgerKind.Commit,
            SnowGlobeLedgerKind.Event, SnowGlobeLedgerKind.Checkpoint, SnowGlobeLedgerKind.ParticipantEvaluation
        }, Enum.GetValues<SnowGlobeLedgerKind>());
        Assert.DoesNotContain(typeof(SnowGlobeReplayAdapter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => typeof(ICredentialLeaseSource).IsAssignableFrom(field.FieldType));

        SnowGlobeObservation observation = new("agent-00", 0, 0, 10, 10, 0, 0, 0, 0);
        RecordedInferenceAdapter recorded = new(new[]
        {
            new SnowGlobeRecordedResponse(observation, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), 1)
        });
        Assert.Equal(SnowGlobeActionKind.Idle, (await recorded.ProposeAsync(observation, default)).Action);
        Assert.Equal(1, source.CallCount); // Only the explicit preflight above; replay acquired no lease.
    }

    private static async Task AssertZeroed(
        OfflineProviderProbeBehavior probeBehavior,
        OfflineCredentialLeaseBehavior leaseBehavior,
        ProviderPreflightReasonCode? expectedError)
    {
        OfflineProviderPreflightClock clock = new(1000);
        FakeCredentialLeaseSource source = new(behavior: leaseBehavior);
        ProviderPreflightAuthorization authorization = Authorization() with
        {
            AuthorizationNonce = "offline-nonce-zero-" + probeBehavior.ToString().ToLowerInvariant() + "-" + leaseBehavior.ToString().ToLowerInvariant()
        };
        ProviderExecutionCapability capability = ProviderAdapterPreflight.Authorize(authorization, source, clock);
        FakeProviderExecutionProbe probe = new(probeBehavior);
        if (expectedError is null)
            await probe.ExecuteOnceAsync(capability, authorization, source, clock);
        else
        {
            ProviderPreflightException error = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
                await probe.ExecuteOnceAsync(capability, authorization, source, clock));
            Assert.Equal(expectedError, error.ReasonCode);
            ProviderPreflightException retry = await Assert.ThrowsAsync<ProviderPreflightException>(async () =>
                await probe.ExecuteOnceAsync(capability, authorization, source, clock));
            Assert.Equal(ProviderPreflightReasonCode.CapabilityReused, retry.ReasonCode);
        }
        Assert.Equal(1, source.CallCount);
        Assert.True(source.LastLeaseZeroed);
        Assert.Equal(1, source.ZeroObservationCount);
    }

    private static ProviderPreflightAuthorization Authorization() => new(
        FixedProviderProfileRegistry.ApprovedOfflineFixture.Identity,
        Hash("model-policy"),
        Hash("financial-journal-header"),
        Account("account"),
        Hash("job"),
        new ProviderExecutionBounds(4096, 4096, 6, 1024, 128, 10_000, 2_000),
        "offline-nonce-0001");

    private static ByokAccountBindingIdentity Account(string value) => new("byok-account-sha256-" + Hash(value));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class ExternalLeaseSource : ICredentialLeaseSource
    {
        internal const string MaterialSentinel = "offline-external-fixture-material";
        private byte[]? _owned;
        public string Identity => "external-test-lease-source/v1";
        public bool OwnedBufferZeroed => _owned is not null && _owned.All(value => value == 0);
        public ValueTask<CredentialLease> AcquireOnceAsync(CredentialLeaseRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _owned = Encoding.UTF8.GetBytes(MaterialSentinel);
            return ValueTask.FromResult(new CredentialLease(_owned, request.ExpiresAtMilliseconds));
        }
    }
}
