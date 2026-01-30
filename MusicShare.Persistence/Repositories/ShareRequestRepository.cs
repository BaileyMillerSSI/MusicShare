using MongoDB.Driver;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class ShareRequestRepository : IShareRequestRepository
{
    private readonly IMongoCollection<ShareRequest> _requests;

    public ShareRequestRepository(MusicShareDbContext context)
    {
        _requests = context.ShareRequests;
    }

    public async Task<ShareRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ShareRequest>.Filter.Eq(r => r.Id, id);
        return await _requests.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ShareRequest?> GetByShareIdAsync(string shareId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ShareRequest>.Filter.Eq(r => r.ShareId, shareId);
        return await _requests.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ShareRequest?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ShareRequest>.Filter.Eq(r => r.CorrelationId, correlationId);
        return await _requests.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ShareRequest?> GetBySongIdAsync(string songId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ShareRequest>.Filter.Eq(r => r.SongId, songId);
        return await _requests.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ShareRequest> InsertAsync(ShareRequest request, CancellationToken cancellationToken = default)
    {
        request.CreatedAt = DateTime.UtcNow;
        await _requests.InsertOneAsync(request, cancellationToken: cancellationToken);
        return request;
    }

    public async Task UpdateAsync(ShareRequest request, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ShareRequest>.Filter.Eq(r => r.Id, request.Id);
        await _requests.ReplaceOneAsync(filter, request, cancellationToken: cancellationToken);
    }
}
