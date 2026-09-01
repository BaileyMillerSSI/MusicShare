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

    public async Task<ShareRequest?> GetBySourceIdentityKeyAsync(string sourceIdentityKey, CancellationToken cancellationToken = default) =>
        await _requests.Find(Builders<ShareRequest>.Filter.Eq(x => x.SourceIdentityKey, sourceIdentityKey))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ShareReservation> ReserveBySourceIdentityAsync(ShareRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceIdentityKey))
        {
            await InsertAsync(request, cancellationToken);
            return new ShareReservation(request, true);
        }

        request.CreatedAt = DateTime.UtcNow;
        try
        {
            await _requests.InsertOneAsync(request, cancellationToken: cancellationToken);
            return new ShareReservation(request, true);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var winner = await GetBySourceIdentityKeyAsync(request.SourceIdentityKey, cancellationToken);
            if (winner is null) throw;
            return new ShareReservation(winner, false);
        }
    }

    public async Task<ShareRequest?> ResolveCanonicalAsync(ShareRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CanonicalShareId)) return request;
        var canonical = await GetByShareIdAsync(request.CanonicalShareId, cancellationToken);
        return canonical is null || !string.IsNullOrWhiteSpace(canonical.CanonicalShareId) ? null : canonical;
    }

    public async Task<IReadOnlyList<ShareRequest>> GetByShareIdsAsync(IReadOnlyCollection<string> shareIds, CancellationToken cancellationToken = default)
    {
        if (shareIds.Count != 2) return [];
        return await _requests.Find(Builders<ShareRequest>.Filter.In(x => x.ShareId, shareIds)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShareRequest>> GetBySongIdsAsync(IReadOnlyCollection<string> songIds, CancellationToken cancellationToken = default)
    {
        if (songIds.Count == 0) return [];
        return await _requests.Find(Builders<ShareRequest>.Filter.In(x => x.SongId, songIds)).ToListAsync(cancellationToken);
    }

    public async Task<ReconciliationWriteResult> TryReconcileAsync(ReconciliationWrite write, CancellationToken cancellationToken = default)
    {
        // This works on standalone Mongo: claims are per share and fenced by a random token.
        // A claimant takes IDs in ascending order to avoid opposing-pair deadlocks. Every read,
        // durable write and release below includes that token, preventing stale lease holders
        // from writing after an expiry/takeover.
        var token = Guid.NewGuid().ToString("N");
        var ordered = new[] { write.CanonicalShareId, write.AliasShareId }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var first = await TryClaimAsync(ordered[0], token, cancellationToken);
        if (first is null) return new(false, false, "Another duplicate-share reconciliation is in progress. Retry the dry-run.");
        var second = await TryClaimAsync(ordered[1], token, cancellationToken);
        if (second is null)
        {
            await ReleaseClaimAsync(ordered[0], token, cancellationToken);
            return new(false, false, "Another duplicate-share reconciliation is in progress. Retry the dry-run.");
        }
        try
        {
        var canonical = await GetClaimedAsync(write.CanonicalShareId, token, cancellationToken);
        var alias = await GetClaimedAsync(write.AliasShareId, token, cancellationToken);
        if (canonical is null || alias is null || canonical.ShareId == alias.ShareId ||
            canonical.Status != write.CanonicalStatus || alias.Status != write.AliasStatus ||
            canonical.Status != ShareStatus.Completed || alias.Status != ShareStatus.Completed ||
            canonical.SongId != write.CanonicalSongId || alias.SongId != write.AliasSongId ||
            !string.IsNullOrEmpty(canonical.CanonicalShareId))
            return new(false, false, "The requested shares are no longer eligible for reconciliation.");

        if (alias.CanonicalShareId == canonical.ShareId && alias.ReconciliationId == write.ReconciliationId && alias.ReconciliationFingerprint == write.Fingerprint)
            return new(true, false);
        if (!string.IsNullOrEmpty(alias.CanonicalShareId))
            return new(false, false, "The alias is already reconciled.");
        if (canonical.CreatedAt != write.CanonicalCreatedAt || alias.CreatedAt != write.AliasCreatedAt)
            return new(false, false, "The reconciliation plan is stale.");

        // Backfill is a separately fenced idempotent write. If it crashes before the alias
        // CAS, retrying the same exact pair safely observes the same canonical identity.
        if (!string.IsNullOrWhiteSpace(write.CanonicalSourceIdentityKey) && string.IsNullOrWhiteSpace(canonical.SourceIdentityKey))
        {
            var backfill = Builders<ShareRequest>.Filter.And(
                Builders<ShareRequest>.Filter.Eq(x => x.ShareId, canonical.ShareId),
                Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, token),
                Builders<ShareRequest>.Filter.Eq(x => x.SourceIdentityKey, null));
            try
            {
                await _requests.UpdateOneAsync(backfill, Builders<ShareRequest>.Update.Set(x => x.SourceIdentityKey, write.CanonicalSourceIdentityKey), cancellationToken: cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return new(false, false, "The canonical source identity is owned by another request.");
            }
        }

        var filter = Builders<ShareRequest>.Filter.And(
            Builders<ShareRequest>.Filter.Eq(x => x.ShareId, alias.ShareId),
            Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, token),
            Builders<ShareRequest>.Filter.Eq(x => x.CanonicalShareId, null),
            Builders<ShareRequest>.Filter.Eq(x => x.CreatedAt, write.AliasCreatedAt),
            Builders<ShareRequest>.Filter.Eq(x => x.SongId, write.AliasSongId),
            Builders<ShareRequest>.Filter.Eq(x => x.Status, write.AliasStatus));
        var update = Builders<ShareRequest>.Update
            .Set(x => x.CanonicalShareId, canonical.ShareId)
            .Set(x => x.ReconciledAt, DateTime.UtcNow)
            .Set(x => x.ReconciliationId, write.ReconciliationId)
            .Set(x => x.ReconciliationFingerprint, write.Fingerprint);
        var result = await _requests.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        if (result.ModifiedCount != 1) return new(false, false, "The reconciliation plan is stale.");
        return new(true, true);
        }
        finally
        {
            await ReleaseClaimAsync(ordered[1], token, cancellationToken);
            await ReleaseClaimAsync(ordered[0], token, cancellationToken);
        }
    }

    private async Task<ShareRequest?> TryClaimAsync(string shareId, string token, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<ShareRequest>.Filter.And(
            Builders<ShareRequest>.Filter.Eq(x => x.ShareId, shareId),
            Builders<ShareRequest>.Filter.Or(
                Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, null),
                Builders<ShareRequest>.Filter.Lte(x => x.ReconciliationClaimExpiresAt, now)));
        var update = Builders<ShareRequest>.Update
            .Set(x => x.ReconciliationClaimToken, token)
            .Set(x => x.ReconciliationClaimExpiresAt, now.AddMinutes(2))
            .Inc(x => x.ReconciliationClaimVersion, 1);
        return await _requests.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<ShareRequest> { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }

    private Task<ShareRequest?> GetClaimedAsync(string shareId, string token, CancellationToken cancellationToken) =>
        _requests.Find(Builders<ShareRequest>.Filter.And(
            Builders<ShareRequest>.Filter.Eq(x => x.ShareId, shareId),
            Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, token)))
            .FirstOrDefaultAsync(cancellationToken);

    private Task ReleaseClaimAsync(string shareId, string token, CancellationToken cancellationToken) =>
        _requests.UpdateOneAsync(Builders<ShareRequest>.Filter.And(
                Builders<ShareRequest>.Filter.Eq(x => x.ShareId, shareId),
                Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, token)),
            Builders<ShareRequest>.Update.Unset(x => x.ReconciliationClaimToken).Unset(x => x.ReconciliationClaimExpiresAt), cancellationToken: cancellationToken);

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

    public async Task<IReadOnlyList<WeeklyCompletedSongCount>> GetCompletedDistinctSongCountsByWeekAsync(
        DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default)
    {
        if (rangeStartUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The range start must be UTC.", nameof(rangeStartUtc));
        if (rangeEndUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The range end must be UTC.", nameof(rangeEndUtc));
        if (rangeStartUtc >= rangeEndUtc) throw new ArgumentException("The range end must be after the range start.", nameof(rangeEndUtc));

        var pipeline = PipelineDefinition<ShareRequest, BsonDocument>.Create([
            .. CanonicalCompletedPipeline(_songs.CollectionNamespace.CollectionName),
            new BsonDocument("$match", new BsonDocument("createdAt", new BsonDocument { { "$gte", rangeStartUtc }, { "$lt", rangeEndUtc } })),
            new BsonDocument("$group", new BsonDocument { { "_id", new BsonDocument("$dateTrunc", new BsonDocument { { "date", "$createdAt" }, { "unit", "week" }, { "timezone", "UTC" }, { "startOfWeek", "Sunday" } }) }, { "count", new BsonDocument("$sum", 1) } }),
            new BsonDocument("$project", new BsonDocument { { "_id", 0 }, { "weekStart", "$_id" }, { "count", 1 } }),
            new BsonDocument("$sort", new BsonDocument("weekStart", 1))]);
        try
        {
            var rows = await _requests.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
            return MaterializeWeeklyCompletedSongCounts(rows);
        }
        catch (MongoCommandException exception) when (exception.Message.Contains("$dateTrunc", StringComparison.Ordinal))
        {
            var fallbackPipeline = PipelineDefinition<ShareRequest, BsonDocument>.Create(CanonicalCompletedPipeline(_songs.CollectionNamespace.CollectionName));
            var fallbackRows = await _requests.Aggregate<BsonDocument>(fallbackPipeline).ToListAsync(cancellationToken);
            return fallbackRows.Where(x => x.TryGetValue("createdAt", out var createdAt) && createdAt.IsValidDateTime)
                .Select(x => x["createdAt"].ToUniversalTime())
                .Where(x => x >= rangeStartUtc && x < rangeEndUtc)
                .GroupBy(GetSundayStartUtc)
                .Select(x => new WeeklyCompletedSongCount(x.Key, x.LongCount()))
                .OrderBy(x => x.WeekStart).ToList();
        }
    }

    internal static BsonDocument[] DistinctCompletedPipeline()
    {
        var stages = new BsonDocument[]
        {
            new("$match", new BsonDocument { { "status", ShareStatus.Completed.ToString() }, { "songId", new BsonDocument("$type", "objectId") },
                { "sourceService", new BsonDocument("$in", new BsonArray(PublicSourceServices())) },
                { "canonicalShareId", new BsonDocument("$exists", false) } }),
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

    internal static IReadOnlyList<WeeklyCompletedSongCount> MaterializeWeeklyCompletedSongCounts(IEnumerable<BsonDocument> rows) =>
        rows.Select(row =>
        {
            if (!row.TryGetValue("weekStart", out var weekStart) || !row.TryGetValue("count", out var count) || !count.IsNumeric || count.ToInt64() < 0)
                return null;
            try { return new WeeklyCompletedSongCount(weekStart.ToUniversalTime(), count.ToInt64()); }
            catch (Exception exception) when (exception is InvalidCastException or NotSupportedException) { return null; }
        }).OfType<WeeklyCompletedSongCount>().OrderBy(x => x.WeekStart).ToList();

    private static DateTime GetSundayStartUtc(DateTime utcTimestamp) => utcTimestamp.Date.AddDays(-(int)utcTimestamp.DayOfWeek);

    private static IEnumerable<string> PublicSourceServices() => Enum.GetValues<ServiceType>()
        .Where(service => service != ServiceType.Unknown).Select(service => service.ToString());

    private static bool IsPublicSourceService(string value, out ServiceType service) =>
        Enum.TryParse(value, out service) && service != ServiceType.Unknown && Enum.IsDefined(service);
}
