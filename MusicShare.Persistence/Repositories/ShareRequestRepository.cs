using MongoDB.Driver;
using MongoDB.Bson;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class ShareRequestRepository(IMusicShareDbContext context) : IShareRequestRepository
{
    private readonly IMongoCollection<ShareRequest> _requests = context.ShareRequests;

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

    public async Task<ShareRequest?> GetByServiceTrackIdAsync(
        ServiceType serviceType,
        string serviceTrackId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ShareRequest>.Filter.And(
            Builders<ShareRequest>.Filter.Eq(r => r.SourceService, serviceType),
            Builders<ShareRequest>.Filter.Eq(r => r.ServiceTrackId, serviceTrackId)
        );
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

    public async Task<long> GetCompletedDistinctSongCountAsync(CancellationToken cancellationToken = default)
    {
        var pipeline = PipelineDefinition<ShareRequest, BsonDocument>.Create([
            .. DistinctCompletedPipeline(),
            new BsonDocument("$count", "count")]);
        var result = await _requests.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(cancellationToken);
        return result is not null && result.TryGetValue("count", out var count) && count.IsNumeric ? count.ToInt64() : 0;
    }

    public async Task<IReadOnlyList<CompletedShareRequest>> GetRecentCompletedDistinctAsync(int maximum, CancellationToken cancellationToken = default)
    {
        if (maximum <= 0) return [];
        var pipeline = PipelineDefinition<ShareRequest, BsonDocument>.Create([
            .. DistinctCompletedPipeline(),
            new BsonDocument("$sort", new BsonDocument { { "createdAt", -1 }, { "shareId", -1 } }),
            new BsonDocument("$limit", maximum)]);
        var rows = await _requests.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return MaterializeCompletedRequests(rows);
    }

    internal static BsonDocument[] DistinctCompletedPipeline()
    {
        var stages = new BsonDocument[]
        {
            new("$match", new BsonDocument { { "status", ShareStatus.Completed.ToString() }, { "songId", new BsonDocument("$type", "objectId") },
                { "sourceService", new BsonDocument("$in", new BsonArray(PublicSourceServices())) } }),
            new("$sort", new BsonDocument { { "createdAt", -1 }, { "shareId", -1 } }),
            new("$group", new BsonDocument { { "_id", "$songId" }, { "songId", new BsonDocument("$first", "$songId") }, { "shareId", new BsonDocument("$first", "$shareId") }, { "sourceService", new BsonDocument("$first", "$sourceService") }, { "createdAt", new BsonDocument("$first", "$createdAt") } })
        };
        return stages;
    }

    internal static IReadOnlyList<CompletedShareRequest> MaterializeCompletedRequests(IEnumerable<BsonDocument> rows) =>
        rows.Where(x => x.TryGetValue("songId", out var songId) && songId.IsObjectId
                        && x.TryGetValue("shareId", out var shareId) && shareId.IsString
                        && x.TryGetValue("sourceService", out var sourceService) && sourceService.IsString
                        && x.TryGetValue("createdAt", out var createdAt) && createdAt.IsValidDateTime
                        && IsPublicSourceService(sourceService.AsString, out _))
            .Select(x => new CompletedShareRequest(
            x["songId"].AsObjectId.ToString(), x["shareId"].AsString,
            Enum.Parse<ServiceType>(x["sourceService"].AsString), x["createdAt"].ToUniversalTime())).ToList();

    private static IEnumerable<string> PublicSourceServices() => Enum.GetValues<ServiceType>()
        .Where(service => service != ServiceType.Unknown).Select(service => service.ToString());

    private static bool IsPublicSourceService(string value, out ServiceType service) =>
        Enum.TryParse(value, out service) && service != ServiceType.Unknown && Enum.IsDefined(service);
}
