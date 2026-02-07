using System.Net;

namespace MusicShare.Services.Services;

public class MethodPreservingRedirectHandler : DelegatingHandler
{
    private static readonly HashSet<HttpStatusCode> RedirectStatusCodes =
    [
        HttpStatusCode.MovedPermanently,   // 301
        HttpStatusCode.Found,              // 302
        HttpStatusCode.TemporaryRedirect,  // 307
        HttpStatusCode.PermanentRedirect   // 308
    ];

    private volatile Uri? _cachedBaseAddress;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_cachedBaseAddress is { } cached)
        {
            var rewrittenUri = RewriteUri(request.RequestUri!, cached);
            request.RequestUri = rewrittenUri;

            var response = await base.SendAsync(request, cancellationToken);
            if (!IsRedirect(response))
                return response;
        }

        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync();

        var originalResponse = await base.SendAsync(request, cancellationToken);

        if (!IsRedirect(originalResponse) || originalResponse.Headers.Location is null)
            return originalResponse;

        var redirectUri = originalResponse.Headers.Location.IsAbsoluteUri
            ? originalResponse.Headers.Location
            : new Uri(request.RequestUri!, originalResponse.Headers.Location);

        _cachedBaseAddress = new Uri(redirectUri.GetLeftPart(UriPartial.Authority));

        var retryRequest = CloneRequest(request, redirectUri);
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static bool IsRedirect(HttpResponseMessage response)
        => RedirectStatusCodes.Contains(response.StatusCode);

    private static Uri RewriteUri(Uri original, Uri cachedBase)
        => new(cachedBase, original.PathAndQuery);

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original, Uri newUri)
    {
        var clone = new HttpRequestMessage(original.Method, newUri)
        {
            Content = original.Content
        };

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
