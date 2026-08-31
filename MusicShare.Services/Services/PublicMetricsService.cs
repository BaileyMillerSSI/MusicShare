using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

public class PublicMetricsService(
    IShareRequestRepository shareRequests,
    ISongRepository songs,
    IPublicMetricsSnapshotRepository snapshots) : IPublicMetricsService
{
    private const int RecentSongLimit = 20;

    public async Task<PublicMetricsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshots.GetAsync(cancellationToken);
        return snapshot is null ? PublicMetricsResponse.Empty() : Map(snapshot);
    }

    public async Task<PublicMetricsRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var counts = await shareRequests.GetCompletedDistinctSongCountsBySourceAsync(cancellationToken);
        var recentRequests = await shareRequests.GetRecentCompletedDistinctAsync(RecentSongLimit, cancellationToken);
        var songsById = (await songs.GetByIdsAsync(recentRequests.Select(x => x.SongId), cancellationToken))
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
        var candidate = new PublicMetricsSnapshot
        {
            TotalCompletedSongs = counts.Values.Sum(),
            GeneratedAt = DateTime.UtcNow,
            ServiceCounts = Enum.GetValues<ServiceType>().Where(x => x != ServiceType.Unknown)
                .Select(x => new PublicMetricsServiceCount { Service = x, Count = counts.GetValueOrDefault(x) }).ToList(),
            RecentSongs = recentRequests.Where(x => songsById.ContainsKey(x.SongId)).Select(x =>
            {
                var song = songsById[x.SongId];
                return new PublicMetricsRecentSong
                {
                    SongId = x.SongId, ShareId = x.ShareId, Title = song.Title, Artists = song.Artists,
                    Album = song.Album, ArtworkUrl = song.ArtworkUrl, SourceService = x.SourceService, CreatedAt = x.CreatedAt
                };
            }).Take(RecentSongLimit).ToList()
        };
        var accepted = await snapshots.TryReplaceAsync(candidate, cancellationToken);
        return new PublicMetricsRefreshResult(accepted, Map(candidate));
    }

    private static PublicMetricsResponse Map(PublicMetricsSnapshot snapshot) => new(
        snapshot.TotalCompletedSongs, snapshot.GeneratedAt,
        snapshot.ServiceCounts.Select(x => new PublicMetricsServiceCountResponse(x.Service, x.Count)).ToList(),
        snapshot.RecentSongs.Take(RecentSongLimit).Select(x => new PublicMetricsRecentSongResponse(
            x.SongId, x.ShareId, x.Title, x.Artists, x.Album, x.ArtworkUrl, x.SourceService, x.CreatedAt)).ToList());
}
