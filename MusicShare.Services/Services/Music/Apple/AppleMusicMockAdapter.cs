using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services.Music;

namespace MusicShare.Services.Services.Music.Apple;

/// <summary>
/// Apple Music adapter that delegates to IAppleMusicService for API operations.
/// Currently uses mock service returning deterministic fake data.
/// TODO: Replace with real Apple Music API integration for production.
/// </summary>
public class AppleMusicMockAdapter(
    IAppleMusicService appleMusicService,
    ILogger<AppleMusicMockAdapter> logger) : IMusicServiceAdapter
{
    public ServiceType ServiceType => ServiceType.AppleMusic;

    public async Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        var trackId = ExtractSongId(url);
        if (string.IsNullOrEmpty(trackId))
        {
            logger.LogWarning("Could not extract track ID from URL: {Url}", url);
            return null;
        }

        try
        {
            var track = await appleMusicService.GetTrackAsync(trackId, cancellationToken);
            if (track == null)
            {
                logger.LogWarning("No track found for ID: {TrackId}", trackId);
                return null;
            }

            return MapToSongMetadata(track);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving metadata for track ID: {TrackId}", trackId);
            return null;
        }
    }

    public async IAsyncEnumerable<SongSearchResult> FindSongsAsync(
        SongMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = BuildSearchQuery(metadata);
        logger.LogDebug("Searching Apple Music for: {Query}", query);

        IReadOnlyList<AppleMusicTrack> results;
        try
        {
            results = await appleMusicService.SearchTracksAsync(query, limit: 10, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching Apple Music for: {Query}", query);
            yield break;
        }

        if (results == null || results.Count == 0)
        {
            logger.LogDebug("No Apple Music results found for query: {Query}", query);
            yield break;
        }

        foreach (var track in results)
        {
            var url = $"https://music.apple.com/us/song/{track.Id}";
            var foundMetadata = MapToSongMetadata(track);
            yield return new SongSearchResult(url, foundMetadata);
        }
    }

    [Obsolete("Use FindSongsAsync() instead. This method will be removed in a future version.")]
    public async Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default)
    {
        await foreach (var result in FindSongsAsync(metadata, cancellationToken))
        {
            return result.Url;
        }

        return null;
    }

    public string NormalizeUrl(string url)
    {
        var songId = ExtractSongId(url);
        return string.IsNullOrEmpty(songId)
            ? url
            : $"https://music.apple.com/us/song/{songId}";
    }

    public string? ExtractSongId(string url)
    {
        // Extract from URLs like:
        // https://music.apple.com/us/song/1234567890
        // https://music.apple.com/us/album/album-name/987654321?i=1234567890

        if (url.Contains("music.apple.com"))
        {
            // Check for ?i= parameter (song ID in album context)
            if (url.Contains("?i="))
            {
                var queryPart = url.Split('?')[1];
                var iParam = queryPart.Split('&')
                    .FirstOrDefault(p => p.StartsWith("i="));
                return iParam?.Split('=')[1];
            }

            // Otherwise extract from /song/ path
            if (url.Contains("/song/"))
            {
                var parts = url.Split('/');
                var songIndex = Array.IndexOf(parts, "song");
                if (songIndex >= 0 && songIndex + 1 < parts.Length)
                {
                    return parts[songIndex + 1].Split('?')[0];
                }
            }
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

    private static SongMetadata MapToSongMetadata(AppleMusicTrack track)
    {
        return new SongMetadata
        {
            Title = track.Name,
            Artists = track.Artists,
            Album = track.AlbumName,
            ArtworkUrl = track.ArtworkUrl,
            Duration = track.Duration,
            IsExplicit = track.IsExplicit
        };
    }
}
