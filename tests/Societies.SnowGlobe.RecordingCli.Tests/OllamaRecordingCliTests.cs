using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.RecordingCli.Tests;

public sealed class OllamaRecordingCliTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "societies-recording-cli-" + Guid.NewGuid().ToString("N"));
    private static readonly long StartTicks = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks;

    [Fact]
    public async Task PreflightIsRawFreeAndCreatesNoFilesOrDirectories()
    {
        string nonce = "cli-preflight-secret-v1"; StringWriter output = new(); StringWriter error = new();
        int exit = await RecordingProgram.RunAsync(["preflight", "--repository-root", _root, "--pid", "777", "--start-utc-ticks", StartTicks.ToString(), "--nonce", nonce], output, error);
        Assert.Equal(0, exit); Assert.Empty(error.ToString()); Assert.Contains("PREFLIGHT_ACCEPTED", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("io_performed=false", output.ToString(), StringComparison.Ordinal); Assert.Contains("live_authorized=false", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(nonce, output.ToString(), StringComparison.Ordinal); Assert.DoesNotContain(_root, output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData("record")]
    [InlineData("live")]
    [InlineData("execute")]
    [InlineData("--record")]
    public async Task EveryLiveTokenFailsBeforeConstruction(string token)
    {
        StringWriter output = new(); StringWriter error = new();
        int exit = await RecordingProgram.RunAsync([token, "--repository-root", _root], output, error);
        Assert.Equal(2, exit); Assert.Empty(output.ToString()); Assert.Equal("RECORDING_FAILED code=live_mode_not_available" + Environment.NewLine, error.ToString()); Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task UnknownDuplicateAndUnapprovedOptionsFailClosedWithoutEcho()
    {
        string secret = "do-not-echo-v1";
        foreach (string[] args in new[]
        {
            new[] { "unknown", "--nonce", secret },
            new[] { "preflight", "--repository-root", _root, "--repository-root", _root, "--pid", "1", "--start-utc-ticks", "1", "--nonce", secret },
            new[] { "preflight", "--repository-root", _root, "--model", secret },
            new[] { "validate", "--repository-root", _root, "--output-path", secret }
        })
        {
            StringWriter output = new(); StringWriter error = new(); int exit = await RecordingProgram.RunAsync(args, output, error);
            Assert.Equal(2, exit); Assert.Empty(output.ToString()); Assert.Contains("arguments_invalid", error.ToString(), StringComparison.Ordinal); Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        }
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task ValidateReadsOnlyFixedBoundedArtifactAndReportsDigest()
    {
        if (!OperatingSystem.IsWindows()) return;
        OllamaRecordingExecutionArtifact artifact = await CreateArtifact(); CreateRepositoryMarkers();
        string path = Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, artifact.CanonicalUtf8.ToArray());
        StringWriter output = new(); StringWriter error = new(); int exit = await RecordingProgram.RunAsync(["validate", "--repository-root", _root], output, error);
        Assert.Equal(0, exit); Assert.Empty(error.ToString()); Assert.Contains(artifact.CanonicalDigestSha256, output.ToString(), StringComparison.Ordinal); Assert.Contains("structurally_complete=true", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateRejectsMalformedArtifactWithoutLiveFallback()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers(); string path = Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "{}");
        StringWriter output = new(); StringWriter error = new(); int exit = await RecordingProgram.RunAsync(["validate", "--repository-root", _root], output, error);
        Assert.Equal(1, exit); Assert.Empty(output.ToString()); Assert.StartsWith("RECORDING_FAILED code=artifact_", error.ToString(), StringComparison.Ordinal);
    }

    private async Task<OllamaRecordingExecutionArtifact> CreateArtifact()
    {
        InMemoryOllamaRecordingArtifactStore store = new(); Factory factory = new(); SnowGlobePinnedOllamaRecordingModule inner = new(new Clock(), factory);
        SnowGlobeOllamaRecordingCompositionModule module = new(_root, inner, store);
        return (await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), "cli-artifact-v1"))).Artifact!;
    }

    private void CreateRepositoryMarkers()
    {
        Directory.CreateDirectory(_root); File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: offline-test"); File.WriteAllText(Path.Combine(_root, "CURRENT_BUILD.md"), "# offline test");
        string lab = Path.Combine(_root, "labs", "Societies.SnowGlobe"); Directory.CreateDirectory(lab); File.WriteAllText(Path.Combine(lab, "Societies.SnowGlobe.csproj"), "<Project />");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class Clock : ICognitionQualityRecordingSessionClock { public long NowMilliseconds => 1; }
    private sealed class Factory : IOllamaLoopbackRecordingTransportFactory
    {
        public IOfflineOllamaRecordingTransportPort Create(OllamaLoopbackRuntimeBinding binding) => new InMemoryOfflineOllamaRecordingTransportAdapter(Enumerable.Range(1, 12).Select(Wrapper).ToArray());
        private static byte[] Wrapper(int index)
        {
            string action = index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" }; int quantity = index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };
            string proposal = $"{{\"agent_id\":\"agent-00\",\"action\":\"{action}\",\"quantity\":{quantity}}}";
            return Encoding.UTF8.GetBytes($"{{\"model\":\"qwen3.5:4b\",\"created_at\":\"2026-08-18T12:00:00Z\",\"response\":{JsonSerializer.Serialize(proposal)},\"done\":true,\"done_reason\":\"stop\",\"context\":[1,2],\"total_duration\":1000000,\"load_duration\":0,\"prompt_eval_count\":10,\"prompt_eval_duration\":500000,\"eval_count\":20,\"eval_duration\":500000}}");
        }
    }
}
