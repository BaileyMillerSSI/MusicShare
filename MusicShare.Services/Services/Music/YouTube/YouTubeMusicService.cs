using Microsoft.Extensions.Logging;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models.Info;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.Services.Services.Music.YouTube;

/// <summary>
/// Implementation of YouTube Music API integration service.
/// Uses the YouTubeMusicAPI library for search and metadata operations.
/// </summary>
public class YouTubeMusicService(YouTubeMusicClient client, ILogger<YouTubeMusicService> logger) : IYouTubeMusicService
{
    private readonly YouTubeMusicClient _client = client;
    private readonly ILogger<YouTubeMusicService> _logger = logger;

    public async Task<IReadOnlyList<SongSearchResult>> SearchSongsAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Searching YouTube Music for: {Query} (max: {MaxResults})", query, maxResults);

            var searchResults = _client.SearchAsync(query, SearchCategory.Songs);
            var results = await searchResults.FetchItemsAsync(0, maxResults);

            if (results == null || results.Count == 0)
            {
                _logger.LogDebug("No results found for query: {Query}", query);
                return Array.Empty<SongSearchResult>();
            }

            var songResults = results.OfType<SongSearchResult>().ToList();
            _logger.LogDebug("Found {Count} song results for query: {Query}", songResults.Count, query);
            return songResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching YouTube Music for query: {Query}", query);
            return Array.Empty<SongSearchResult>();
        }
    }

    public async Task<SongVideoInfo?> GetSongVideoInfoAsync(string videoId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching YouTube Music video info: {VideoId}", videoId);

            var info = await _client.GetSongVideoInfoAsync(videoId, cancellationToken);

            if (info == null)
            {
                _logger.LogWarning("No info returned for video ID: {VideoId}", videoId);
                return null;
            }

            _logger.LogDebug("Successfully fetched video info: {VideoId} ({Name})", videoId, info.Name);
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching YouTube Music video info: {VideoId}", videoId);
            return null;
        }
    }
}
