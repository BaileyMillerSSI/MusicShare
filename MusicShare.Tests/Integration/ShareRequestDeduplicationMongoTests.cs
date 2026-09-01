using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MusicShare.Contracts;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Tests.Integration;

/// <summary>
/// Exercises the standalone-Mongo correctness boundary. CI supplies MongoDB through
/// MUSICSHARE_TEST_MONGODB; developers get the same cases via Mongo2Go fallback.
/// </summary>
public sealed class ShareRequestDeduplicationMongoTests : IAsyncLifetime
{
    private static readonly object GuidLock = new();
    private static bool guidConfigured;
    private MongoDbRunner? runner;
    private IMongoDatabase database = null!;

    public ValueTask InitializeAsync()
    {
        lock (GuidLock)
        {
            if (!guidConfigured)
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
                guidConfigured = true;
            }
        }
        var connectionString = Environment.GetEnvironmentVariable("MUSICSHARE_TEST_MONGODB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            runner = MongoDbRunner.Start(singleNodeReplSet: false);
            connectionString = runner.ConnectionString;
        }
        database = new MongoClient(connectionString).GetDatabase($"dedupe-{Guid.NewGuid():N}");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() { runner?.Dispose(); return ValueTask.CompletedTask; }

    [Fact]
    public async Task ItWillReserveOneWinnerAndPreserveHistoricalKeylessRowsUnderTheSparseIndex()
    {
        var context = new TestDbContext(database);
        var repo = new ShareRequestRepository(context);
        await context.ShareRequests.InsertOneAsync(Request("historic", null));
        await context.ShareRequests.Indexes.CreateOneAsync(new CreateIndexModel<ShareRequest>(
            Builders<ShareRequest>.IndexKeys.Ascending(x => x.SourceIdentityKey),
            new CreateIndexOptions { Name = "share_request_source_identity_unique", Unique = true, Sparse = true }));

        var reservations = await Task.WhenAll(Enumerable.Range(0, 24).Select(i =>
            repo.ReserveBySourceIdentityAsync(Request($"share{i:x8}"[..12], "v1:1:track"))));
        reservations.Count(x => x.Inserted).Should().Be(1);
        reservations.Select(x => x.Request.ShareId).Distinct().Should().ContainSingle();
        (await context.ShareRequests.CountDocumentsAsync(Builders<ShareRequest>.Filter.Eq(x => x.SourceIdentityKey, "v1:1:track"))).Should().Be(1);
    }

    [Fact]
    public async Task ItWillFenceOpposingAndOverlappingClaimsAndRejectStaleTakeoverWrites()
    {
        var context = new TestDbContext(database);
        var repo = new ShareRequestRepository(context);
        var canonicalSong = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        var aliasSong = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        var canonical = Completed("aaaaaaaaaaaa", canonicalSong);
        var aliasRequest = Completed("bbbbbbbbbbbb", aliasSong);
        await context.ShareRequests.InsertManyAsync([canonical, aliasRequest]);
        await context.Songs.InsertManyAsync([ResolvedSong(canonicalSong), ResolvedSong(aliasSong)]);
        await context.SongServiceLinks.InsertManyAsync([Link(canonicalSong, "track"), Link(aliasSong, "track")]);
        canonical = (await repo.GetByShareIdAsync(canonical.ShareId))!;
        aliasRequest = (await repo.GetByShareIdAsync(aliasRequest.ShareId))!;
        var snapshot = ReconciliationSnapshots.TryCreate(canonical, aliasRequest,
            await context.Songs.Find(Builders<Song>.Filter.In(x => x.Id, new[] { canonicalSong, aliasSong })).ToListAsync(),
            await context.SongServiceLinks.Find(Builders<SongServiceLink>.Filter.In(x => x.SongId, new[] { canonicalSong, aliasSong })).ToListAsync(),
            [canonical, aliasRequest], canonical.ReconciliationClaimVersion, aliasRequest.ReconciliationClaimVersion)!;
        var write = new ReconciliationWrite("aaaaaaaaaaaa", "bbbbbbbbbbbb", "op", snapshot.Fingerprint, canonicalSong, aliasSong, ShareStatus.Completed, ShareStatus.Completed, canonical.CreatedAt, aliasRequest.CreatedAt, null, snapshot.CanonicalPreClaimVersion, snapshot.AliasPreClaimVersion);

        // An unexpired first claim represents a crash/other overlapping operation. The second
        // operation must not acquire the pair or mutate either row; expiry makes recovery safe.
        await context.ShareRequests.UpdateOneAsync(x => x.ShareId == "aaaaaaaaaaaa", Builders<ShareRequest>.Update
            .Set(x => x.ReconciliationClaimToken, "other").Set(x => x.ReconciliationClaimExpiresAt, DateTime.UtcNow.AddMinutes(1)));
        (await repo.TryReconcileAsync(write)).Succeeded.Should().BeFalse();
        await context.ShareRequests.UpdateOneAsync(x => x.ShareId == "aaaaaaaaaaaa", Builders<ShareRequest>.Update
            .Set(x => x.ReconciliationClaimExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        (await repo.TryReconcileAsync(write)).Succeeded.Should().BeTrue();
        var alias = await repo.GetByShareIdAsync("bbbbbbbbbbbb");
        alias!.CanonicalShareId.Should().Be("aaaaaaaaaaaa");
        (await repo.TryReconcileAsync(write)).Changed.Should().BeFalse("apply is idempotent after crash recovery");
    }

    [Fact]
    public async Task ItWillRejectEveryStaleTokenMutationAfterAnExpiredClaimIsTakenOver()
    {
        var context = new TestDbContext(database);
        var canonical = Completed("aaaaaaaaaaaa", MongoDB.Bson.ObjectId.GenerateNewId().ToString());
        var alias = Completed("bbbbbbbbbbbb", MongoDB.Bson.ObjectId.GenerateNewId().ToString());
        await context.ShareRequests.InsertManyAsync([canonical, alias]);

        // This is the exact claim lifecycle used by ShareRequestRepository: a crashed holder's
        // expired token is replaced atomically, and every backfill/CAS/release filters on it.
        const string expiredClaimToken = "old-token";
        const string activeClaimToken = "new-token";
        await context.ShareRequests.UpdateManyAsync(
            x => x.ShareId == canonical.ShareId || x.ShareId == alias.ShareId,
            Builders<ShareRequest>.Update.Set(x => x.ReconciliationClaimToken, expiredClaimToken)
                .Set(x => x.ReconciliationClaimExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        foreach (var id in new[] { canonical.ShareId, alias.ShareId })
        {
            var takeover = await context.ShareRequests.UpdateOneAsync(
                Builders<ShareRequest>.Filter.And(
                    Builders<ShareRequest>.Filter.Eq(x => x.ShareId, id),
                    Builders<ShareRequest>.Filter.Lte(x => x.ReconciliationClaimExpiresAt, DateTime.UtcNow)),
                Builders<ShareRequest>.Update.Set(x => x.ReconciliationClaimToken, activeClaimToken)
                    .Set(x => x.ReconciliationClaimExpiresAt, DateTime.UtcNow.AddMinutes(2)));
            takeover.ModifiedCount.Should().Be(1);
        }

        var staleBackfill = await context.ShareRequests.UpdateOneAsync(
            Builders<ShareRequest>.Filter.And(
                Builders<ShareRequest>.Filter.Eq(x => x.ShareId, canonical.ShareId),
                Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, expiredClaimToken),
                Builders<ShareRequest>.Filter.Eq(x => x.SourceIdentityKey, null)),
            Builders<ShareRequest>.Update.Set(x => x.SourceIdentityKey, "v1:1:stale"));
        var staleAliasCas = await context.ShareRequests.UpdateOneAsync(
            Builders<ShareRequest>.Filter.And(
                Builders<ShareRequest>.Filter.Eq(x => x.ShareId, alias.ShareId),
                Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, expiredClaimToken),
                Builders<ShareRequest>.Filter.Eq(x => x.CanonicalShareId, null)),
            Builders<ShareRequest>.Update.Set(x => x.CanonicalShareId, canonical.ShareId));
        var staleRelease = await context.ShareRequests.UpdateManyAsync(
            Builders<ShareRequest>.Filter.And(
                Builders<ShareRequest>.Filter.In(x => x.ShareId, new[] { canonical.ShareId, alias.ShareId }),
                Builders<ShareRequest>.Filter.Eq(x => x.ReconciliationClaimToken, expiredClaimToken)),
            Builders<ShareRequest>.Update.Unset(x => x.ReconciliationClaimToken).Unset(x => x.ReconciliationClaimExpiresAt));

        staleBackfill.ModifiedCount.Should().Be(0);
        staleAliasCas.ModifiedCount.Should().Be(0);
        staleRelease.ModifiedCount.Should().Be(0);
        var rows = await context.ShareRequests.Find(x => x.ShareId == canonical.ShareId || x.ShareId == alias.ShareId).ToListAsync();
        rows.Should().OnlyContain(x => x.ReconciliationClaimToken == activeClaimToken);
        rows.Should().OnlyContain(x => x.CanonicalShareId == null);
    }

    private static ShareRequest Request(string id, string? key) => new()
    {
        Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(), ShareId = id, SourceUrl = "https://example.test", SourceService = ServiceType.Spotify,
        CorrelationId = Guid.NewGuid(), SourceIdentityKey = key
    };

    private static ShareRequest Completed(string shareId, string songId) => new()
    {
        Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(), ShareId = shareId, SongId = songId, SourceUrl = "https://example.test",
        SourceService = ServiceType.Spotify, CorrelationId = Guid.NewGuid(), Status = ShareStatus.Completed, CreatedAt = DateTime.UtcNow
    };

    private static Song ResolvedSong(string id) => new() { Id = id, Status = SongStatus.Resolved, CreatedAt = DateTime.UnixEpoch, UpdatedAt = DateTime.UnixEpoch };
    private static SongServiceLink Link(string songId, string identity) => new() { Id = ObjectId.GenerateNewId().ToString(), SongId = songId, ServiceType = ServiceType.Spotify, ServiceSongId = identity, OriginalUrl = "https://example.test", NormalizedUrl = "https://example.test", CreatedAt = DateTime.UnixEpoch };

    private sealed class TestDbContext(IMongoDatabase db) : IMusicShareDbContext
    {
        public IMongoDatabase Database => db;
        public IMongoCollection<Song> Songs => db.GetCollection<Song>("songs");
        public IMongoCollection<ShareRequest> ShareRequests => db.GetCollection<ShareRequest>("shareRequests");
        public IMongoCollection<SongServiceLink> SongServiceLinks => db.GetCollection<SongServiceLink>("songServiceLinks");
        public IMongoCollection<WorkflowState> WorkflowStates => db.GetCollection<WorkflowState>("workflowStates");
        public IMongoCollection<PublicMetricsSnapshot> PublicMetricsSnapshots => db.GetCollection<PublicMetricsSnapshot>("snapshots");
    }
}
