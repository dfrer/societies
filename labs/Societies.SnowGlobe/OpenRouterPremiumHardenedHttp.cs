using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;

namespace Societies.SnowGlobe;

internal static class OpenRouterPremiumHardenedHttp
{
    internal const string PolicyDescriptor =
        "openrouter-hardened-http-policy/v1|no-redirect|no-retry-loop|no-proxy|no-cookies|no-ambient-auth|no-preauth|no-decompression|headers-8k|connect-5s|max-connection-1|pool-life-60s|pool-idle-5s|response-drain-0|no-activity-propagation|bounded-body";
    internal static readonly string PolicyDigestSha256 = OpenRouterPremiumCanonical.Digest(PolicyDescriptor);

    internal static SocketsHttpHandler CreateSocketsHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        Proxy = null,
        UseCookies = false,
        Credentials = null,
        PreAuthenticate = false,
        AutomaticDecompression = DecompressionMethods.None,
        MaxResponseHeadersLength = 8,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        MaxConnectionsPerServer = 1,
        PooledConnectionLifetime = TimeSpan.FromSeconds(60),
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(5),
        MaxResponseDrainSize = 0,
        ResponseDrainTimeout = TimeSpan.Zero,
        ActivityHeadersPropagator = null
    };

    internal static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        Func<Exception> oversized,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(oversized);
        if (maximumBytes is < 1 or > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        await using Stream input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Min(maximumBytes + 1, 8192));
        using MemoryStream output = new(Math.Min(maximumBytes, 16 * 1024));
        try
        {
            while (true)
            {
                int read = await input.ReadAsync(
                    rented.AsMemory(0, Math.Min(rented.Length, maximumBytes + 1)), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                if (output.Length + read > maximumBytes) throw oversized();
                output.Write(rented, 0, read);
            }
            return output.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rented);
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
