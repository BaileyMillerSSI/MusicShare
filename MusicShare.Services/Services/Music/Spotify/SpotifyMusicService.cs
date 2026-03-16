using Flurl;
using MusicShare.Contracts.Messages;
using System.Net.Http.Json;

namespace MusicShare.Services.Services.Music.Spotify;

/// <summary>
/// Spotify API service implementation.
/// Handles HTTP communication with Spotify Web API and returns raw Spotify DTOs.
/// </summary>
public class SpotifyMusicService(HttpClient spotifyClient) : ISpotifyMusicService
{
    private readonly HttpClient _httpClient = spotifyClient;

    public async Task<SpotifySearchResponse?> SearchAsync(
        SongMetadata metadata,
        int limit = 5,
        CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<SpotifySearchResponse>(
            new Url()
            .SetQueryParam("search", string.IsNullOrEmpty(metadata.Artists.FirstOrDefault())
            ? $"track:{metadata.Title}"
            : $"track:{metadata.Title} artist:{metadata.Artists.FirstOrDefault()}")
            .SetQueryParam("type", "track")
            .SetQueryParam("limit", limit),
            cancellationToken);

    public async Task<SpotifyResponse?> GetTrackAsync(
        string trackId,
        CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<SpotifyResponse>(
            $"tracks/{trackId}",
            cancellationToken);
}
