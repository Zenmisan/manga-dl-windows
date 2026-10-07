using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace MangaDl.Core.Http;

public sealed class HttpService : IDisposable
{
    public const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private readonly HttpClient _client;
    private readonly HttpClientHandler _handler;

    public string BackendUrl { get; set; } = string.Empty;

    public HttpService(HttpClient? customClient = null)
    {
        if (customClient != null)
        {
            _client = customClient;
            _handler = new HttpClientHandler();
        }
        else
        {
            _handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true
            };

            _client = new HttpClient(_handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _client.DefaultRequestHeaders.Add("User-Agent", DefaultUserAgent);
            _client.DefaultRequestHeaders.Add("Accept", "*/*");
        }
    }

    /// <summary>
    /// Resolves proxy endpoints (e.g. /manga/proxy/html?url=...) directly to the target URL.
    /// </summary>
    public string ResolveUrl(string url)
    {
        if (url.StartsWith("/manga/proxy/html", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/manga/proxy/json", StringComparison.OrdinalIgnoreCase))
        {
            var idx = url.IndexOf("?url=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var queryPart = url.Substring(idx + 5);
                var ampIdx = queryPart.IndexOf('&');
                var rawUrl = ampIdx >= 0 ? queryPart.Substring(0, ampIdx) : queryPart;
                return HttpUtility.UrlDecode(rawUrl);
            }
        }

        if (url.StartsWith("/") && !string.IsNullOrEmpty(BackendUrl))
        {
            return BackendUrl.TrimEnd('/') + url;
        }

        return url;
    }

    public async Task<HttpResponseResult> FetchAsync(
        string url,
        string method = "GET",
        IDictionary<string, string>? headers = null,
        string? body = null,
        CancellationToken ct = default)
    {
        var resolvedUrl = ResolveUrl(url);
        var isProxyHtml = url.StartsWith("/manga/proxy/html", StringComparison.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), resolvedUrl);

        if (headers != null)
        {
            foreach (var (k, v) in headers)
            {
                if (k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) continue;
                if (k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                request.Headers.TryAddWithoutValidation(k, v);
            }
        }

        if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) && body != null)
        {
            var contentType = headers != null && headers.TryGetValue("Content-Type", out var ctHeader)
                ? ctHeader
                : "application/json";
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        return new HttpResponseResult
        {
            StatusCode = (int)response.StatusCode,
            IsSuccess = response.IsSuccessStatusCode,
            Url = resolvedUrl,
            Text = text,
            IsProxyHtml = isProxyHtml
        };
    }

    public async Task<byte[]> FetchBytesAsync(
        string url,
        string? referer = null,
        CancellationToken ct = default)
    {
        var resolvedUrl = ResolveUrl(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, resolvedUrl);
        if (!string.IsNullOrEmpty(referer))
        {
            request.Headers.TryAddWithoutValidation("Referer", referer);
        }

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}

public sealed class HttpResponseResult
{
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsProxyHtml { get; set; }
}
