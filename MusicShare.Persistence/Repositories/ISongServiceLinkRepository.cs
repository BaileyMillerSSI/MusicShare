using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public interface ISongServiceLinkRepository
{
    Task<SongServiceLink?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<SongServiceLink>> GetBySongIdAsync(string songId, CancellationToken cancellationToken = default);
    Task<SongServiceLink?> GetBySongIdAndServiceAsync(string songId, ServiceType serviceType, CancellationToken cancellationToken = default);
    Task<SongServiceLink?> GetByServiceAndSongIdAsync(ServiceType serviceType, string serviceSongId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SongServiceLink>> GetBySongIdsAsync(IReadOnlyCollection<string> songIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<ServiceType, long>> GetCompletedDistinctSongLinkCountsAsync(CancellationToken cancellationToken = default);
    Task<SongServiceLink> InsertAsync(SongServiceLink link, CancellationToken cancellationToken = default);
    Task<List<SongServiceLink>> InsertManyAsync(List<SongServiceLink> links, CancellationToken cancellationToken = default);
}
