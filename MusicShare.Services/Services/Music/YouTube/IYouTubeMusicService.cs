using YouTubeMusicAPI.Models.Info;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.Services.Services.Music.YouTube;

/// <summary>
/// Service interface for YouTube Music API integration.
/// Handles search and metadata retrieval operations using the YouTubeMusicAPI library.
/// </summary>
public interface IYouTubeMusicService
{
    /// <summary>
    /// Searches YouTube Music for songs matching the given query.
    /// </summary>
    /// <param name="query">Search query string</param>
    /// <param name="maxResults">Maximum number of results to fetch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of song search results, or empty list if search fails</returns>
    Task<IReadOnlyList<SongSearchResult>> SearchSongsAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed song information by video ID.
    /// </summary>
    /// <param name="videoId">YouTube video ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Song video info, or null if not found</returns>
    Task<SongVideoInfo?> GetSongVideoInfoAsync(string videoId, CancellationToken cancellationToken = default);
}
