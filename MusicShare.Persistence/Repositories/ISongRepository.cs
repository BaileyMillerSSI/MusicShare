using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public interface ISongRepository
{
    Task<Song?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Song>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    Task<Song> InsertAsync(Song song, CancellationToken cancellationToken = default);
    Task<Song> UpsertAsync(Song song, CancellationToken cancellationToken = default);
    Task UpdateAsync(Song song, CancellationToken cancellationToken = default);
}
