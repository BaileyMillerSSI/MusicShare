using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

public class PublicMetricsService(
    IShareRequestRepository shareRequests,
    ISongServiceLinkRepository links,
    ISongRepository songs,
    IPublicMetricsSnapshotRepository snapshots) : IPublicMetricsService
{
    private const int RecentSongLimit = 20;
    private const int WeeklyCompletedSongBucketCount = 8;

    public async Task<PublicMetricsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshots.GetAsync(cancellationToken);
        return snapshot is null ? PublicMetricsResponse.Empty() : Map(snapshot);
    }

    public async Task<PublicMetricsRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.UtcNow;
        var currentWeekStart = GetSundayStartUtc(generatedAt);
        var rangeStart = currentWeekStart.AddDays(-7 * (WeeklyCompletedSongBucketCount - 1));
        var rangeEnd = currentWeekStart.AddDays(7);
        var snapshotVersion = await snapshots.ReserveVersionAsync(cancellationToken);
        var totalCompletedSongs = await shareRequests.GetCompletedDistinctSongCountAsync(cancellationToken);
        var counts = await links.GetCompletedDistinctSongLinkCountsAsync(cancellationToken) ?? new Dictionary<ServiceType, long>();
        var recentRequests = await shareRequests.GetRecentCompletedDistinctAsync(RecentSongLimit, cancellationToken) ?? [];
        var weeklyCounts = await shareRequests.GetCompletedDistinctSongCountsByWeekAsync(rangeStart, rangeEnd, cancellationToken) ?? [];
        var songsById = (await songs.GetByIdsAsync(recentRequests.Select(x => x.SongId), cancellationToken) ?? [])
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
        var candidate = new PublicMetricsSnapshot
        {
            TotalCompletedSongs = totalCompletedSongs,
            SnapshotVersion = snapshotVersion,
            GeneratedAt = generatedAt,
            ServiceCounts = PublicMetricsResponse.MetricsPlatforms
                .Select(x => new PublicMetricsServiceCount { Service = x, Count = counts.GetValueOrDefault(x) }).ToList(),
            RecentSongs = recentRequests.Where(x => IsPublicSourceService(x.SourceService) && songsById.ContainsKey(x.SongId)).Select(x =>
            {
                var song = songsById[x.SongId];
                return new PublicMetricsRecentSong
                {
                    SongId = x.SongId, ShareId = x.ShareId, Title = song.Title, Artists = song.Artists,
                    Album = song.Album, ArtworkUrl = song.ArtworkUrl, SourceService = x.SourceService, CreatedAt = x.CreatedAt
                };
            }).Take(RecentSongLimit).ToList(),
            WeeklyCompletedSongs = Enumerable.Range(0, WeeklyCompletedSongBucketCount).Select(index =>
            {
                var weekStart = rangeStart.AddDays(index * 7);
                return new PublicMetricsWeeklyCompletedSong { WeekStart = weekStart, Count = weeklyCounts.FirstOrDefault(x => x.WeekStart == weekStart)?.Count ?? 0 };
            }).ToList()
        };
        var accepted = await snapshots.TryReplaceAsync(candidate, cancellationToken);
        return new PublicMetricsRefreshResult(accepted, Map(candidate));
    }

    private static PublicMetricsResponse Map(PublicMetricsSnapshot snapshot) => new(
        snapshot.TotalCompletedSongs, snapshot.GeneratedAt,
        PublicMetricsResponse.MetricsPlatforms.Select(service => new PublicMetricsServiceCountResponse(
            service, snapshot.ServiceCounts.FirstOrDefault(x => x.Service == service)?.Count ?? 0)).ToList(),
        snapshot.RecentSongs.Take(RecentSongLimit).Select(x => new PublicMetricsRecentSongResponse(
            x.SongId, x.ShareId, x.Title, x.Artists, x.Album, x.ArtworkUrl, x.SourceService, x.CreatedAt)).ToList(),
        (snapshot.WeeklyCompletedSongs ?? []).Where(x => x.Count >= 0).OrderBy(x => x.WeekStart).Select(x => new PublicMetricsWeeklyCompletedSongResponse(x.WeekStart, x.Count)).ToList());

    public static DateTime GetSundayStartUtc(DateTime utcTimestamp)
    {
        if (utcTimestamp.Kind != DateTimeKind.Utc) throw new ArgumentException("The timestamp must be UTC.", nameof(utcTimestamp));
        return utcTimestamp.Date.AddDays(-(int)utcTimestamp.DayOfWeek);
    }

    private static bool IsPublicSourceService(ServiceType service) =>
        service != ServiceType.Unknown && Enum.IsDefined(service);
}
