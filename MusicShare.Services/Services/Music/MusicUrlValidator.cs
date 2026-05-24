using System.Text.RegularExpressions;
using MusicShare.Contracts;

namespace MusicShare.Services.Services.Music;

internal static partial class MusicUrlValidator
{
    public static ServiceType? DetectServiceType(string url)
    {
        if (TryExtractSpotifyTrackId(url, out _))
            return ServiceType.Spotify;

        if (TryExtractYouTubeVideoId(url, out _))
            return ServiceType.YouTubeMusic;

        if (!TryCreateHttpsUri(url, out var uri))
            return null;

        return uri.Host.ToLowerInvariant() switch
        {
            "music.apple.com" => ServiceType.AppleMusic,
            _ => null
        };
    }

    public static bool TryExtractSpotifyTrackId(string url, out string trackId)
    {
        trackId = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        var uriMatch = SpotifyTrackUriRegex().Match(url);
        if (uriMatch.Success)
        {
            trackId = uriMatch.Groups["id"].Value;
            return true;
        }

        if (!TryCreateHttpsUri(url, out var uri) ||
            !IsAllowedHost(uri, "open.spotify.com", "play.spotify.com"))
        {
            return false;
        }

        var segments = GetPathSegments(uri);
        if (segments.Length != 2 || !segments[0].Equals("track", StringComparison.OrdinalIgnoreCase))
            return false;

        return TryUseSpotifyTrackId(segments[1], out trackId);
    }

    public static bool TryExtractYouTubeVideoId(string url, out string videoId)
    {
        videoId = string.Empty;

        if (!TryCreateHttpsUri(url, out var uri))
            return false;

        if (IsAllowedHost(uri, "youtu.be"))
        {
            var segments = GetPathSegments(uri);
            return segments.Length == 1 && TryUseYouTubeVideoId(segments[0], out videoId);
        }

        if (!IsAllowedHost(uri, "music.youtube.com", "www.youtube.com", "youtube.com") ||
            !uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQuery(uri.Query);
        return query.TryGetValue("v", out var value) && TryUseYouTubeVideoId(value, out videoId);
    }

    private static bool TryCreateHttpsUri(string url, out Uri uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out uri!) &&
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool IsAllowedHost(Uri uri, params string[] allowedHosts) =>
        allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    private static string[] GetPathSegments(Uri uri) =>
        uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
                values[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
        }

        return values;
    }

    private static bool TryUseSpotifyTrackId(string value, out string trackId)
    {
        trackId = value;
        return SpotifyTrackIdRegex().IsMatch(value);
    }

    private static bool TryUseYouTubeVideoId(string value, out string videoId)
    {
        videoId = value;
        return YouTubeVideoIdRegex().IsMatch(value);
    }

    [GeneratedRegex(@"^spotify:track:(?<id>[A-Za-z0-9]{22})$", RegexOptions.IgnoreCase)]
    private static partial Regex SpotifyTrackUriRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{22}$")]
    private static partial Regex SpotifyTrackIdRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeVideoIdRegex();
}
