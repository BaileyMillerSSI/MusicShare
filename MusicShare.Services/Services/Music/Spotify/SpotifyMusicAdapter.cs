using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services.Music.Spotify;

/// <summary>
/// Spotify Music adapter that delegates to ISpotifyMusicService for API operations.
/// Responsible for URL handling, DTO transformation, and metadata mapping.
/// </summary>
public class SpotifyMusicAdapter(
    ISpotifyMusicService spotifyService,
    ILogger<SpotifyMusicAdapter> logger) : IMusicServiceAdapter
{
    public ServiceType ServiceType => ServiceType.Spotify;

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
            var response = await spotifyService.GetTrackAsync(trackId, cancellationToken);
            if (response == null)
            {
                logger.LogWarning("No track found for ID: {TrackId}", trackId);
                return null;
            }

            return MapToSongMetadata(response);
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
        await foreach (var track in GetSearchResponse(metadata, cancellationToken))
        {
            if (track?.external_urls?.spotify == null)
                continue;

            yield return new SongSearchResult(track.external_urls.spotify, MapToSongMetadata(track));
        }
    }

    public string NormalizeUrl(string url)
    {
        var trackId = ExtractSongId(url);
        return string.IsNullOrEmpty(trackId)
            ? url
            : $"https://open.spotify.com/track/{trackId}";
    }

    public string? ExtractSongId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Handle URLs like: https://open.spotify.com/track/TRACK_ID or spotify:track:TRACK_ID
        if (url.Contains("spotify.com/track/"))
        {
            var parts = url.Split('/');
            var trackIndex = Array.IndexOf(parts, "track");
            if (trackIndex >= 0 && trackIndex + 1 < parts.Length)
            {
                return parts[trackIndex + 1].Split('?')[0];
            }
        }

        if (url.StartsWith("spotify:track:"))
        {
            return url.Split(':')[2];
        }

        return null;
    }

    private static SongMetadata MapToSongMetadata(SpotifyResponse track) =>
        new SongMetadata
        {
            Title = track.name,
            Artists = track.artists?.Select(a => a.name) ?? [],
            Album = track.album?.name,
            ArtworkUrl = track.album?.images?.OrderByDescending(i => i.width * i.height).FirstOrDefault()?.url,
            Duration = TimeSpan.FromMilliseconds(track.Duration),
            IsExplicit = track.Explicit
        };

    private async IAsyncEnumerable<SpotifyResponse> GetSearchResponse(SongMetadata metadata, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        SpotifySearchResponse? response;
        try
        {
            response = await spotifyService.SearchAsync(metadata, limit: 10, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching Spotify for metadata: {Title}", metadata.Title);
            yield break;
        }

        if (response?.tracks?.items != null)
        {
            foreach (var track in response.tracks.items)
            {
                yield return track;
            }
        }
    }
}
