using System.Net.Http.Json;
using System.Web;

namespace MusicShare.Services.Services.Music.Spotify;

/// <summary>
/// Spotify API service implementation.
/// Handles HTTP communication with Spotify Web API and returns raw Spotify DTOs.
/// </summary>
public class SpotifyMusicService(HttpClient spotifyClient) : ISpotifyMusicService
{
    private readonly HttpClient _httpClient = spotifyClient;

    public async Task<SpotifySearchResponse?> SearchAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = HttpUtility.UrlEncode(query);
        var searchResult = await _httpClient.GetFromJsonAsync<SpotifySearchResponse>(
            $"search?q={encodedQuery}&type=track&limit={limit}",
            cancellationToken);

        return searchResult;
    }

    public async Task<SpotifyResponse?> GetTrackAsync(
        string trackId,
        CancellationToken cancellationToken = default)
    {
        var apiResponse = await _httpClient.GetFromJsonAsync<SpotifyResponse>(
            $"tracks/{trackId}",
            cancellationToken);

        return apiResponse;
    }
}
