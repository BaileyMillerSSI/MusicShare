using MongoDB.Driver;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class SongServiceLinkRepository(IMusicShareDbContext context) : ISongServiceLinkRepository
{
    private readonly IMongoCollection<SongServiceLink> _links = context.SongServiceLinks;

    public async Task<SongServiceLink?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SongServiceLink>.Filter.Eq(l => l.Id, id);
        return await _links.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<SongServiceLink>> GetBySongIdAsync(string songId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SongServiceLink>.Filter.Eq(l => l.SongId, songId);
        return await _links.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<SongServiceLink?> GetBySongIdAndServiceAsync(string songId, ServiceType serviceType, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SongServiceLink>.Filter.And(
            Builders<SongServiceLink>.Filter.Eq(l => l.SongId, songId),
            Builders<SongServiceLink>.Filter.Eq(l => l.ServiceType, serviceType)
        );
        return await _links.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SongServiceLink?> GetByServiceAndSongIdAsync(ServiceType serviceType, string serviceSongId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SongServiceLink>.Filter.And(
            Builders<SongServiceLink>.Filter.Eq(l => l.ServiceType, serviceType),
            Builders<SongServiceLink>.Filter.Eq(l => l.ServiceSongId, serviceSongId)
        );
        return await _links.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SongServiceLink> InsertAsync(SongServiceLink link, CancellationToken cancellationToken = default)
    {
        link.CreatedAt = DateTime.UtcNow;
        await _links.InsertOneAsync(link, cancellationToken: cancellationToken);
        return link;
    }

    public async Task<List<SongServiceLink>> InsertManyAsync(List<SongServiceLink> links, CancellationToken cancellationToken = default)
    {
        if (links.Count == 0) return links;

        foreach (var link in links)
        {
            link.CreatedAt = DateTime.UtcNow;
        }

        await _links.InsertManyAsync(links, cancellationToken: cancellationToken);
        return links;
    }
}
