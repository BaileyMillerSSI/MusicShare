using MusicShare.Contracts;

namespace MusicShare.Api.Services;

/// <summary>
/// Handles URL normalization and service type detection.
/// </summary>
public class UrlNormalizer
{
    public ServiceType DetectServiceType(string url)
    {
        // Normalize to lowercase for comparison
        var normalizedUrl = url.ToLowerInvariant();

        if (normalizedUrl.Contains("spotify.com") || normalizedUrl.StartsWith("spotify:"))
            return ServiceType.Spotify;

        if (normalizedUrl.Contains("music.apple.com"))
            return ServiceType.AppleMusic;

        if (normalizedUrl.Contains("music.youtube.com") ||
            normalizedUrl.Contains("youtube.com") ||
            normalizedUrl.Contains("youtu.be"))
            return ServiceType.YouTubeMusic;

        return ServiceType.Unknown;
    }

    public string GenerateShareId()
    {
        // Generate a short, URL-friendly share ID
        return Guid.NewGuid().ToString("N")[..12];
    }
}
