using MongoDB.Driver;
using MongoDB.Bson;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class ShareRequestRepository(IMusicShareDbContext context) : IShareRequestRepository
{
    private readonly IMongoCollection<ShareRequest> _requests = context.ShareRequests;
    private readonly IMongoCollection<Song> _songs = context.Songs;

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
            .. CanonicalCompletedPipeline(_songs.CollectionNamespace.CollectionName),
            new BsonDocument("$sort", new BsonDocument { { "createdAt", -1 }, { "shareId", -1 } }),
            new BsonDocument("$limit", maximum)]);
        var rows = await _requests.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return MaterializeCompletedRequests(rows);
    }

    public async Task<IReadOnlyList<DailyCompletedSongCount>> GetCompletedDistinctSongCountsByDayAsync(
        DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default)
    {
        if (rangeStartUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The range start must be UTC.", nameof(rangeStartUtc));
        if (rangeEndUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The range end must be UTC.", nameof(rangeEndUtc));
        if (rangeStartUtc >= rangeEndUtc) throw new ArgumentException("The range end must be after the range start.", nameof(rangeEndUtc));

        var pipeline = PipelineDefinition<ShareRequest, BsonDocument>.Create([
            .. CanonicalCompletedPipeline(_songs.CollectionNamespace.CollectionName),
            new BsonDocument("$match", new BsonDocument("createdAt", new BsonDocument { { "$gte", rangeStartUtc }, { "$lt", rangeEndUtc } })),
            new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument("$dateTrunc", new BsonDocument { { "date", "$createdAt" }, { "unit", "day" }, { "timezone", "UTC" } }) }, { "count", new BsonDocument("$sum", 1) } }),
            new BsonDocument("$project", new BsonDocument { { "_id", 0 }, { "dayStart", "$_id" }, { "count", 1 } }),
            new BsonDocument("$sort", new BsonDocument("dayStart", 1))]);
        try
        {
            var rows = await _requests.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
            return MaterializeDailyCompletedSongCounts(rows);
        }
        catch (MongoCommandException exception) when (exception.Message.Contains("$dateTrunc", StringComparison.Ordinal))
        {
            var fallbackPipeline = PipelineDefinition<ShareRequest, BsonDocument>.Create(CanonicalCompletedPipeline(_songs.CollectionNamespace.CollectionName));
            var fallbackRows = await _requests.Aggregate<BsonDocument>(fallbackPipeline).ToListAsync(cancellationToken);
            return fallbackRows.Where(x => x.TryGetValue("createdAt", out var createdAt) && createdAt.IsValidDateTime)
                .Select(x => x["createdAt"].ToUniversalTime())
                .Where(x => x >= rangeStartUtc && x < rangeEndUtc)
                .GroupBy(x => x.Date)
                .Select(x => new DailyCompletedSongCount(x.Key, x.LongCount()))
                .OrderBy(x => x.DayStart).ToList();
        }
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

    internal static BsonDocument[] CanonicalCompletedPipeline(string songsCollectionName) =>
    [
        .. DistinctCompletedPipeline(),
        new("$lookup", new BsonDocument
        {
            { "from", songsCollectionName },
            { "let", new BsonDocument("songId", "$songId") },
            { "pipeline", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$_id", "$$songId" }),
                        new BsonDocument("$eq", new BsonArray { new BsonDocument("$type", "$createdAt"), "date" })
                    })))
                }
            },
            { "as", "canonicalSong" }
        }),
        new("$unwind", "$canonicalSong"),
        new("$project", new BsonDocument
        {
            { "_id", 0 }, { "songId", 1 }, { "shareId", 1 }, { "sourceService", 1 }, { "createdAt", "$canonicalSong.createdAt" }
        })
    ];

    internal static IReadOnlyList<CompletedShareRequest> MaterializeCompletedRequests(IEnumerable<BsonDocument> rows) =>
        rows.Where(x => x.TryGetValue("songId", out var songId) && songId.IsObjectId
                        && x.TryGetValue("shareId", out var shareId) && shareId.IsString
                        && x.TryGetValue("sourceService", out var sourceService) && sourceService.IsString
                        && x.TryGetValue("createdAt", out var createdAt) && createdAt.IsValidDateTime
                        && IsPublicSourceService(sourceService.AsString, out _))
            .Select(x => new CompletedShareRequest(
            x["songId"].AsObjectId.ToString(), x["shareId"].AsString,
            Enum.Parse<ServiceType>(x["sourceService"].AsString), x["createdAt"].ToUniversalTime())).ToList();

    internal static IReadOnlyList<DailyCompletedSongCount> MaterializeDailyCompletedSongCounts(IEnumerable<BsonDocument> rows) =>
        rows.Select(row =>
        {
            if (!row.TryGetValue("dayStart", out var dayStart) || !row.TryGetValue("count", out var count) || !count.IsNumeric || count.ToInt64() < 0)
                return null;
            try { return new DailyCompletedSongCount(dayStart.ToUniversalTime().Date, count.ToInt64()); }
            catch (Exception exception) when (exception is InvalidCastException or NotSupportedException) { return null; }
        }).OfType<DailyCompletedSongCount>().OrderBy(x => x.DayStart).ToList();

    private static IEnumerable<string> PublicSourceServices() => Enum.GetValues<ServiceType>()
        .Where(service => service != ServiceType.Unknown).Select(service => service.ToString());

    private static bool IsPublicSourceService(string value, out ServiceType service) =>
        Enum.TryParse(value, out service) && service != ServiceType.Unknown && Enum.IsDefined(service);
}
