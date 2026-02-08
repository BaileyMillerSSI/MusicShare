using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MusicShare.Services.Services.Music.Spotify;

/// <summary>
/// Implementation of Spotify API integration service.
/// Handles authentication via SpotifyAccessTokenHandler and provides search/lookup operations.
/// </summary>
public class SpotifyMusicServiceImpl(HttpClient spotifyClient, ILogger<SpotifyMusicServiceImpl> logger) : ISpotifyMusicService
{
    private readonly HttpClient _httpClient = spotifyClient;
    private readonly ILogger<SpotifyMusicServiceImpl> _logger = logger;

    public async Task<SpotifySearchResponse?> SearchAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Searching Spotify for query: {Query} (limit: {Limit})", query, limit);

            var searchResult = await _httpClient.GetFromJsonAsync<SpotifySearchResponse>(
                $"search?q={query}&type=track&limit={limit}",
                cancellationToken);

            if (searchResult?.tracks?.items == null || searchResult.tracks.items.Length == 0)
            {
                _logger.LogDebug("No results found for query: {Query}", query);
                return null;
            }

            _logger.LogDebug("Found {Count} results for query: {Query}", searchResult.tracks.items.Length, query);
            return searchResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Spotify for query: {Query}", query);
            return null;
        }
    }

    public async Task<SpotifyResponse?> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Spotify track: {TrackId}", trackId);

            var track = await _httpClient.GetFromJsonAsync<SpotifyResponse>(
                $"tracks/{trackId}",
                cancellationToken);

            if (track == null)
            {
                _logger.LogWarning("Track not found: {TrackId}", trackId);
                return null;
            }

            _logger.LogDebug("Successfully fetched track: {TrackId} ({Name})", trackId, track.name);
            return track;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Spotify track: {TrackId}", trackId);
            return null;
        }
    }
}
