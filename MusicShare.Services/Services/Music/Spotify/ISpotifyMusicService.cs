namespace MusicShare.Services.Services.Music.Spotify;

/// <summary>
/// Service interface for Spotify API integration.
/// Handles search operations and returns raw Spotify DTOs.
/// </summary>
public interface ISpotifyMusicService
{
    /// <summary>
    /// Searches Spotify for tracks matching the given query.
    /// </summary>
    /// <param name="query">Search query string (URL-encoded by caller)</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Spotify search response containing track results, or null if search fails</returns>
    Task<SpotifySearchResponse?> SearchAsync(string query, int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets track details by Spotify track ID.
    /// </summary>
    /// <param name="trackId">Spotify track ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Spotify track response, or null if not found</returns>
    Task<SpotifyResponse?> GetTrackAsync(string trackId, CancellationToken cancellationToken = default);
}
