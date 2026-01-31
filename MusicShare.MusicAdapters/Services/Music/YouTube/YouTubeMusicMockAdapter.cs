using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.MusicAdapters.Services.Music.YouTube;

/// <summary>
/// Mock YouTube Music adapter that returns deterministic fake data.
/// TODO: Replace with real YouTube Music API integration for production.
/// </summary>
public class YouTubeMusicMockAdapter : IMusicServiceAdapter
{
    public ServiceType ServiceType => ServiceType.YouTubeMusic;

    public Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        var songId = ExtractSongId(url);
        if (string.IsNullOrEmpty(songId))
            return Task.FromResult<SongMetadata?>(null);

        // Return deterministic fake data based on song ID
        var metadata = new SongMetadata
        {
            Title = $"Song {songId}",
            Artists = new List<string> { "Artist A", "Artist B" },
            Album = $"Album {songId}",
            ArtworkUrl = $"https://i.ytimg.com/vi/{songId}/maxresdefault.jpg",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(30)),
            IsExplicit = null
        };

        return Task.FromResult<SongMetadata?>(metadata);
    }

    public Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default)
    {
        // Mock: Generate a deterministic YouTube Music URL based on song title
        var mockVideoId = GenerateMockVideoId(metadata.Title);
        var url = $"https://music.youtube.com/watch?v={mockVideoId}";

        return Task.FromResult<string?>(url);
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
        // Extract from URLs like:
        // https://music.youtube.com/watch?v=dQw4w9WgXcQ
        // https://www.youtube.com/watch?v=dQw4w9WgXcQ
        // https://youtu.be/dQw4w9WgXcQ

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

        if (url.Contains("youtu.be/"))
        {
            var parts = url.Split('/');
            return parts.LastOrDefault()?.Split('?')[0];
        }

        return null;
    }

    private static string GenerateMockVideoId(string title)
    {
        // Generate a deterministic 11-character video ID based on title
        var hash = title.GetHashCode();
        return $"mock{Math.Abs(hash):x7}";
    }
}
