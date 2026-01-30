using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.Worker.Services;

/// <summary>
/// Mock Apple Music adapter that returns deterministic fake data.
/// TODO: Replace with real Apple Music API integration for production.
/// </summary>
public class AppleMusicMockAdapter : IMusicServiceAdapter
{
    public ServiceType ServiceType => ServiceType.AppleMusic;

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
            ArtworkUrl = $"https://is1-ssl.mzstatic.com/image/{songId}",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(30))
        };

        return Task.FromResult<SongMetadata?>(metadata);
    }

    public Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default)
    {
        // Mock: Generate a deterministic Apple Music URL based on song title
        var mockSongId = GenerateMockSongId(metadata.Title);
        var url = $"https://music.apple.com/us/song/{mockSongId}";

        return Task.FromResult<string?>(url);
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

    private static string GenerateMockSongId(string title)
    {
        // Generate a deterministic numeric ID based on title
        var hash = Math.Abs(title.GetHashCode());
        return hash.ToString();
    }
}
