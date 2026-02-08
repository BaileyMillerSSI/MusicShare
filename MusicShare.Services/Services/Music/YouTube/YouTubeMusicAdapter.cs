using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using YouTubeMusicAPI.Models;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.Services.Services.Music.YouTube;

/// <summary>
/// YouTube Music adapter that delegates to IYouTubeMusicService for API operations.
/// Responsible for URL handling, DTO transformation, and metadata mapping.
/// </summary>
public class YouTubeMusicAdapter(
    IYouTubeMusicService youTubeService,
    ILogger<YouTubeMusicAdapter> logger) : IMusicServiceAdapter
{

    public ServiceType ServiceType => ServiceType.YouTubeMusic;

    public async Task<SongMetadata?> ResolveMetadataAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var videoId = ExtractSongId(url);
        if (string.IsNullOrEmpty(videoId))
        {
            logger.LogWarning("Could not extract video ID from URL: {Url}", url);
            return null;
        }

        try
        {
            var info = await youTubeService.GetSongVideoInfoAsync(videoId, cancellationToken);
            if (info == null)
            {
                logger.LogWarning("No info returned for video ID: {VideoId}", videoId);
                return null;
            }

            return MapToSongMetadata(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving metadata for video ID: {VideoId}", videoId);
            return null;
        }
    }

    public async IAsyncEnumerable<Models.SongSearchResult> FindSongsAsync(
        SongMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = BuildSearchQuery(metadata);
        logger.LogDebug("Searching YouTube Music for: {Query}", query);

        IReadOnlyList<YouTubeMusicAPI.Models.Search.SongSearchResult> results;
        try
        {
            results = await youTubeService.SearchSongsAsync(query, maxResults: 10, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching YouTube Music for: {Query}", query);
            yield break;
        }

        if (results == null || results.Count == 0)
        {
            logger.LogDebug("No YouTube Music results found for query: {Query}", query);
            yield break;
        }

        foreach (var result in results)
        {
            if (result?.Id == null)
                continue;

            var url = $"https://music.youtube.com/watch?v={result.Id}";
            var foundMetadata = MapToSongMetadata(result);
            yield return new Models.SongSearchResult(url, foundMetadata);
        }
    }

    [Obsolete("Use FindSongsAsync() instead. This method will be removed in a future version.")]
    public async Task<string?> FindSongAsync(
        SongMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        await foreach (var result in FindSongsAsync(metadata, cancellationToken))
        {
            return result.Url;
        }

        return null;
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

    private static SongMetadata MapToSongMetadata(YouTubeMusicAPI.Models.Search.SongSearchResult result)
    {
        return new SongMetadata
        {
            Title = result.Name,
            Artists = result.Artists?.Select(a => a.Name) ?? [],
            Album = result.Album?.Name,
            ArtworkUrl = GetBestThumbnail(result.Thumbnails),
            Duration = result.Duration,
            IsExplicit = result.IsExplicit
        };
    }

    private static SongMetadata MapToSongMetadata(YouTubeMusicAPI.Models.Info.SongVideoInfo info)
    {
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

    private static string? GetBestThumbnail(IEnumerable<Thumbnail>? thumbnails)
    {
        return thumbnails?
            .OrderByDescending(t => t.Width * t.Height)
            .FirstOrDefault()?.Url;
    }
}
