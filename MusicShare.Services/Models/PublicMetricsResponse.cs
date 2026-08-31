using MusicShare.Contracts;

namespace MusicShare.Services.Models;

public record PublicMetricsResponse(
    long TotalCompletedSongs,
    DateTime? GeneratedAt,
    IReadOnlyList<PublicMetricsServiceCountResponse> ServiceCounts,
    IReadOnlyList<PublicMetricsRecentSongResponse> RecentSongs,
    IReadOnlyList<PublicMetricsWeeklyCompletedSongResponse> WeeklyCompletedSongs)
{
    public static IReadOnlyList<ServiceType> MetricsPlatforms { get; } = [ServiceType.Spotify, ServiceType.YouTubeMusic];

    public static PublicMetricsResponse Empty() => new(
        0,
        null,
        MetricsPlatforms
            .Select(x => new PublicMetricsServiceCountResponse(x, 0)).ToList(),
        [],
        []);
}

public record PublicMetricsServiceCountResponse(ServiceType Service, long Count);
public record PublicMetricsRecentSongResponse(
    string SongId, string ShareId, string Title, IReadOnlyList<string> Artists,
    string? Album, string? ArtworkUrl, ServiceType SourceService, DateTime CreatedAt);
public record PublicMetricsWeeklyCompletedSongResponse(DateTime WeekStart, long Count);
public record PublicMetricsRefreshResult(bool Accepted, PublicMetricsResponse Snapshot);
