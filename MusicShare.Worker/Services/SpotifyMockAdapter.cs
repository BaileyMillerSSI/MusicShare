using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.Worker.Services;

/// <summary>
/// Mock Spotify adapter that returns deterministic fake data.
/// TODO: Replace with real Spotify API integration for production.
/// </summary>
public class SpotifyMockAdapter : IMusicServiceAdapter
{
    public ServiceType ServiceType => ServiceType.Spotify;

    public Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        var songId = ExtractSongId(url);
        if (string.IsNullOrEmpty(songId))
            return Task.FromResult<SongMetadata?>(null);

        // Return deterministic fake data based on song ID
        var metadata = new SongMetadata
        {
            Title = $"Clumsy",
            Artists = new List<string> { "Fergie" },
            Album = $"Album {songId}",
            ArtworkUrl = $"https://i.scdn.co/image/{songId}",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(30))
        };

        return Task.FromResult<SongMetadata?>(metadata);
    }

    public Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default)
    {
        // Mock: Generate a deterministic Spotify URL based on song title
        var mockSongId = GenerateMockSongId(metadata.Title);
        var url = $"https://open.spotify.com/track/{mockSongId}";

        return Task.FromResult<string?>(url);
    }

    public string NormalizeUrl(string url)
    {
        var songId = ExtractSongId(url);
        return string.IsNullOrEmpty(songId)
            ? url
            : $"https://open.spotify.com/track/{songId}";
    }

    public string? ExtractSongId(string url)
    {
        // Extract from URLs like:
        // https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6
        // spotify:track:6rqhFgbbKwnb9MLmUQDhG6

        if (url.Contains("spotify.com/track/"))
        {
            var parts = url.Split('/');
            var trackPart = parts.LastOrDefault(p => !string.IsNullOrEmpty(p));
            return trackPart?.Split('?')[0]; // Remove query params
        }

        if (url.StartsWith("spotify:track:"))
        {
            return url.Split(':').LastOrDefault();
        }

        return null;
    }

    private static string GenerateMockSongId(string title)
    {
        // Generate a deterministic 22-character ID based on title
        var hash = title.GetHashCode();
        return $"mock{Math.Abs(hash):x16}".PadRight(22, 'x');
    }
}
