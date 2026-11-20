using Remotely.Shared.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Remotely.Agent.Services;

/// <summary>
/// Downloads the content that the server publishes, using the http client of
/// the host.
/// </summary>
public class PackageContentFetcher : IPackageContentFetcher
{
    private static readonly TimeSpan _timeout = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _clientFactory;

    public PackageContentFetcher(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task FetchToFileAsync(string url, string filePath, CancellationToken cancellationToken)
    {
        using var client = _clientFactory.CreateClient();
        client.Timeout = _timeout;

        using var response = await client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            useAsync: true);

        await source.CopyToAsync(target, cancellationToken);
    }

    public async Task<byte[]> FetchBytesAsync(string url, CancellationToken cancellationToken)
    {
        using var client = _clientFactory.CreateClient();
        client.Timeout = _timeout;

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
