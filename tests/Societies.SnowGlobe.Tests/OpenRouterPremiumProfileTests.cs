using System.Reflection;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumProfileTests
{
    [Fact]
    public void RegistryPinsApprovalTimeCatalogRoutePricingAndClosedLiveGate()
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;

        Assert.Equal("openai/gpt-5.6-luna", OpenRouterPremiumProfile.ModelIdentity);
        Assert.Equal("openai/gpt-5.6-luna-20260709", OpenRouterPremiumProfile.CanonicalModelSlug);
        Assert.Equal(1_050_000, OpenRouterPremiumProfile.ContextLengthTokens);
        Assert.Equal(200_000, OpenRouterPremiumProfile.PromptMicrousdPerMillionTokens);
        Assert.Equal(1_200_000, OpenRouterPremiumProfile.CompletionMicrousdPerMillionTokens);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", OpenRouterPremiumProfile.EffectiveUri);
        Assert.Equal("openai", OpenRouterPremiumProfile.ProviderSlug);
        Assert.Equal("OpenAI", OpenRouterPremiumProfile.ProviderResponseIdentity);
        Assert.Equal("68aabd8ed3d88a41d4b0d0856512e6008d780b39eca9606682812f9755e91e9f", OpenRouterPremiumProfile.CatalogEvidenceDigestSha256);
        Assert.Equal("5117d521a43040174b59ba95486f3aef4736e243100d61fbd7e18dc3c4d22284", OpenRouterPremiumProfile.EndpointEvidenceDigestSha256);
        Assert.False(profile.TemperatureAdvertised);
        Assert.True(profile.RequiresStructuredOutputs);
        Assert.True(profile.ZdrRequested);
        Assert.False(profile.CanonicalModelCallabilityProven);
        Assert.False(profile.LiveTrafficEnabled);
        Assert.Equal("paid_cost_ceiling_and_account_binding_unresolved", OpenRouterPremiumProfile.LiveTrafficBlockerCode);
        Assert.Contains("request_header_x-openrouter-metadata=enabled", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("success_response_field=openrouter_metadata", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Contains("source=https://openrouter.ai/docs/guides/features/router-metadata", OpenRouterPremiumProfile.EndpointEvidenceCanonicalDescriptor, StringComparison.Ordinal);
        Assert.Equal(12, profile.Bounds.RequiredScenarioCount);
        Assert.Equal(12, profile.Bounds.MaximumExchanges);
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
