using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Societies.SnowGlobe;

public sealed class LocalModelBenchmarkException : Exception
{
    public LocalModelBenchmarkException(string code, Exception? innerException = null)
        : base(code, innerException) => Code = code;

    public string Code { get; }
}

public sealed record OllamaVerifiedModelProvenance(
    string RuntimeModelReference,
    string ArtifactDigestSha256,
    long ArtifactSizeBytes,
    string ArtifactFormat,
    string ModelFamily,
    string ParameterSize,
    string QuantizationLevel,
    string OllamaProcessIdentity,
    int OllamaProcessId);

/// <summary>
/// Provenance-bearing wrapper around frozen metrics evidence. ObservedSampledPeakVramMiB is explicitly
/// a bounded sampled observation, not a guarantee that an unsampled transient peak did not occur.
/// </summary>
public sealed record OllamaBenchmarkRunResult(
    LocalModelBenchmarkEvidence Evidence,
    OllamaVerifiedModelProvenance Provenance,
    double ObservedSampledPeakVramMiB,
    double MaximumAllowedObservedVramMiB,
    int OutputTokenLimit,
    int VramSampleCount,
    bool ClientSideSerializationEnforced,
    bool ExternalServerStartupConfigurationVerified,
    string ExternalServerStartupConfigurationClaim);

internal static class OllamaBenchmarkContract
{
    private const int MaximumRuntimeFieldLength = 128;
    private static readonly Regex RuntimeReferencePattern = new(
        @"\A[a-z0-9][a-z0-9._/-]{0,126}:[A-Za-z0-9][A-Za-z0-9._-]{0,62}\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex DigestPattern = new(
        @"\A[0-9a-f]{64}\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    internal static void ValidateExactPlan(LocalModelBenchmarkPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        LocalModelPreflightResult reconstructed = LocalModelAdapterPreflight.Validate(new LocalModelAdapterPreflightRequest
        {
            Endpoint = plan.Endpoint,
            SharedServerIdentity = plan.SharedServerIdentity,
            AdapterIdentity = plan.AdapterIdentity,
            PromptIdentity = plan.PromptIdentity,
            ModelIdentity = plan.ModelIdentity,
            QuantizationIdentity = plan.QuantizationIdentity,
            ContextWindowTokens = plan.ContextWindowTokens,
            AuthenticationMode = "none",
            CredentialReferences = Array.Empty<string>(),
            Budgets = plan.Budgets,
            Benchmark = new LocalModelBenchmarkRequirements
            {
                WarmupRequestCount = plan.WarmupRequestCount,
                MeasuredRequestCount = plan.MeasuredRequestCount
            }
        });
        if (!reconstructed.IsValid
            || reconstructed.BenchmarkPlan != plan
            || !string.Equals(plan.SharedServerIdentity, OllamaBenchmarkRunner.SharedServerIdentity, StringComparison.Ordinal)
            || !string.Equals(plan.AdapterIdentity, OllamaBenchmarkRunner.AdapterIdentity, StringComparison.Ordinal)
            || !string.Equals(plan.PromptIdentity, OllamaBenchmarkRunner.PromptIdentity, StringComparison.Ordinal))
        {
            throw new LocalModelBenchmarkException("benchmark_plan_invalid_or_unsupported");
        }
    }

    internal static void ValidateRuntimeAuthorization(OllamaRuntimeAuthorization runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (IsCloudRuntimeReference(runtime.RuntimeModelReference))
        {
            throw new LocalModelBenchmarkException("cloud_model_rejected");
        }
        if (!IsRuntimeReference(runtime.RuntimeModelReference)
            || !DigestPattern.IsMatch(runtime.ArtifactDigestSha256)
            || runtime.ArtifactSizeBytes <= 0
            || !string.Equals(runtime.ArtifactFormat, "gguf", StringComparison.Ordinal)
            || !IsSubstantiveMetadata(runtime.ModelFamily, allowUppercase: false)
            || !IsSubstantiveMetadata(runtime.ParameterSize, allowUppercase: true)
            || !IsSubstantiveMetadata(runtime.QuantizationLevel, allowUppercase: true)
            || !IsCanonicalProcessIdentity(runtime.OllamaProcessIdentity)
            || runtime.OllamaProcessId <= 0)
        {
            throw new LocalModelBenchmarkException("runtime_authorization_invalid");
        }
    }

    internal static string ComputePlanDigest(LocalModelBenchmarkPlan plan)
    {
        string material = string.Join('\n', new[]
        {
            plan.SchemaVersion, plan.ContractId, plan.Endpoint,
            plan.Port.ToString(CultureInfo.InvariantCulture),
            plan.SharedServerIdentity, plan.AdapterIdentity, plan.PromptIdentity,
            plan.ModelIdentity, plan.QuantizationIdentity,
            plan.ContextWindowTokens.ToString(CultureInfo.InvariantCulture),
            plan.Budgets.RequestBytes.ToString(CultureInfo.InvariantCulture),
            plan.Budgets.OutputBytes.ToString(CultureInfo.InvariantCulture),
            plan.Budgets.QueueDepth.ToString(CultureInfo.InvariantCulture),
            plan.Budgets.RequestTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture),
            plan.Budgets.TotalQueueWaitMilliseconds.ToString(CultureInfo.InvariantCulture),
            plan.VramBudgetMiB.ToString(CultureInfo.InvariantCulture),
            plan.WarmupRequestCount.ToString(CultureInfo.InvariantCulture),
            plan.MeasuredRequestCount.ToString(CultureInfo.InvariantCulture),
            plan.MetricsSampleSchema, plan.MetricsSampleDigestAlgorithm, plan.MetricsSampleContentPolicy,
            plan.LatencyPercentileMethod, plan.QueueWaitAggregationMethod,
            plan.MaximumFailureCount.ToString(CultureInfo.InvariantCulture),
            plan.MaximumFallbackCount.ToString(CultureInfo.InvariantCulture),
            plan.ServerTopology, plan.ResponseAuthority,
            plan.DeterministicCommitValidationRequired.ToString(CultureInfo.InvariantCulture),
            plan.FollowRedirects.ToString(CultureInfo.InvariantCulture),
            plan.AutomaticRetries.ToString(CultureInfo.InvariantCulture),
            plan.CredentialsPermitted.ToString(CultureInfo.InvariantCulture),
            plan.EnvironmentReadsPermitted.ToString(CultureInfo.InvariantCulture),
            plan.PaidCallsPermitted.ToString(CultureInfo.InvariantCulture),
            plan.ExecutionAuthorized.ToString(CultureInfo.InvariantCulture)
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    internal static bool IsRuntimeReference(string? value) =>
        value is not null && value.Length <= MaximumRuntimeFieldLength && RuntimeReferencePattern.IsMatch(value);

    internal static bool IsCloudRuntimeReference(string? value)
    {
        if (value is null) return false;
        int separator = value.LastIndexOf(':');
        if (separator < 0 || separator == value.Length - 1) return false;
        ReadOnlySpan<char> tag = value.AsSpan(separator + 1);
        return tag.Equals("cloud", StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("-cloud", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsDigest(string? value) => value is not null && DigestPattern.IsMatch(value);

    internal static bool IsBoundedAscii(string? value, bool allowUppercase)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumRuntimeFieldLength) return false;
        foreach (char character in value)
        {
            bool valid = character is >= 'a' and <= 'z'
                || allowUppercase && character is >= 'A' and <= 'Z'
                || character is >= '0' and <= '9'
                || character is '.' or '_' or '-';
            if (!valid) return false;
        }
        return true;
    }

    internal static bool IsSubstantiveMetadata(string? value, bool allowUppercase)
    {
        if (!IsBoundedAscii(value, allowUppercase)) return false;
        return value!.ToLowerInvariant() is not (
            "unknown" or "none" or "null" or "unspecified" or "unset" or "tbd"
            or "na" or "n_a" or "n-a" or "n.a"
            or "not_available" or "not-available" or "not_applicable" or "not-applicable");
    }

    private static bool IsCanonicalProcessIdentity(string? value) =>
        IsBoundedAscii(value, allowUppercase: false) && value!.StartsWith("ollama", StringComparison.Ordinal);
}

/// <summary>
/// Process-wide endpoint admission for the one authoritative lab process. Queue capacity is frozen
/// for the lifetime of an endpoint-and-contract policy, and PeakQueueDepth is the highest global
/// admission depth observed so far for that policy, including active and waiting requests.
/// </summary>
internal static class OllamaEndpointAdmissionRegistry
{
    private static readonly ConcurrentDictionary<string, EndpointPolicy> Policies = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, AdmissionEntry> Entries = new(StringComparer.Ordinal);

    internal static async Task<AdmissionLease> AcquireAsync(
        string endpoint,
        string contractId,
        int queueDepth,
        int maximumWaitMilliseconds,
        ILocalModelBenchmarkClock clock,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EndpointPolicy policy = Policies.GetOrAdd(endpoint, _ => new EndpointPolicy(contractId, queueDepth));
        policy.ValidateContractAndCapacity(contractId, queueDepth);
        policy.ThrowIfPoisoned();
        long started = clock.GetTimestamp();
        AdmissionEntry entry;
        while (true)
        {
            entry = Entries.GetOrAdd(endpoint, _ => new AdmissionEntry(endpoint, policy));
            if (entry.TryAddReference()) break;
            Entries.TryRemove(new KeyValuePair<string, AdmissionEntry>(endpoint, entry));
        }

        AdmissionWait wait;
        try
        {
            wait = entry.Enqueue();
        }
        catch
        {
            ReleaseReference(entry);
            throw;
        }

        if (!wait.IsImmediate)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(maximumWaitMilliseconds));
            try
            {
                bool granted = await wait.Completion!.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                if (!granted)
                {
                    ReleaseReference(entry);
                    throw new LocalModelBenchmarkException("endpoint_poisoned");
                }
            }
            catch (OperationCanceledException exception)
            {
                AdmissionWaitCancellation outcome = entry.CancelWait(wait);
                if (outcome == AdmissionWaitCancellation.Granted) entry.ReleaseActive();
                ReleaseReference(entry);
                if (outcome == AdmissionWaitCancellation.Poisoned)
                {
                    throw new LocalModelBenchmarkException("endpoint_poisoned", exception);
                }
                if (cancellationToken.IsCancellationRequested) throw;
                throw new LocalModelBenchmarkException("queue_wait_timeout", exception);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            entry.ReleaseActive();
            ReleaseReference(entry);
            cancellationToken.ThrowIfCancellationRequested();
        }

        long ended = clock.GetTimestamp();
        double waitMilliseconds = clock.GetElapsedMilliseconds(started, ended);
        if (!double.IsFinite(waitMilliseconds)
            || waitMilliseconds < 0
            || waitMilliseconds > maximumWaitMilliseconds)
        {
            entry.ReleaseActive();
            ReleaseReference(entry);
            throw new LocalModelBenchmarkException("queue_wait_invalid");
        }

        return new AdmissionLease(entry, policy, waitMilliseconds, ReleaseReference);
    }

    private static void ReleaseReference(AdmissionEntry entry)
    {
        if (entry.ReleaseReference())
        {
            Entries.TryRemove(new KeyValuePair<string, AdmissionEntry>(entry.Endpoint, entry));
        }
    }

    internal static int RetainedLateResponseObserverCount(string endpoint) =>
        Policies.TryGetValue(endpoint, out EndpointPolicy? policy)
            ? policy.RetainedLateResponseObserverCount
            : 0;

    internal sealed class EndpointPolicy
    {
        private int _peakDepth;
        private int _poisoned;
        private readonly object _lateResponseGate = new();
        private Task? _lateResponseObserver;

        internal EndpointPolicy(string contractId, int queueDepth)
        {
            ContractId = contractId;
            QueueDepth = queueDepth;
        }
        internal string ContractId { get; }
        internal int QueueDepth { get; }
        internal int PeakDepth => Volatile.Read(ref _peakDepth);
        internal bool IsPoisoned => Volatile.Read(ref _poisoned) != 0;
        internal int RetainedLateResponseObserverCount
        {
            get
            {
                lock (_lateResponseGate) return _lateResponseObserver is null ? 0 : 1;
            }
        }

        internal void ValidateContractAndCapacity(string contractId, int queueDepth)
        {
            if (queueDepth != QueueDepth)
            {
                throw new LocalModelBenchmarkException("queue_capacity_mismatch");
            }
            if (!string.Equals(contractId, ContractId, StringComparison.Ordinal))
            {
                throw new LocalModelBenchmarkException("queue_contract_mismatch");
            }
        }

        internal void ThrowIfPoisoned()
        {
            if (Volatile.Read(ref _poisoned) != 0)
            {
                throw new LocalModelBenchmarkException("endpoint_poisoned");
            }
        }

        internal void ObserveDepth(int depth)
        {
            int observed = Volatile.Read(ref _peakDepth);
            while (depth > observed)
            {
                int prior = Interlocked.CompareExchange(ref _peakDepth, depth, observed);
                if (prior == observed) return;
                observed = prior;
            }
        }

        internal LateResponseObserverRegistration? Poison(
            Task<LocalModelBenchmarkTransportResponse>? lateResponseTask = null)
        {
            Interlocked.Exchange(ref _poisoned, 1);
            if (lateResponseTask is null) return null;
            lock (_lateResponseGate)
            {
                if (_lateResponseObserver is not null) return null;
                LateResponseObserverRegistration registration = new(lateResponseTask);
                _lateResponseObserver = registration.Completion;
                return registration;
            }
        }

        internal sealed class LateResponseObserverRegistration
        {
            private readonly Task<LocalModelBenchmarkTransportResponse> _lateResponseTask;
            private readonly TaskCompletionSource<bool> _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _started;

            internal LateResponseObserverRegistration(
                Task<LocalModelBenchmarkTransportResponse> lateResponseTask) =>
                _lateResponseTask = lateResponseTask;

            internal Task Completion => _completion.Task;

            internal void Start()
            {
                if (Interlocked.Exchange(ref _started, 1) != 0)
                {
                    throw new InvalidOperationException("Late-response observer already started.");
                }

                _ = Task.Run(ObserveAndDisposeAsync);
            }

            private async Task ObserveAndDisposeAsync()
            {
                try
                {
                    await using LocalModelBenchmarkTransportResponse response =
                        await _lateResponseTask.ConfigureAwait(false);
                }
                catch
                {
                    // Completion is observed; transport failure or cancellation requires no response disposal.
                }
                finally
                {
                    _completion.TrySetResult(true);
                }
            }
        }
    }

    internal sealed class AdmissionLease : IDisposable
    {
        private AdmissionEntry? _entry;
        private readonly EndpointPolicy _policy;
        private readonly Action<AdmissionEntry> _releaseReference;

        internal AdmissionLease(
            AdmissionEntry entry,
            EndpointPolicy policy,
            double queueWaitMilliseconds,
            Action<AdmissionEntry> releaseReference)
        {
            _entry = entry;
            _policy = policy;
            QueueWaitMilliseconds = queueWaitMilliseconds;
            _releaseReference = releaseReference;
        }

        internal double QueueWaitMilliseconds { get; }
        internal int PeakQueueDepth => _policy.PeakDepth;

        /// <summary>
        /// Permanently prevents new admissions for this endpoint-and-contract policy. Existing waiters
        /// fail without transport; the active lease remains held until its owner unwinds and disposes it.
        /// </summary>
        internal void Poison(Task<LocalModelBenchmarkTransportResponse>? lateResponseTask = null) =>
            Volatile.Read(ref _entry)?.Poison(lateResponseTask);

        public void Dispose()
        {
            AdmissionEntry? entry = Interlocked.Exchange(ref _entry, null);
            if (entry is null) return;
            entry.ReleaseActive();
            _releaseReference(entry);
        }
    }

    internal enum AdmissionWaitCancellation
    {
        Removed,
        Granted,
        Poisoned
    }

    internal sealed class AdmissionWait
    {
        internal AdmissionWait(bool isImmediate, TaskCompletionSource<bool>? completion)
        {
            IsImmediate = isImmediate;
            Completion = completion;
            Granted = isImmediate;
        }

        internal bool IsImmediate { get; }
        internal TaskCompletionSource<bool>? Completion { get; }
        internal LinkedListNode<AdmissionWait>? Node { get; set; }
        internal bool Granted { get; set; }
        internal bool Poisoned { get; set; }
    }

    internal sealed class AdmissionEntry
    {
        private readonly object _gate = new();
        private readonly LinkedList<AdmissionWait> _waiters = new();
        private readonly EndpointPolicy _policy;
        private bool _active;
        private bool _retired;
        private int _references;

        internal AdmissionEntry(string endpoint, EndpointPolicy policy)
        {
            Endpoint = endpoint;
            _policy = policy;
        }
        internal string Endpoint { get; }

        internal bool TryAddReference()
        {
            lock (_gate)
            {
                if (_retired) return false;
                _policy.ThrowIfPoisoned();
                _references++;
                return true;
            }
        }

        internal bool ReleaseReference()
        {
            lock (_gate)
            {
                _references--;
                if (_references < 0) throw new InvalidOperationException("Admission reference underflow.");
                if (_references == 0 && !_active && _waiters.Count == 0)
                {
                    _retired = true;
                    return true;
                }
                return false;
            }
        }

        internal AdmissionWait Enqueue()
        {
            lock (_gate)
            {
                _policy.ThrowIfPoisoned();
                if (!_active)
                {
                    _active = true;
                    _policy.ObserveDepth(1);
                    return new AdmissionWait(true, null);
                }

                int maximumWaiting = Math.Max(0, _policy.QueueDepth - 1);
                if (_waiters.Count >= maximumWaiting)
                {
                    throw new LocalModelBenchmarkException("queue_saturated");
                }

                TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                AdmissionWait wait = new(false, completion);
                wait.Node = _waiters.AddLast(wait);
                _policy.ObserveDepth(1 + _waiters.Count);
                return wait;
            }
        }

        internal AdmissionWaitCancellation CancelWait(AdmissionWait wait)
        {
            lock (_gate)
            {
                if (wait.Node?.List is not null)
                {
                    _waiters.Remove(wait.Node);
                    return AdmissionWaitCancellation.Removed;
                }
                if (wait.Poisoned) return AdmissionWaitCancellation.Poisoned;
                return AdmissionWaitCancellation.Granted;
            }
        }

        internal void ReleaseActive()
        {
            AdmissionWait? next = null;
            lock (_gate)
            {
                if (!_active) throw new InvalidOperationException("Admission active lease underflow.");
                if (_policy.IsPoisoned)
                {
                    _active = false;
                    return;
                }
                if (_waiters.First is { } node)
                {
                    next = node.Value;
                    _waiters.RemoveFirst();
                    next.Granted = true;
                }
                else
                {
                    _active = false;
                }
            }
            next?.Completion!.TrySetResult(true);
        }

        internal void Poison(Task<LocalModelBenchmarkTransportResponse>? lateResponseTask = null)
        {
            EndpointPolicy.LateResponseObserverRegistration? lateResponseObserver =
                _policy.Poison(lateResponseTask);
            List<AdmissionWait> poisoned;
            lock (_gate)
            {
                poisoned = _waiters.ToList();
                _waiters.Clear();
                foreach (AdmissionWait wait in poisoned) wait.Poisoned = true;
            }
            foreach (AdmissionWait wait in poisoned) wait.Completion!.TrySetResult(false);
            lateResponseObserver?.Start();
        }
    }
}

/// <summary>Exact immutable runtime artifact and process authorization asserted by the human-owned launch boundary.</summary>
public sealed record OllamaRuntimeAuthorization
{
    public required string RuntimeModelReference { get; init; }
    public required string ArtifactDigestSha256 { get; init; }
    public long ArtifactSizeBytes { get; init; }
    public required string ArtifactFormat { get; init; }
    public required string ModelFamily { get; init; }
    public required string ParameterSize { get; init; }
    public required string QuantizationLevel { get; init; }
    public required string OllamaProcessIdentity { get; init; }
    public int OllamaProcessId { get; init; }
}

/// <summary>
/// One-shot execution capability. Issuance is the explicit positive-authorization boundary; a normal
/// preflight plan remains ExecutionAuthorized=false and is insufficient to invoke transport.
/// </summary>
public sealed class LocalModelBenchmarkExecutionCapability
{
    private int _consumed;

    private LocalModelBenchmarkExecutionCapability(
        string planDigestSha256,
        string endpoint,
        string contractId,
        string normalizedModelIdentity,
        OllamaRuntimeAuthorization runtime)
    {
        PlanDigestSha256 = planDigestSha256;
        Endpoint = endpoint;
        ContractId = contractId;
        NormalizedModelIdentity = normalizedModelIdentity;
        Runtime = runtime;
    }

    public string PlanDigestSha256 { get; }
    public string Endpoint { get; }
    public string ContractId { get; }
    public string NormalizedModelIdentity { get; }
    public OllamaRuntimeAuthorization Runtime { get; }
    public bool IsConsumed => Volatile.Read(ref _consumed) != 0;

    public static LocalModelBenchmarkExecutionCapability AuthorizeSingleUse(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime)
    {
        OllamaBenchmarkContract.ValidateExactPlan(plan);
        OllamaBenchmarkContract.ValidateRuntimeAuthorization(runtime);
        if (!string.Equals(runtime.QuantizationLevel.ToLowerInvariant(), plan.QuantizationIdentity, StringComparison.Ordinal))
        {
            throw new LocalModelBenchmarkException("runtime_quantization_not_bound_to_plan");
        }
        return new LocalModelBenchmarkExecutionCapability(
            OllamaBenchmarkContract.ComputePlanDigest(plan),
            plan.Endpoint,
            plan.ContractId,
            plan.ModelIdentity,
            runtime);
    }

    internal void Consume(LocalModelBenchmarkPlan plan)
    {
        ValidateForAdmission(plan);
        if (Interlocked.CompareExchange(ref _consumed, 1, 0) != 0)
        {
            throw new LocalModelBenchmarkException("execution_capability_reused");
        }
    }

    internal void ValidateForAdmission(LocalModelBenchmarkPlan plan)
    {
        if (!string.Equals(PlanDigestSha256, OllamaBenchmarkContract.ComputePlanDigest(plan), StringComparison.Ordinal)
            || !string.Equals(Endpoint, plan.Endpoint, StringComparison.Ordinal)
            || !string.Equals(ContractId, plan.ContractId, StringComparison.Ordinal)
            || !string.Equals(NormalizedModelIdentity, plan.ModelIdentity, StringComparison.Ordinal))
        {
            throw new LocalModelBenchmarkException("execution_capability_mismatch");
        }

        if (Volatile.Read(ref _consumed) != 0)
        {
            throw new LocalModelBenchmarkException("execution_capability_reused");
        }
    }
}

public sealed record LocalModelVramReading(
    string ContractId,
    string NormalizedModelIdentity,
    string ArtifactDigestSha256,
    string OllamaProcessIdentity,
    int OllamaProcessId,
    double UsedMiB);

/// <summary>The runner never discovers or invokes a GPU tool itself; an explicit bounded probe supplies readings.</summary>
public interface ILocalModelBenchmarkVramProbe
{
    ValueTask<LocalModelVramReading> ReadAsync(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime,
        CancellationToken cancellationToken);
}

public interface ILocalModelBenchmarkClock
{
    long GetTimestamp();
    double GetElapsedMilliseconds(long startTimestamp, long endTimestamp);
}

public sealed class StopwatchLocalModelBenchmarkClock : ILocalModelBenchmarkClock
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public double GetElapsedMilliseconds(long startTimestamp, long endTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;
}

public sealed record LocalModelBenchmarkTransportRequest(HttpMethod Method, Uri RequestUri, byte[] BodyUtf8);

public sealed class LocalModelBenchmarkTransportResponse : IAsyncDisposable
{
    private readonly IDisposable? _owner;

    public LocalModelBenchmarkTransportResponse(
        HttpStatusCode statusCode,
        Uri effectiveRequestUri,
        IPAddress remoteAddress,
        string? mediaType,
        long? contentLength,
        Stream body,
        Uri? redirectLocation = null,
        IDisposable? owner = null)
    {
        StatusCode = statusCode;
        EffectiveRequestUri = effectiveRequestUri ?? throw new ArgumentNullException(nameof(effectiveRequestUri));
        RemoteAddress = remoteAddress ?? throw new ArgumentNullException(nameof(remoteAddress));
        MediaType = mediaType;
        ContentLength = contentLength;
        Body = body ?? throw new ArgumentNullException(nameof(body));
        RedirectLocation = redirectLocation;
        _owner = owner;
    }

    public HttpStatusCode StatusCode { get; }
    public Uri EffectiveRequestUri { get; }
    public IPAddress RemoteAddress { get; }
    public string? MediaType { get; }
    public long? ContentLength { get; }
    public Stream Body { get; }
    public Uri? RedirectLocation { get; }

    public ValueTask DisposeAsync()
    {
        try
        {
            Body.Dispose();
        }
        finally
        {
            _owner?.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A transport owns every operation until it transfers a response through successful task completion.
/// After cancellation, an implementation must dispose any response it later acquires and complete canceled
/// or faulted; it must not transfer late response ownership to an abandoned caller task.
/// </summary>
public interface ILocalModelBenchmarkTransport
{
    ValueTask<LocalModelBenchmarkTransportResponse> SendAsync(
        LocalModelBenchmarkTransportRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Production transport for one IP-literal loopback Ollama endpoint. Redirects, proxy inheritance,
/// credentials, cookies, decompression and HTTP-version fallback are disabled; the connected peer is verified.
/// </summary>
public sealed class OllamaLoopbackHttpTransport : ILocalModelBenchmarkTransport, IAsyncDisposable
{
    private readonly Uri _tagsUri;
    private readonly Uri _generateUri;
    private readonly IPAddress _expectedAddress;
    private readonly int _expectedPort;
    private readonly SocketsHttpHandler _handler;
    private readonly HttpClient _client;
    private IPAddress? _connectedAddress;

    public OllamaLoopbackHttpTransport(string canonicalEndpoint)
    {
        if (!Uri.TryCreate(canonicalEndpoint, UriKind.Absolute, out Uri? endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttp
            || endpoint.AbsolutePath != "/"
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || !IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out IPAddress? address)
            || !IPAddress.IsLoopback(address)
            || endpoint.Port is < LocalModelAdapterPreflight.MinimumPort or > LocalModelAdapterPreflight.MaximumPort)
        {
            throw new ArgumentException("Endpoint must be a canonical IP-literal loopback HTTP origin.", nameof(canonicalEndpoint));
        }

        string expectedCanonical = address.AddressFamily == AddressFamily.InterNetwork
            ? $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}/"
            : $"http://[::1]:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}/";
        if (!string.Equals(canonicalEndpoint, expectedCanonical, StringComparison.Ordinal))
        {
            throw new ArgumentException("Endpoint must be canonical.", nameof(canonicalEndpoint));
        }

        _expectedAddress = address;
        _expectedPort = endpoint.Port;
        _tagsUri = new Uri(endpoint, "api/tags");
        _generateUri = new Uri(endpoint, "api/generate");
        _handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            Credentials = null,
            DefaultProxyCredentials = null,
            PreAuthenticate = false,
            Proxy = null,
            UseCookies = false,
            UseProxy = false,
            ConnectCallback = ConnectLoopbackAsync
        };
        _client = new HttpClient(_handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async ValueTask<LocalModelBenchmarkTransportResponse> SendAsync(
        LocalModelBenchmarkTransportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool isTags = request.Method == HttpMethod.Get
            && Uri.Equals(request.RequestUri, _tagsUri)
            && request.BodyUtf8.Length == 0;
        bool isGenerate = request.Method == HttpMethod.Post
            && Uri.Equals(request.RequestUri, _generateUri)
            && request.BodyUtf8.Length > 0;
        if (!isTags && !isGenerate)
        {
            throw new LocalModelBenchmarkException("transport_request_not_exact");
        }

        using HttpRequestMessage message = new(request.Method, request.RequestUri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        if (isGenerate)
        {
            message.Content = new ByteArrayContent(request.BodyUtf8);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response = await _client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IPAddress? connectedAddress = Volatile.Read(ref _connectedAddress);
            if (connectedAddress is null || !connectedAddress.Equals(_expectedAddress) || !IPAddress.IsLoopback(connectedAddress))
            {
                throw new LocalModelBenchmarkException("actual_connection_not_exact_loopback");
            }

            Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new LocalModelBenchmarkTransportResponse(
                response.StatusCode,
                response.RequestMessage?.RequestUri ?? request.RequestUri,
                connectedAddress,
                response.Content.Headers.ContentType?.MediaType,
                response.Content.Headers.ContentLength,
                body,
                response.Headers.Location,
                response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _handler.Dispose();
        return ValueTask.CompletedTask;
    }

    private async ValueTask<Stream> ConnectLoopbackAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (context.DnsEndPoint.Port != _expectedPort
            || !string.Equals(context.DnsEndPoint.Host.Trim('[', ']'), _expectedAddress.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalModelBenchmarkException("connection_target_not_exact");
        }

        Socket socket = new(_expectedAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(_expectedAddress, _expectedPort), cancellationToken).ConfigureAwait(false);
            if (socket.RemoteEndPoint is not IPEndPoint remote
                || remote.Port != _expectedPort
                || !remote.Address.Equals(_expectedAddress)
                || !IPAddress.IsLoopback(remote.Address))
            {
                throw new LocalModelBenchmarkException("actual_connection_not_exact_loopback");
            }

            Volatile.Write(ref _connectedAddress, remote.Address);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

/// <summary>
/// Executes a one-shot authorized plan against one verified local Ollama artifact. Provider output remains
/// untrusted and is validated only against a disposable scratch world; no simulation state reference is accepted.
/// </summary>
public sealed class OllamaBenchmarkRunner
{
    public const string SharedServerIdentity = "snow-globe-ollama-loopback-v1";
    public const string AdapterIdentity = "ollama-generate-v1";
    public const string PromptIdentity = "snow-globe-proposal-v1";
    public const int OutputTokenLimit = 96;
    public const int MaximumResponseJsonDepth = 4;
    public const int MaximumProposalJsonDepth = 2;
    public const int MaximumTagsJsonDepth = 5;
    public const int VramReadTimeoutMilliseconds = 250;
    public const int VramSamplingIntervalMilliseconds = 25;
    public const int AbandonedResponseDrainMilliseconds = 250;
    public const double RequiredVramHeadroomFraction = 0.15;
    public const string ExternalStartupConfigurationClaim = "unverified_external_startup_configuration_no_cache_batching_or_speculation_claim";

    private const int MaximumVramSamples = 4801;
    private const string FixedPrompt = "Return only one JSON object matching the supplied schema. This is a fixed Snow Globe benchmark observation, not user input: agent_id=agent-00; tick=0; available_wood=64; available_stone=32; stockpile_wood=0; stockpile_stone=0; shelter_count=0; storage_count=0. Select one legal action and quantity. Do not include prose.";
    private readonly ILocalModelBenchmarkTransport _transport;
    private readonly ILocalModelBenchmarkVramProbe _vramProbe;
    private readonly ILocalModelBenchmarkClock _clock;
    private readonly Action? _beforeLateResponseObserverInstallationForTesting;

    private static readonly JsonSerializerOptions ResponseJsonOptions = StrictJsonOptions(MaximumResponseJsonDepth);
    private static readonly JsonSerializerOptions ProposalJsonOptions = StrictJsonOptions(MaximumProposalJsonDepth);
    private static readonly JsonSerializerOptions TagsJsonOptions = StrictJsonOptions(MaximumTagsJsonDepth);

    public OllamaBenchmarkRunner(
        ILocalModelBenchmarkTransport transport,
        ILocalModelBenchmarkVramProbe vramProbe,
        ILocalModelBenchmarkClock? clock = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _vramProbe = vramProbe ?? throw new ArgumentNullException(nameof(vramProbe));
        _clock = clock ?? new StopwatchLocalModelBenchmarkClock();
    }

    internal OllamaBenchmarkRunner(
        ILocalModelBenchmarkTransport transport,
        ILocalModelBenchmarkVramProbe vramProbe,
        ILocalModelBenchmarkClock clock,
        Action beforeLateResponseObserverInstallationForTesting)
        : this(transport, vramProbe, clock)
    {
        _beforeLateResponseObserverInstallationForTesting =
            beforeLateResponseObserverInstallationForTesting
            ?? throw new ArgumentNullException(nameof(beforeLateResponseObserverInstallationForTesting));
    }

    public async Task<OllamaBenchmarkRunResult> RunAsync(
        LocalModelBenchmarkPlan plan,
        LocalModelBenchmarkExecutionCapability executionCapability,
        CancellationToken cancellationToken = default)
    {
        OllamaBenchmarkContract.ValidateExactPlan(plan);
        ArgumentNullException.ThrowIfNull(executionCapability);
        executionCapability.ValidateForAdmission(plan);
        using OllamaEndpointAdmissionRegistry.AdmissionLease admission = await OllamaEndpointAdmissionRegistry.AcquireAsync(
            plan.Endpoint,
            plan.ContractId,
            plan.Budgets.QueueDepth,
            plan.Budgets.TotalQueueWaitMilliseconds,
            _clock,
            cancellationToken).ConfigureAwait(false);

        executionCapability.Consume(plan);
        OllamaRuntimeAuthorization runtime = executionCapability.Runtime;
        if (!string.Equals(runtime.QuantizationLevel.ToLowerInvariant(), plan.QuantizationIdentity, StringComparison.Ordinal))
        {
            throw new LocalModelBenchmarkException("runtime_quantization_not_bound_to_plan");
        }

        Uri endpoint = new(plan.Endpoint, UriKind.Absolute);
        Uri tagsUri = new(endpoint, "api/tags");
        Uri generateUri = new(endpoint, "api/generate");
        OllamaVerifiedModelProvenance provenance = await VerifyTagsAsync(
            plan,
            runtime,
            tagsUri,
            admission,
            cancellationToken).ConfigureAwait(false);

        byte[] requestUtf8 = CreateRequestUtf8(plan, runtime.RuntimeModelReference);
        try
        {
            if (requestUtf8.Length <= 0 || requestUtf8.Length > plan.Budgets.RequestBytes)
            {
                throw new LocalModelBenchmarkException("request_byte_budget_exceeded");
            }

            double observedPeak = 0;
            int vramSampleCount = 0;
            for (int index = 0; index < plan.WarmupRequestCount; index++)
            {
                CompletedRequest warmup = await ExecuteOneAsync(
                    plan, runtime, generateUri, requestUtf8, admission, cancellationToken).ConfigureAwait(false);
                observedPeak = Math.Max(observedPeak, warmup.ObservedSampledPeakVramMiB);
                vramSampleCount = checked(vramSampleCount + warmup.VramSampleCount);
            }

            LocalModelVramReading staticReading = await ReadVramBoundedAsync(plan, runtime, cancellationToken).ConfigureAwait(false);
            observedPeak = Math.Max(observedPeak, staticReading.UsedMiB);
            vramSampleCount = checked(vramSampleCount + 1);
            List<LocalModelMetricsSample> samples = new(plan.MeasuredRequestCount);
            long totalEvalCount = 0;
            long totalEvalDurationNanoseconds = 0;
            for (int index = 0; index < plan.MeasuredRequestCount; index++)
            {
                CompletedRequest completed = await ExecuteOneAsync(
                    plan, runtime, generateUri, requestUtf8, admission, cancellationToken).ConfigureAwait(false);
                observedPeak = Math.Max(observedPeak, completed.ObservedSampledPeakVramMiB);
                vramSampleCount = checked(vramSampleCount + completed.VramSampleCount);
                samples.Add(new LocalModelMetricsSample
                {
                    Sequence = index,
                    RequestLatencyMilliseconds = completed.LatencyMilliseconds,
                    QueueWaitMilliseconds = index == 0 ? admission.QueueWaitMilliseconds : 0,
                    RequestBytes = requestUtf8.Length,
                    OutputBytes = completed.OutputBytes,
                    Outcome = LocalModelMetricsSampleOutcome.Success
                });
                try
                {
                    totalEvalCount = checked(totalEvalCount + completed.EvalCount);
                    totalEvalDurationNanoseconds = checked(totalEvalDurationNanoseconds + completed.EvalDurationNanoseconds);
                }
                catch (OverflowException exception)
                {
                    throw new LocalModelBenchmarkException("ollama_timing_counter_overflow", exception);
                }
            }

            double throughput = (double)totalEvalCount * 1_000_000_000d / totalEvalDurationNanoseconds;
            if (!double.IsFinite(throughput) || throughput <= 0)
            {
                throw new LocalModelBenchmarkException("throughput_invalid");
            }

            byte[] canonicalSamples = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(samples);
            double[] sortedLatency = samples.Select(sample => sample.RequestLatencyMilliseconds).OrderBy(value => value).ToArray();
            LocalModelBenchmarkEvidence evidence = new()
            {
                ContractId = plan.ContractId,
                ModelIdentity = plan.ModelIdentity,
                QuantizationIdentity = plan.QuantizationIdentity,
                ContextWindowTokens = plan.ContextWindowTokens,
                StaticVramMiB = staticReading.UsedMiB,
                PeakVramMiB = observedPeak,
                WarmupRequestCount = plan.WarmupRequestCount,
                MeasuredRequestCount = plan.MeasuredRequestCount,
                MetricsSampleCount = samples.Count,
                CanonicalMetricsSampleDigestSha256 = Sha256(canonicalSamples),
                CanonicalMetricsSamplesUtf8 = canonicalSamples,
                P50LatencyMilliseconds = Percentile(sortedLatency, 0.50),
                P95LatencyMilliseconds = Percentile(sortedLatency, 0.95),
                P99LatencyMilliseconds = Percentile(sortedLatency, 0.99),
                MaximumRequestLatencyMilliseconds = sortedLatency[^1],
                ThroughputTokensPerSecond = throughput,
                FailureCount = 0,
                FallbackCount = 0,
                QueueBound = plan.Budgets.QueueDepth,
                PeakQueueDepth = admission.PeakQueueDepth,
                PeakRequestBytes = requestUtf8.Length,
                PeakOutputBytes = samples.Max(sample => sample.OutputBytes),
                PeakQueueWaitMilliseconds = samples.Max(sample => sample.QueueWaitMilliseconds),
                TotalQueueWaitMilliseconds = samples.Sum(sample => sample.QueueWaitMilliseconds)
            };

            LocalModelBenchmarkValidationResult validation = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence);
            if (!validation.IsAccepted)
            {
                CryptographicOperations.ZeroMemory(canonicalSamples);
                throw new LocalModelBenchmarkException("benchmark_evidence_rejected");
            }

            return new OllamaBenchmarkRunResult(
                evidence,
                provenance,
                observedPeak,
                MaximumAllowedObservedVram(plan),
                OutputTokenLimit,
                vramSampleCount,
                ClientSideSerializationEnforced: true,
                ExternalServerStartupConfigurationVerified: false,
                ExternalStartupConfigurationClaim);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(requestUtf8);
        }
    }

    private async Task<OllamaVerifiedModelProvenance> VerifyTagsAsync(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime,
        Uri tagsUri,
        OllamaEndpointAdmissionRegistry.AdmissionLease admission,
        CancellationToken callerCancellationToken)
    {
        byte[] tagsUtf8 = await SendAndReadBoundedAsync(
            plan,
            new LocalModelBenchmarkTransportRequest(HttpMethod.Get, tagsUri, Array.Empty<byte>()),
            tagsUri,
            admission,
            callerCancellationToken).ConfigureAwait(false);
        try
        {
            InspectStructure(tagsUtf8, MaximumTagsJsonDepth, "tags_json");
            OllamaTagsResponse? tags;
            try
            {
                tags = JsonSerializer.Deserialize<OllamaTagsResponse>(tagsUtf8, TagsJsonOptions);
            }
            catch (JsonException exception)
            {
                throw new LocalModelBenchmarkException("tags_json_invalid", exception);
            }

            if (tags?.Models is null || tags.Models.Count == 0)
            {
                throw new LocalModelBenchmarkException("tags_models_missing");
            }

            List<OllamaTagModel> exact = new();
            foreach (OllamaTagModel? model in tags.Models)
            {
                if (model is not null
                    && (string.Equals(model.Name, runtime.RuntimeModelReference, StringComparison.Ordinal)
                        || string.Equals(model.Model, runtime.RuntimeModelReference, StringComparison.Ordinal))
                    && (!string.Equals(model.Name, runtime.RuntimeModelReference, StringComparison.Ordinal)
                        || !string.Equals(model.Model, runtime.RuntimeModelReference, StringComparison.Ordinal)))
                {
                    throw new LocalModelBenchmarkException("runtime_model_alias_rejected");
                }
                ValidateTagShape(model);
                if (string.Equals(model!.Digest, runtime.ArtifactDigestSha256, StringComparison.Ordinal)
                    && !string.Equals(model.Name, runtime.RuntimeModelReference, StringComparison.Ordinal))
                {
                    throw new LocalModelBenchmarkException("runtime_digest_alias_rejected");
                }
                if (string.Equals(model.Name, runtime.RuntimeModelReference, StringComparison.Ordinal))
                {
                    exact.Add(model);
                }
            }

            if (exact.Count != 1)
            {
                throw new LocalModelBenchmarkException(exact.Count == 0
                    ? "runtime_model_missing"
                    : "runtime_model_duplicate");
            }

            OllamaTagModel selected = exact[0];
            OllamaTagDetails details = selected.Details!;
            if (!string.Equals(selected.Digest, runtime.ArtifactDigestSha256, StringComparison.Ordinal)
                || selected.Size != runtime.ArtifactSizeBytes
                || !string.Equals(details.Format, runtime.ArtifactFormat, StringComparison.Ordinal)
                || !string.Equals(details.Family, runtime.ModelFamily, StringComparison.Ordinal)
                || !string.Equals(details.ParameterSize, runtime.ParameterSize, StringComparison.Ordinal)
                || !string.Equals(details.QuantizationLevel, runtime.QuantizationLevel, StringComparison.Ordinal)
                || details.Families is null
                || details.Families.Count == 0
                || details.Families.Distinct(StringComparer.Ordinal).Count() != details.Families.Count
                || !details.Families.Contains(runtime.ModelFamily, StringComparer.Ordinal))
            {
                throw new LocalModelBenchmarkException("runtime_provenance_mismatch");
            }

            return new OllamaVerifiedModelProvenance(
                runtime.RuntimeModelReference,
                runtime.ArtifactDigestSha256,
                runtime.ArtifactSizeBytes,
                runtime.ArtifactFormat,
                runtime.ModelFamily,
                runtime.ParameterSize,
                runtime.QuantizationLevel,
                runtime.OllamaProcessIdentity,
                runtime.OllamaProcessId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tagsUtf8);
        }
    }

    private static void ValidateTagShape(OllamaTagModel? model)
    {
        if (model is null
            || !OllamaBenchmarkContract.IsRuntimeReference(model.Name)
            || OllamaBenchmarkContract.IsCloudRuntimeReference(model.Name)
            || !string.Equals(model.Name, model.Model, StringComparison.Ordinal)
            || string.IsNullOrEmpty(model.ModifiedAt)
            || model.ModifiedAt.Length > 64
            || !DateTimeOffset.TryParse(model.ModifiedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
            || model.Size <= 0
            || !OllamaBenchmarkContract.IsDigest(model.Digest)
            || model.Details is null
            || !string.Equals(model.Details.Format, "gguf", StringComparison.Ordinal)
            || !OllamaBenchmarkContract.IsSubstantiveMetadata(model.Details.Family, allowUppercase: false)
            || !OllamaBenchmarkContract.IsSubstantiveMetadata(model.Details.ParameterSize, allowUppercase: true)
            || !OllamaBenchmarkContract.IsSubstantiveMetadata(model.Details.QuantizationLevel, allowUppercase: true)
            || model.Details.Families is null
            || model.Details.Families.Count == 0
            || model.Details.Families.Any(family =>
                !OllamaBenchmarkContract.IsSubstantiveMetadata(family, allowUppercase: false)))
        {
            throw new LocalModelBenchmarkException("tags_model_invalid");
        }
    }

    private async Task<CompletedRequest> ExecuteOneAsync(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime,
        Uri generateUri,
        byte[] requestUtf8,
        OllamaEndpointAdmissionRegistry.AdmissionLease admission,
        CancellationToken callerCancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(plan.Budgets.RequestTimeoutMilliseconds));
        long started = _clock.GetTimestamp();
        Task<LocalModelBenchmarkTransportResponse> responseTask = _transport.SendAsync(
            new LocalModelBenchmarkTransportRequest(HttpMethod.Post, generateUri, requestUtf8),
            timeout.Token).AsTask();
        bool responseAcquired = false;
        double observedPeak = 0;
        int vramSamples = 0;
        try
        {
            while (!responseTask.IsCompleted)
            {
                LocalModelVramReading reading = await ReadVramBoundedAsync(plan, runtime, timeout.Token).ConfigureAwait(false);
                observedPeak = Math.Max(observedPeak, reading.UsedMiB);
                vramSamples++;
                if (responseTask.IsCompleted) break;
                if (vramSamples >= MaximumVramSamples)
                {
                    throw new LocalModelBenchmarkException("vram_sampling_limit_exceeded");
                }

                Task delay = Task.Delay(VramSamplingIntervalMilliseconds, timeout.Token);
                await Task.WhenAny(responseTask, delay).ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
            }

            if (vramSamples == 0)
            {
                throw new LocalModelBenchmarkException("inflight_vram_sample_unavailable");
            }

            await using LocalModelBenchmarkTransportResponse response = await responseTask.ConfigureAwait(false);
            responseAcquired = true;
            ValidateTransportResponse(plan, generateUri, response);
            byte[] responseUtf8 = await ReadBoundedAsync(
                response.Body,
                response.ContentLength,
                plan.Budgets.OutputBytes,
                admission,
                timeout.Token).ConfigureAwait(false);
            try
            {
                OllamaGenerateResponse parsed = ParseResponse(responseUtf8, plan, runtime);
                ValidateUntrustedProposal(parsed.Response!, plan);
                long ended = _clock.GetTimestamp();
                double elapsed = _clock.GetElapsedMilliseconds(started, ended);
                if (!double.IsFinite(elapsed) || elapsed <= 0 || elapsed > plan.Budgets.RequestTimeoutMilliseconds)
                {
                    throw new LocalModelBenchmarkException("request_latency_invalid");
                }

                return new CompletedRequest(
                    elapsed,
                    responseUtf8.Length,
                    parsed.EvalCount,
                    parsed.EvalDuration,
                    observedPeak,
                    vramSamples);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(responseUtf8);
            }
        }
        catch (OperationCanceledException exception) when (!callerCancellationToken.IsCancellationRequested)
        {
            throw new LocalModelBenchmarkException("request_timeout", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new LocalModelBenchmarkException("transport_failure", exception);
        }
        catch (IOException exception)
        {
            throw new LocalModelBenchmarkException("transport_failure", exception);
        }
        finally
        {
            if (!responseAcquired)
            {
                timeout.Cancel();
                bool drained = await DrainAndDisposeAbandonedResponseAsync(responseTask).ConfigureAwait(false);
                if (!drained)
                {
                    _beforeLateResponseObserverInstallationForTesting?.Invoke();
                    admission.Poison(responseTask);
                }
            }
        }
    }

    /// <summary>
    /// Disposes a response only if the transport honors cancellation and quiesces inside the fixed drain
    /// window. Otherwise the caller permanently poisons the endpoint and retains exactly one policy-owned
    /// observer that disposes any eventual response. No later admission can create another operation or observer.
    /// </summary>
    private static async Task<bool> DrainAndDisposeAbandonedResponseAsync(
        Task<LocalModelBenchmarkTransportResponse> responseTask)
    {
        Task drainDeadline = Task.Delay(AbandonedResponseDrainMilliseconds);
        await Task.WhenAny(responseTask, drainDeadline).ConfigureAwait(false);
        if (!responseTask.IsCompleted) return false;
        try
        {
            await using LocalModelBenchmarkTransportResponse abandoned =
                await responseTask.ConfigureAwait(false);
        }
        catch
        {
            // The original bounded validation, timeout, cancellation, or probe failure is authoritative.
        }
        return true;
    }

    private async Task<byte[]> SendAndReadBoundedAsync(
        LocalModelBenchmarkPlan plan,
        LocalModelBenchmarkTransportRequest request,
        Uri exactUri,
        OllamaEndpointAdmissionRegistry.AdmissionLease admission,
        CancellationToken callerCancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(plan.Budgets.RequestTimeoutMilliseconds));
        Task<LocalModelBenchmarkTransportResponse> responseTask = _transport.SendAsync(request, timeout.Token).AsTask();
        bool responseAcquired = false;
        try
        {
            await using LocalModelBenchmarkTransportResponse response =
                await responseTask.WaitAsync(timeout.Token).ConfigureAwait(false);
            responseAcquired = true;
            ValidateTransportResponse(plan, exactUri, response);
            return await ReadBoundedAsync(
                response.Body,
                response.ContentLength,
                plan.Budgets.OutputBytes,
                admission,
                timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!callerCancellationToken.IsCancellationRequested)
        {
            throw new LocalModelBenchmarkException("request_timeout", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new LocalModelBenchmarkException("transport_failure", exception);
        }
        catch (IOException exception)
        {
            throw new LocalModelBenchmarkException("transport_failure", exception);
        }
        finally
        {
            if (!responseAcquired)
            {
                timeout.Cancel();
                bool drained = await DrainAndDisposeAbandonedResponseAsync(responseTask).ConfigureAwait(false);
                if (!drained)
                {
                    _beforeLateResponseObserverInstallationForTesting?.Invoke();
                    admission.Poison(responseTask);
                }
            }
        }
    }

    private static void ValidateTransportResponse(
        LocalModelBenchmarkPlan plan,
        Uri exactUri,
        LocalModelBenchmarkTransportResponse response)
    {
        IPAddress expectedAddress = plan.Endpoint.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)
            ? IPAddress.Loopback
            : IPAddress.IPv6Loopback;
        if (!IPAddress.IsLoopback(response.RemoteAddress) || !response.RemoteAddress.Equals(expectedAddress))
        {
            throw new LocalModelBenchmarkException("actual_connection_not_exact_loopback");
        }
        if (!Uri.Equals(response.EffectiveRequestUri, exactUri))
        {
            throw new LocalModelBenchmarkException("effective_request_uri_mismatch");
        }

        int status = (int)response.StatusCode;
        if (status is >= 300 and <= 399 || response.RedirectLocation is not null)
        {
            throw new LocalModelBenchmarkException("redirect_rejected");
        }
        if (status is < 200 or > 299)
        {
            throw new LocalModelBenchmarkException("http_status_rejected");
        }
        if (!string.Equals(response.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalModelBenchmarkException("response_media_type_rejected");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        long? declaredLength,
        int maximumBytes,
        OllamaEndpointAdmissionRegistry.AdmissionLease admission,
        CancellationToken cancellationToken)
    {
        if (declaredLength is < 0 || declaredLength > maximumBytes)
        {
            throw new LocalModelBenchmarkException("response_byte_budget_exceeded");
        }

        ArrayBufferWriter<byte> writer = new(Math.Min(maximumBytes, 16 * 1024));
        byte[] buffer = new byte[Math.Min(8192, maximumBytes + 1)];
        bool bufferOwnedByAbandonedRead = false;
        try
        {
            while (true)
            {
                int remaining = maximumBytes - writer.WrittenCount;
                int requested = Math.Min(buffer.Length, remaining + 1);
                Task<int> readTask = source.ReadAsync(
                    buffer.AsMemory(0, requested),
                    cancellationToken).AsTask();
                int read;
                try
                {
                    read = await readTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    source.Dispose();
                    bool drained = await DrainAbandonedOperationAsync(readTask).ConfigureAwait(false);
                    if (!drained)
                    {
                        // The read operation itself keeps this dedicated bounded array alive. It is never
                        // pooled, returned, cleared, or exposed to another operation after endpoint poison.
                        bufferOwnedByAbandonedRead = true;
                        admission.Poison();
                    }
                    throw;
                }
                if (read == 0) break;
                if (read > remaining) throw new LocalModelBenchmarkException("response_byte_budget_exceeded");
                writer.Write(buffer.AsSpan(0, read));
            }

            if (writer.WrittenCount == 0 || declaredLength.HasValue && writer.WrittenCount != declaredLength.Value)
            {
                throw new LocalModelBenchmarkException("response_length_invalid");
            }
            return writer.WrittenSpan.ToArray();
        }
        finally
        {
            if (!bufferOwnedByAbandonedRead)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }
        }
    }

    private static async Task<bool> DrainAbandonedOperationAsync(Task operation)
    {
        Task drainDeadline = Task.Delay(AbandonedResponseDrainMilliseconds);
        await Task.WhenAny(operation, drainDeadline).ConfigureAwait(false);
        if (!operation.IsCompleted) return false;
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
            // Completion, rather than success, proves that no abandoned operation remains in flight.
        }
        return true;
    }

    private static OllamaGenerateResponse ParseResponse(
        byte[] utf8,
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime)
    {
        InspectStructure(utf8, MaximumResponseJsonDepth, "response_json");
        OllamaGenerateResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<OllamaGenerateResponse>(utf8, ResponseJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new LocalModelBenchmarkException("response_json_invalid", exception);
        }

        long maximumDurationNanoseconds = checked((long)plan.Budgets.RequestTimeoutMilliseconds * 1_000_000L);
        long accountedDuration;
        try
        {
            accountedDuration = checked(response!.LoadDuration + response.PromptEvalDuration + response.EvalDuration);
        }
        catch (Exception exception) when (exception is OverflowException or NullReferenceException)
        {
            throw new LocalModelBenchmarkException("response_counter_invalid", exception);
        }

        if (!string.Equals(response.Model, runtime.RuntimeModelReference, StringComparison.Ordinal)
            || string.IsNullOrEmpty(response.CreatedAt)
            || response.CreatedAt.Length > 64
            || !DateTimeOffset.TryParse(response.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
            || !response.Done
            || !string.Equals(response.DoneReason, "stop", StringComparison.Ordinal)
            || response.Response is null
            || response.TotalDuration <= 0
            || response.TotalDuration > maximumDurationNanoseconds
            || response.LoadDuration < 0
            || response.PromptEvalCount <= 0
            || response.PromptEvalCount > plan.ContextWindowTokens
            || response.PromptEvalDuration <= 0
            || response.EvalCount <= 0
            || response.EvalCount > OutputTokenLimit
            || response.EvalDuration <= 0
            || response.LoadDuration > response.TotalDuration
            || response.PromptEvalDuration > response.TotalDuration
            || response.EvalDuration > response.TotalDuration
            || accountedDuration > response.TotalDuration
            || response.Context is { Length: > 0 } context
                && (context.Length > plan.ContextWindowTokens || context.Any(token => token < 0)))
        {
            throw new LocalModelBenchmarkException(
                response is not null && !string.Equals(response.Model, runtime.RuntimeModelReference, StringComparison.Ordinal)
                    ? "runtime_model_response_mismatch"
                    : "response_counter_or_content_invalid");
        }

        return response;
    }

    private static void ValidateUntrustedProposal(string rawProposal, LocalModelBenchmarkPlan plan)
    {
        int byteCount = Encoding.UTF8.GetByteCount(rawProposal);
        if (byteCount <= 0 || byteCount > plan.Budgets.OutputBytes)
        {
            throw new LocalModelBenchmarkException("proposal_byte_budget_exceeded");
        }

        byte[] proposalUtf8 = Encoding.UTF8.GetBytes(rawProposal);
        try
        {
            InspectStructure(proposalUtf8, MaximumProposalJsonDepth, "proposal_json");
            OllamaProposal? proposal;
            try
            {
                proposal = JsonSerializer.Deserialize<OllamaProposal>(proposalUtf8, ProposalJsonOptions);
            }
            catch (JsonException exception)
            {
                throw new LocalModelBenchmarkException("proposal_json_invalid", exception);
            }

            if (proposal is null
                || !string.Equals(proposal.AgentId, "agent-00", StringComparison.Ordinal)
                || !SnowGlobeRunStore.TryParseCanonicalAction(proposal.Action, out SnowGlobeActionKind action))
            {
                throw new LocalModelBenchmarkException("proposal_content_invalid");
            }

            SnowGlobeWorld scratch = SnowGlobeWorld.Create(seed: 4242, agentCount: 1);
            SnowGlobeCommitResult validation = scratch.ValidateAndCommit(
                new SnowGlobeActionProposal(proposal.AgentId!, action, proposal.Quantity));
            if (!validation.Accepted)
            {
                throw new LocalModelBenchmarkException("proposal_domain_rejected");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(proposalUtf8);
        }
    }

    private async ValueTask<LocalModelVramReading> ReadVramBoundedAsync(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readTimeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(
            VramReadTimeoutMilliseconds,
            plan.Budgets.RequestTimeoutMilliseconds)));
        LocalModelVramReading reading;
        try
        {
            reading = await _vramProbe.ReadAsync(plan, runtime, readTimeout.Token)
                .AsTask().WaitAsync(readTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LocalModelBenchmarkException("vram_read_timeout", exception);
        }

        if (!string.Equals(reading.ContractId, plan.ContractId, StringComparison.Ordinal)
            || !string.Equals(reading.NormalizedModelIdentity, plan.ModelIdentity, StringComparison.Ordinal)
            || !string.Equals(reading.ArtifactDigestSha256, runtime.ArtifactDigestSha256, StringComparison.Ordinal)
            || !string.Equals(reading.OllamaProcessIdentity, runtime.OllamaProcessIdentity, StringComparison.Ordinal)
            || reading.OllamaProcessId != runtime.OllamaProcessId
            || !double.IsFinite(reading.UsedMiB)
            || reading.UsedMiB <= 0)
        {
            throw new LocalModelBenchmarkException("vram_reading_identity_invalid");
        }

        if (reading.UsedMiB > MaximumAllowedObservedVram(plan))
        {
            throw new LocalModelBenchmarkException("vram_headroom_exceeded");
        }
        return reading;
    }

    private static double MaximumAllowedObservedVram(LocalModelBenchmarkPlan plan) =>
        plan.VramBudgetMiB * (1d - RequiredVramHeadroomFraction);

    private static void InspectStructure(ReadOnlySpan<byte> utf8, int maximumDepth, string prefix)
    {
        Stack<HashSet<string>> objects = new();
        try
        {
            Utf8JsonReader reader = new(utf8, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = maximumDepth + 1
            });
            while (reader.Read())
            {
                if ((reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    && reader.CurrentDepth >= maximumDepth)
                {
                    throw new LocalModelBenchmarkException(prefix + "_too_deep");
                }
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    objects.Push(new HashSet<string>(StringComparer.Ordinal));
                }
                else if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (objects.Count == 0 || !objects.Peek().Add(reader.GetString()!))
                    {
                        throw new LocalModelBenchmarkException(prefix + "_duplicate_property");
                    }
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    objects.Pop();
                }
            }

            if (objects.Count != 0) throw new LocalModelBenchmarkException(prefix + "_invalid");
        }
        catch (JsonException exception)
        {
            throw new LocalModelBenchmarkException(prefix + "_invalid", exception);
        }
    }

    private static byte[] CreateRequestUtf8(LocalModelBenchmarkPlan plan, string runtimeModelReference)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("model", runtimeModelReference);
        writer.WriteString("prompt", FixedPrompt);
        writer.WriteBoolean("stream", false);
        writer.WriteBoolean("think", false);
        writer.WriteBoolean("raw", false);
        writer.WritePropertyName("format");
        writer.WriteStartObject();
        writer.WriteString("type", "object");
        writer.WriteBoolean("additionalProperties", false);
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        WriteStringSchema(writer, "agent_id", new[] { "agent-00" });
        WriteStringSchema(writer, "action", Enum.GetNames<SnowGlobeActionKind>());
        writer.WritePropertyName("quantity");
        writer.WriteStartObject();
        writer.WriteString("type", "integer");
        writer.WriteNumber("minimum", 0);
        writer.WriteNumber("maximum", 64);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WritePropertyName("required");
        writer.WriteStartArray();
        writer.WriteStringValue("agent_id");
        writer.WriteStringValue("action");
        writer.WriteStringValue("quantity");
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WritePropertyName("options");
        writer.WriteStartObject();
        writer.WriteNumber("num_ctx", plan.ContextWindowTokens);
        writer.WriteNumber("num_predict", OutputTokenLimit);
        writer.WriteNumber("seed", 0);
        writer.WriteNumber("temperature", 0);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteStringSchema(Utf8JsonWriter writer, string property, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(property);
        writer.WriteStartObject();
        writer.WriteString("type", "string");
        writer.WritePropertyName("enum");
        writer.WriteStartArray();
        foreach (string value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static JsonSerializerOptions StrictJsonOptions(int maximumDepth) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = maximumDepth,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static double Percentile(IReadOnlyList<double> sorted, double fraction) =>
        sorted[(int)Math.Ceiling(sorted.Count * fraction) - 1];

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed record CompletedRequest(
        double LatencyMilliseconds,
        int OutputBytes,
        long EvalCount,
        long EvalDurationNanoseconds,
        double ObservedSampledPeakVramMiB,
        int VramSampleCount);

    private sealed record OllamaTagsResponse
    {
        [JsonRequired] public IReadOnlyList<OllamaTagModel?>? Models { get; init; }
    }

    private sealed record OllamaTagModel
    {
        [JsonRequired] public string? Name { get; init; }
        [JsonRequired] public string? Model { get; init; }
        [JsonRequired] public string? ModifiedAt { get; init; }
        [JsonRequired] public long Size { get; init; }
        [JsonRequired] public string? Digest { get; init; }
        [JsonRequired] public OllamaTagDetails? Details { get; init; }
    }

    private sealed record OllamaTagDetails
    {
        [JsonRequired] public string? Format { get; init; }
        [JsonRequired] public string? Family { get; init; }
        [JsonRequired] public IReadOnlyList<string>? Families { get; init; }
        [JsonRequired] public string? ParameterSize { get; init; }
        [JsonRequired] public string? QuantizationLevel { get; init; }
    }

    private sealed record OllamaGenerateResponse
    {
        [JsonRequired] public string? Model { get; init; }
        [JsonRequired] public string? CreatedAt { get; init; }
        [JsonRequired] public string? Response { get; init; }
        [JsonRequired] public bool Done { get; init; }
        [JsonRequired] public string? DoneReason { get; init; }
        public int[]? Context { get; init; }
        [JsonRequired] public long TotalDuration { get; init; }
        [JsonRequired] public long LoadDuration { get; init; }
        [JsonRequired] public long PromptEvalCount { get; init; }
        [JsonRequired] public long PromptEvalDuration { get; init; }
        [JsonRequired] public long EvalCount { get; init; }
        [JsonRequired] public long EvalDuration { get; init; }
    }

    private sealed record OllamaProposal
    {
        [JsonRequired] public string? AgentId { get; init; }
        [JsonRequired] public string? Action { get; init; }
        [JsonRequired] public int Quantity { get; init; }
    }
}
