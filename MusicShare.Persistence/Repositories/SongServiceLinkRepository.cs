using MongoDB.Driver;
using MongoDB.Bson;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class SongServiceLinkRepository(IMusicShareDbContext context) : ISongServiceLinkRepository
{
    private readonly IMongoCollection<SongServiceLink> _links = context.SongServiceLinks;
    private readonly IMongoCollection<ShareRequest> _requests = context.ShareRequests;

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

    public async Task<IReadOnlyList<SongServiceLink>> GetBySongIdsAsync(IReadOnlyCollection<string> songIds, CancellationToken cancellationToken = default)
    {
        if (songIds.Count == 0) return [];
        return await _links.Find(Builders<SongServiceLink>.Filter.In(x => x.SongId, songIds)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<ServiceType, long>> GetCompletedDistinctSongLinkCountsAsync(CancellationToken cancellationToken = default)
    {
        var pipeline = PipelineDefinition<SongServiceLink, BsonDocument>.Create(BuildCompletedDistinctSongLinkCountPipeline(_requests.CollectionNamespace.CollectionName));
        var rows = await _links.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return rows.Where(x => x.TryGetValue("_id", out var service) && x.TryGetValue("count", out var count)
                               && service.IsString && count.IsNumeric && IsDefinedService(service.AsString, out _))
            .ToDictionary(x => Enum.Parse<ServiceType>(x["_id"].AsString), x => x["count"].ToInt64());
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

    internal static BsonDocument[] BuildCompletedDistinctSongLinkCountPipeline(string shareRequestsCollectionName) =>
    [
        new("$match", new BsonDocument
        {
            { "songId", new BsonDocument("$type", "objectId") },
            { "serviceType", new BsonDocument("$in", new BsonArray(PublicServices())) }
        }),
        new("$group", new BsonDocument
        {
            { "_id", new BsonDocument { { "songId", "$songId" }, { "serviceType", "$serviceType" } } },
            { "songId", new BsonDocument("$first", "$songId") },
            { "serviceType", new BsonDocument("$first", "$serviceType") }
        }),
        new("$lookup", new BsonDocument
        {
            { "from", shareRequestsCollectionName },
            { "let", new BsonDocument("songId", "$songId") },
            { "pipeline", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$songId", "$$songId" }),
                        new BsonDocument("$eq", new BsonArray { "$status", ShareStatus.Completed.ToString() }),
                        new BsonDocument("$eq", new BsonArray { new BsonDocument("$type", "$canonicalShareId"), "missing" }),
                        new BsonDocument("$eq", new BsonArray { new BsonDocument("$type", "$songId"), "objectId" }),
                        new BsonDocument("$in", new BsonArray { "$sourceService", new BsonArray(PublicServices()) })
                    })))
                }
            },
            { "as", "completedShares" }
        }),
        new("$match", new BsonDocument("completedShares.0", new BsonDocument("$exists", true))),
        new("$group", new BsonDocument { { "_id", "$serviceType" }, { "count", new BsonDocument("$sum", 1) } })
    ];

    private static IEnumerable<string> PublicServices() => Enum.GetValues<ServiceType>()
        .Where(service => service != ServiceType.Unknown).Select(service => service.ToString());

    private static bool IsDefinedService(string value, out ServiceType service) =>
        Enum.TryParse(value, out service) && service != ServiceType.Unknown && Enum.IsDefined(service);
}
