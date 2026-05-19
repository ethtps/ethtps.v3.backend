using Microsoft.Extensions.Logging;

namespace ETHTPS.ChainRegistry.Clients;

public class LogoFetcher(HttpClient httpClient, ILogger<LogoFetcher> logger)
{
    public async Task<(byte[] Bytes, string ContentType)?> FetchAsync(string? icon, int chainId, CancellationToken ct)
    {
        var candidates = BuildCandidates(icon, chainId);
        foreach (var url in candidates)
        {
            var result = await TryFetchAsync(url, ct);
            if (result.HasValue)
                return result;
        }
        return null;
    }

    private static IEnumerable<string> BuildCandidates(string? icon, int chainId)
    {
        if (!string.IsNullOrEmpty(icon))
            yield return $"https://icons.llamao.fi/icons/chains/rsz_{icon}.jpg";
        yield return $"https://chainid.network/chain-icons/eip155-{chainId}.png";
    }

    private async Task<(byte[] Bytes, string ContentType)?> TryFetchAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return bytes.Length > 0 ? (bytes, contentType) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("Logo fetch failed for {Url}: {Message}", url, ex.Message);
            return null;
        }
    }
}
