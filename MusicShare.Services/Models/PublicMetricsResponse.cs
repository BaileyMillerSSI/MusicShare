using MusicShare.Contracts;

namespace MusicShare.Services.Models;

public record PublicMetricsResponse(
    long TotalCompletedSongs,
    DateTime? GeneratedAt,
    IReadOnlyList<PublicMetricsServiceCountResponse> ServiceCounts,
    IReadOnlyList<PublicMetricsRecentSongResponse> RecentSongs)
{
    public static PublicMetricsResponse Empty() => new(
        0,
        null,
        Enum.GetValues<ServiceType>().Where(x => x != ServiceType.Unknown)
            .Select(x => new PublicMetricsServiceCountResponse(x, 0)).ToList(),
        []);
}

public record PublicMetricsServiceCountResponse(ServiceType Service, long Count);
public record PublicMetricsRecentSongResponse(
    string SongId, string ShareId, string Title, IReadOnlyList<string> Artists,
    string? Album, string? ArtworkUrl, ServiceType SourceService, DateTime CreatedAt);
public record PublicMetricsRefreshResult(bool Accepted, PublicMetricsResponse Snapshot);
