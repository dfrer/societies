using System.Reflection;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumProfileTests
{
    [Fact]
    public async Task SelectedProfileCredentialLeaseOutlivesObservedFiveSecondResponseBoundary()
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        Assert.Equal(OpenRouterPremiumProfile.CredentialLeaseLifetimeMilliseconds,
            profile.Bounds.CredentialLeaseLifetimeMilliseconds);
        Assert.Equal(OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds,
            profile.Bounds.TimeoutMilliseconds);
        Assert.True(profile.Bounds.TimeoutMilliseconds >=
            (profile.Bounds.MaximumExchanges * profile.Bounds.CredentialLeaseLifetimeMilliseconds)
            + OpenRouterPremiumProfile.RuntimeAuthorizationOverheadMilliseconds);
        const long nowMilliseconds = 1_000;
        byte[] owned = [1, 2, 3, 4];
        bool observedZero = false;
        using CredentialLease lease = new(
            owned,
            nowMilliseconds + profile.Bounds.CredentialLeaseLifetimeMilliseconds,
            zeroed => observedZero = zeroed);

        int result = await lease.ExecuteOnceAsync(
            nowMilliseconds,
            async (_, token) =>
            {
                await Task.Delay(5_200, token);
                return 1;
            },
            CancellationToken.None);

        Assert.Equal(1, result);
        Assert.True(observedZero);
        Assert.All(owned, value => Assert.Equal(0, value));
    }

    [Fact]
    public void RegistryPinsApprovalTimeCatalogRoutePricingAndClosedLiveGate()
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;

        Assert.Equal("openai/gpt-5.6-luna", OpenRouterPremiumProfile.ModelIdentity);
        Assert.Equal("openai/gpt-5.6-luna", OpenRouterPremiumProfile.CanonicalModelSlug);
        Assert.Equal("2026-07-09", OpenRouterPremiumProfile.ModelReleaseDateUtc);
        Assert.Equal("openai/gpt-5.6-luna-20260709", OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity);
        Assert.Equal(1_050_000, OpenRouterPremiumProfile.ContextLengthTokens);
        Assert.Equal(200_000, OpenRouterPremiumProfile.PromptMicrousdPerMillionTokens);
        Assert.Equal(1_200_000, OpenRouterPremiumProfile.CompletionMicrousdPerMillionTokens);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", OpenRouterPremiumProfile.EffectiveUri);
        Assert.Equal("azure", OpenRouterPremiumProfile.ProviderSlug);
        Assert.Equal("Azure", OpenRouterPremiumProfile.ProviderResponseIdentity);
        Assert.Equal("74b9c8f5467dbb7cb9f4cbcaf685ae0705e197388f44785f049dbf95841172d3", OpenRouterPremiumProfile.CatalogEvidenceDigestSha256);
        Assert.Contains("api_model_id=openai/gpt-5.6-luna", OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("release_revision_path=openai/gpt-5.6-luna-20260709", OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("canonical_slug=openai/gpt-5.6-luna-20260709", OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("pricing_override_provenance=authenticated_current_api_and_frozen_model_page", OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("pricing_override_authenticated_current=true", OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("pricing_override_reachable=false", OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Equal("b9d379c321f7746f619b40cc9b3bfd7f99f7e9bb1c1e29f460f694f2e0f9aa6b", OpenRouterPremiumProfile.EndpointEvidenceDigestSha256);
        Assert.False(profile.TemperatureAdvertised);
        Assert.True(profile.RequiresStructuredOutputs);
        Assert.True(profile.ZdrRequested);
        Assert.False(profile.CanonicalModelCallabilityProven);
        Assert.False(profile.LiveTrafficEnabled);
        Assert.Equal("paid_cost_ceiling_and_account_binding_unresolved", OpenRouterPremiumProfile.LiveTrafficBlockerCode);
        Assert.Contains("request_header_x-openrouter-metadata=enabled", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("success_response_field=openrouter_metadata", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("source=https://openrouter.ai/docs/guides/features/router-metadata", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("routing_source=https://openrouter.ai/docs/guides/routing/provider-selection", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("provider_sort=price", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("provider_max_price_prompt_usd_per_million_tokens=0.2", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("provider_max_price_completion_usd_per_million_tokens=1.2", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("max_completion_tokens=512", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("reasoning_effort=minimal", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("reasoning_exclude=true", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Equal(12, profile.Bounds.RequiredScenarioCount);
        Assert.Equal(12, profile.Bounds.MaximumExchanges);
        Assert.Equal(512, profile.Bounds.MaximumOutputTokens);
        long maximumTokenCostMicrousd = checked(((long)profile.Bounds.MaximumInputTokens
            * OpenRouterPremiumProfile.PromptMicrousdPerMillionTokens
            + (long)profile.Bounds.MaximumOutputTokens
            * OpenRouterPremiumProfile.CompletionMicrousdPerMillionTokens
            + 999_999) / 1_000_000);
        Assert.True(maximumTokenCostMicrousd <= profile.Bounds.PerSlotCostCeilingMicrousd,
            $"Maximum token exposure {maximumTokenCostMicrousd} exceeds the per-slot reservation.");
        Assert.Equal(profile.Bounds.PerSlotCostCeilingMicrousd * 12, profile.Bounds.AggregateCostCeilingMicrousd);
        Assert.Matches("^[0-9a-f]{64}$", profile.ProfileDigestSha256);
    }

    [Fact]
    public void PublicSurfaceDoesNotExposeTransportOrParserKnobs()
    {
        string[] forbidden = ["endpoint", "uri", "host", "header", "retry", "redirect", "proxy", "cookie", "parser", "model"];
        MethodInfo[] methods = typeof(OpenRouterPremiumEvidenceModule).GetMethods(BindingFlags.Public | BindingFlags.Static);

        Assert.Equal(new[] { "Authorize", "ExecuteOnceAsync" }, methods.Select(method => method.Name).OrderBy(value => value, StringComparer.Ordinal));
        foreach (ParameterInfo parameter in methods.SelectMany(method => method.GetParameters()))
            Assert.DoesNotContain(forbidden, term => parameter.Name!.Contains(term, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(IOpenRouterPremiumExchange).GetProperties(), property =>
            property.Name.Contains("Offline", StringComparison.OrdinalIgnoreCase));
    }
}
