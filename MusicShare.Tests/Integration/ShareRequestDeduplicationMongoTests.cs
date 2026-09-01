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
        canonical = (await repo.GetByShareIdAsync(canonical.ShareId))!;
        aliasRequest = (await repo.GetByShareIdAsync(aliasRequest.ShareId))!;
        var write = new ReconciliationWrite("aaaaaaaaaaaa", "bbbbbbbbbbbb", "op", "fingerprint", canonicalSong, aliasSong, ShareStatus.Completed, ShareStatus.Completed, canonical.CreatedAt, aliasRequest.CreatedAt);

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
