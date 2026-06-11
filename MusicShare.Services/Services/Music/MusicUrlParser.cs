using System.Text.RegularExpressions;

namespace MusicShare.Services.Services.Music;

internal static partial class MusicUrlParser
{
    private static readonly HashSet<string> SpotifyHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "open.spotify.com"
    };

    private static readonly HashSet<string> AppleMusicHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "music.apple.com"
    };

    private static readonly HashSet<string> YouTubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "music.youtube.com",
        "www.youtube.com",
        "youtube.com",
        "youtu.be"
    };

    public static bool IsSpotifyUrl(string url) =>
        TryExtractSpotifyTrackId(url, out _);

    public static bool IsAppleMusicUrl(string url) =>
        IsHttpsUrlForHost(url, AppleMusicHosts);

    public static bool IsYouTubeMusicUrl(string url) =>
        TryExtractYouTubeVideoId(url, out _);

    public static bool TryExtractSpotifyTrackId(string url, out string trackId)
    {
        trackId = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (TryExtractSpotifyUriTrackId(url, out trackId))
            return true;

        if (!TryCreateHttpsUri(url, out var uri) || !SpotifyHosts.Contains(uri.Host))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not ["track", var id])
            return false;

        trackId = id;
        return SpotifyTrackIdRegex().IsMatch(trackId);
    }

    public static bool TryExtractYouTubeVideoId(string url, out string videoId)
    {
        videoId = string.Empty;

        if (!TryCreateHttpsUri(url, out var uri) || !YouTubeHosts.Contains(uri.Host))
            return false;

        if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            videoId = uri.AbsolutePath.Trim('/').Split('/')[0];
            return YouTubeVideoIdRegex().IsMatch(videoId);
        }

        if (!uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase))
            return false;

        videoId = GetQueryValue(uri.Query, "v") ?? string.Empty;
        return YouTubeVideoIdRegex().IsMatch(videoId);
    }

    private static bool IsHttpsUrlForHost(string url, HashSet<string> allowedHosts) =>
        TryCreateHttpsUri(url, out var uri) && allowedHosts.Contains(uri.Host);

    private static bool TryCreateHttpsUri(string url, out Uri uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out uri!) && uri.Scheme == Uri.UriSchemeHttps)
            return true;

        uri = null!;
        return false;
    }

    private static bool TryExtractSpotifyUriTrackId(string url, out string trackId)
    {
        trackId = string.Empty;
        var parts = url.Split(':', StringSplitOptions.None);

        if (parts is not ["spotify", "track", var id])
            return false;

        trackId = id;
        return SpotifyTrackIdRegex().IsMatch(trackId);
    }

    private static string? GetQueryValue(string query, string name)
    {
        foreach (var parameter in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = parameter.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals(name, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9]{22}$")]
    private static partial Regex SpotifyTrackIdRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeVideoIdRegex();
}
