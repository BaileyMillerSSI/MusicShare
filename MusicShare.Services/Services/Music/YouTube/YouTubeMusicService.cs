using Microsoft.Extensions.Logging;
using MusicShare.Contracts.Messages;
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

    public async Task<IReadOnlyList<SongSearchResult>> SearchSongsAsync(SongMetadata metadata, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var searchResults = _client.SearchAsync(BuildSearchQuery(metadata), SearchCategory.Songs);
            var results = await searchResults.FetchItemsAsync(0, maxResults);

            if (results == null || results.Count == 0)
            {
                return [];
            }

            return results.OfType<SongSearchResult>().ToList();
        }
        catch
        {
            return [];
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

    private static string BuildSearchQuery(SongMetadata metadata)
    {
        var artist = metadata.Artists.FirstOrDefault();
        return string.IsNullOrEmpty(artist)
            ? metadata.Title
            : $"{metadata.Title} {artist}";
    }
}
