using Microsoft.Extensions.Options;
using MusicShare.MusicAdapters.Configuration.MusicServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicShare.MusicAdapters.Services.Extensions
{
    public class SpotifyAccessTokenHandler(IHttpClientFactory httpFactory, IOptionsMonitor<SpotifyConfiguration> spotifyConfigMonitor) : DelegatingHandler
    {
        private readonly IHttpClientFactory _httpFactory = httpFactory;
        private readonly IOptionsMonitor<SpotifyConfiguration> _spotifyConfigMonitor = spotifyConfigMonitor;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Get an access token and attach it
            var httpClient = _httpFactory.CreateClient(nameof(SpotifyAccessTokenHandler));

            httpClient
                .DefaultRequestHeaders
                .Add("Authorization", $"Basic {CredentialsToSpotifyFormat(_spotifyConfigMonitor.CurrentValue.ClientId, _spotifyConfigMonitor.CurrentValue.ClientSecret)}");

            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                ]);

            // Construct an x-www-form-urlencoded request to get the token
            var accessTokenRequest = await httpClient
                .PostAsync("https://accounts.spotify.com/api/token", content, cancellationToken);

            accessTokenRequest.EnsureSuccessStatusCode();

            var result = await JsonSerializer.DeserializeAsync<AccessTokenResult>(accessTokenRequest.Content.ReadAsStream(), cancellationToken: cancellationToken);

            request.Headers.Add("Authorization", $"Bearer {result!.AccessToken}");

            return await base.SendAsync(request, cancellationToken);
        }

        private static string CredentialsToSpotifyFormat(string clientId, string clientSecret) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        protected class AccessTokenResult
        {
            [JsonPropertyName("access_token")]
            public required string AccessToken { get; set; }
        }
    }
}
