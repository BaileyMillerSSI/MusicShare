using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using YouTubeMusicAPI.Models;

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

    public async IAsyncEnumerable<SongSearchResult> FindSongsAsync(
        SongMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var result in await youTubeService.SearchSongsAsync(metadata, maxResults: 10, cancellationToken))
        {
            if (result?.Id == null)
                continue;

            yield return new SongSearchResult($"https://music.youtube.com/watch?v={result.Id}", MapToSongMetadata(result));
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

    private static SongMetadata MapToSongMetadata(YouTubeMusicAPI.Models.Search.SongSearchResult result) =>
        new()
        {
            Title = result.Name,
            Artists = result.Artists?.Select(a => a.Name) ?? [],
            Album = result.Album?.Name,
            ArtworkUrl = GetBestThumbnail(result.Thumbnails),
            Duration = result.Duration,
            IsExplicit = result.IsExplicit
        };

    private static SongMetadata MapToSongMetadata(YouTubeMusicAPI.Models.Info.SongVideoInfo info) =>
        new()
        {
            Title = info.Name,
            Artists = info.Artists?.Select(a => a.Name) ?? [],
            Album = info.Album?.Name,
            ArtworkUrl = GetBestThumbnail(info.Thumbnails),
            Duration = info.Duration,
            IsExplicit = info.IsExplicit
        };

    private static string? GetBestThumbnail(IEnumerable<Thumbnail>? thumbnails) =>
        thumbnails?
            .OrderByDescending(t => t.Width * t.Height)
            .FirstOrDefault()?.Url;
}
