using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters.Configuration.MusicServices;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.MusicAdapters.Services.Music.YouTube;

/// <summary>
/// YouTube Music adapter using the YouTubeMusicAPI library.
/// </summary>
public class YouTubeMusicAdapter(ILogger<YouTubeMusicAdapter> logger, YouTubeMusicClient client) : IMusicServiceAdapter
{
    private readonly YouTubeMusicClient _client = client;
    private readonly ILogger<YouTubeMusicAdapter> _logger = logger;

    public ServiceType ServiceType => ServiceType.YouTubeMusic;

    public async Task<SongMetadata?> ResolveMetadataAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var videoId = ExtractSongId(url);
        if (string.IsNullOrEmpty(videoId))
        {
            _logger.LogWarning("Could not extract video ID from URL: {Url}", url);
            return null;
        }

        try
        {
            var info = await _client.GetSongVideoInfoAsync(videoId);

            if (info == null)
            {
                _logger.LogWarning("No info returned for video ID: {VideoId}", videoId);
                return null;
            }

            return new SongMetadata
            {
                Title = info.Name,
                Artists = info.Artists?.Select(a => a.Name) ?? [],
                Album = info.Album?.Name,
                ArtworkUrl = GetBestThumbnail(info.Thumbnails),
                Duration = info.Duration,
                IsExplicit = info.IsExplicit
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving metadata for video ID: {VideoId}", videoId);
            return null;
        }
    }

    public async Task<string?> FindSongAsync(
        SongMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = BuildSearchQuery(metadata);

            _logger.LogDebug("Searching YouTube Music for: {Query}", query);

            var searchResults = _client.SearchAsync(query, SearchCategory.Songs);
            var results = await searchResults.FetchItemsAsync(0, 10);

            if (results == null || results.Count == 0)
            {
                _logger.LogDebug("No results found for query: {Query}", query);
                return null;
            }

            foreach (var result in results.OfType<SongSearchResult>())
            {
                if (IsMatch(result, metadata))
                {
                    var url = $"https://music.youtube.com/watch?v={result.Id}";
                    _logger.LogDebug("Found match: {Url}", url);
                    return url;
                }
            }

            // If no exact match, return the first result as a fallback
            var firstResult = results.OfType<SongSearchResult>().FirstOrDefault();
            if (firstResult != null)
            {
                var url = $"https://music.youtube.com/watch?v={firstResult.Id}";
                _logger.LogDebug("No exact match, using first result: {Url}", url);
                return url;
            }

            _logger.LogDebug("No matching song found for: {Title} by {Artists}",
                metadata.Title, string.Join(", ", metadata.Artists));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for song: {Title}", metadata.Title);
            return null;
        }
    }

    public string NormalizeUrl(string url)
    {
        var videoId = ExtractSongId(url);
        return string.IsNullOrEmpty(videoId)
            ? url
            : $"https://music.youtube.com/watch?v={videoId}";
    }

    public string? ExtractSongId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Handle watch URLs: youtube.com/watch?v=... or music.youtube.com/watch?v=...
        if (url.Contains("youtube.com/watch") || url.Contains("music.youtube.com/watch"))
        {
            var queryPart = url.Split('?').LastOrDefault();
            if (queryPart != null)
            {
                var vParam = queryPart.Split('&')
                    .FirstOrDefault(p => p.StartsWith("v="));
                return vParam?.Split('=')[1];
            }
        }

        // Handle short URLs: youtu.be/VIDEO_ID
        if (url.Contains("youtu.be/"))
        {
            var parts = url.Split('/');
            return parts.LastOrDefault()?.Split('?')[0];
        }

        return null;
    }

    private static string BuildSearchQuery(SongMetadata metadata)
    {
        var artist = metadata.Artists.FirstOrDefault();
        return string.IsNullOrEmpty(artist)
            ? metadata.Title
            : $"{metadata.Title} {artist}";
    }

    private static bool IsMatch(SongSearchResult result, SongMetadata metadata)
    {
        // Check title match (case-insensitive, allowing partial matches)
        var titleMatches = result.Name.Contains(metadata.Title, StringComparison.OrdinalIgnoreCase)
            || metadata.Title.Contains(result.Name, StringComparison.OrdinalIgnoreCase);

        if (!titleMatches)
            return false;

        // Check for at least one artist match
        var resultArtists = result.Artists?.Select(a => a.Name) ?? [];
        var hasArtistMatch = metadata.Artists.Any(inputArtist =>
            resultArtists.Any(resultArtist =>
                resultArtist.Contains(inputArtist, StringComparison.OrdinalIgnoreCase)
                || inputArtist.Contains(resultArtist, StringComparison.OrdinalIgnoreCase)));

        return hasArtistMatch;
    }

    private static string? GetBestThumbnail(IEnumerable<Thumbnail>? thumbnails)
    {
        return thumbnails?
            .OrderByDescending(t => t.Width * t.Height)
            .FirstOrDefault()?.Url;
    }
}
