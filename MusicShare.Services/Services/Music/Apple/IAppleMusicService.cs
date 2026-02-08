namespace MusicShare.Services.Services.Music.Apple;

/// <summary>
/// Service interface for Apple Music API integration.
/// Currently implemented as a mock service returning deterministic fake data.
/// </summary>
public interface IAppleMusicService
{
    /// <summary>
    /// Searches Apple Music for tracks matching the given query.
    /// Mock implementation returns deterministic fake results.
    /// </summary>
    /// <param name="query">Search query string</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of mock track results</returns>
    Task<IReadOnlyList<AppleMusicTrack>> SearchTracksAsync(string query, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets track details by Apple Music track ID.
    /// Mock implementation returns deterministic fake data.
    /// </summary>
    /// <param name="trackId">Apple Music track ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Mock track details, or null if invalid ID format</returns>
    Task<AppleMusicTrack?> GetTrackAsync(string trackId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mock Apple Music track DTO.
/// TODO: Replace with actual Apple Music API models when implementing real integration.
/// </summary>
public record AppleMusicTrack(
    string Id,
    string Name,
    IEnumerable<string> Artists,
    string? AlbumName,
    string? ArtworkUrl,
    TimeSpan Duration,
    bool? IsExplicit);
