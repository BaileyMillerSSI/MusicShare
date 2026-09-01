using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;

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
        await new ShareIdentityIndexInitializer(context).StartAsync(CancellationToken.None);
        var index = (await context.ShareRequests.Indexes.ListAsync()).ToList().Single(x => x["name"] == ShareIdentityIndexInitializer.IndexName);
        index["unique"].ToBoolean().Should().BeTrue();
        index["sparse"].ToBoolean().Should().BeTrue();

        var reservations = await Task.WhenAll(Enumerable.Range(0, 24).Select(i =>
            repo.ReserveBySourceIdentityAsync(Request($"share{i:x8}"[..12], "v1:1:track"))));
        reservations.Count(x => x.Inserted).Should().Be(1);
        reservations.Select(x => x.Request.ShareId).Distinct().Should().ContainSingle();
        (await context.ShareRequests.CountDocumentsAsync(Builders<ShareRequest>.Filter.Eq(x => x.SourceIdentityKey, "v1:1:track"))).Should().Be(1);
    }

    [Fact]
    public async Task ItWillFailStartupWhenExistingKeyedRowsWouldViolateTheCorrectnessIndex()
    {
        var context = new TestDbContext(database);
        await context.ShareRequests.InsertManyAsync([Request("aaaaaaaaaaaa", "v1:1:duplicate"), Request("bbbbbbbbbbbb", "v1:1:duplicate")]);

        var start = () => new ShareIdentityIndexInitializer(context).StartAsync(CancellationToken.None);

        await start.Should().ThrowAsync<MongoCommandException>();
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

    [Theory]
    [InlineData("provider-link")]
    [InlineData("resolved-song")]
    [InlineData("third-owner")]
    [InlineData("share-request")]
    public async Task ItWillRejectEachPersistedStaleStateBeforeBackfillOrAliasCas(string mutation)
    {
        var context = new TestDbContext(database);
        var repo = new ShareRequestRepository(context);
        var canonicalSong = ObjectId.GenerateNewId().ToString();
        var aliasSong = ObjectId.GenerateNewId().ToString();
        var canonical = Completed("aaaaaaaaaaaa", canonicalSong);
        var alias = Completed("bbbbbbbbbbbb", aliasSong);
        await context.ShareRequests.InsertManyAsync([canonical, alias]);
        await context.Songs.InsertManyAsync([ResolvedSong(canonicalSong), ResolvedSong(aliasSong)]);
        await context.SongServiceLinks.InsertManyAsync([Link(canonicalSong, "track"), Link(aliasSong, "track")]);
        var snapshot = ReconciliationSnapshots.TryCreate(canonical, alias,
            await context.Songs.Find(Builders<Song>.Filter.Empty).ToListAsync(),
            await context.SongServiceLinks.Find(Builders<SongServiceLink>.Filter.Empty).ToListAsync(), [canonical, alias], 0, 0)!;
        var write = new ReconciliationWrite(canonical.ShareId, alias.ShareId, $"reconcile-{snapshot.Fingerprint}", snapshot.Fingerprint,
            canonicalSong, aliasSong, ShareStatus.Completed, ShareStatus.Completed, canonical.CreatedAt, alias.CreatedAt, "v1:1:track", 0, 0);

        switch (mutation)
        {
            case "provider-link":
                await context.SongServiceLinks.InsertOneAsync(Link(aliasSong, "different-track"));
                break;
            case "resolved-song":
                await context.Songs.UpdateOneAsync(x => x.Id == aliasSong, Builders<Song>.Update.Set(x => x.Status, SongStatus.Pending));
                break;
            case "third-owner":
                var thirdSong = ObjectId.GenerateNewId().ToString();
                await context.Songs.InsertOneAsync(ResolvedSong(thirdSong));
                await context.ShareRequests.InsertOneAsync(Completed("cccccccccccc", thirdSong));
                await context.SongServiceLinks.InsertOneAsync(Link(thirdSong, "track"));
                break;
            case "share-request":
                await context.ShareRequests.UpdateOneAsync(x => x.ShareId == alias.ShareId, Builders<ShareRequest>.Update.Inc(x => x.ReconciliationClaimVersion, 1));
                break;
        }

        var result = await repo.TryReconcileAsync(write);

        result.Succeeded.Should().BeFalse(mutation);
        var persistedCanonical = await repo.GetByShareIdAsync(canonical.ShareId);
        var persistedAlias = await repo.GetByShareIdAsync(alias.ShareId);
        persistedCanonical!.SourceIdentityKey.Should().BeNull("stale evidence cannot backfill the source key");
        persistedAlias!.CanonicalShareId.Should().BeNull("stale evidence cannot write an alias");
    }

    [Fact]
    public async Task ItWillApplyAndReturnTheStoredBoundedContractOnAnEndToEndRetry()
    {
        var context = new TestDbContext(database);
        var canonical = Completed("aaaaaaaaaaaa", ObjectId.GenerateNewId().ToString());
        var alias = Completed("bbbbbbbbbbbb", ObjectId.GenerateNewId().ToString());
        await context.ShareRequests.InsertManyAsync([canonical, alias]);
        await context.Songs.InsertManyAsync([ResolvedSong(canonical.SongId!), ResolvedSong(alias.SongId!)]);
        await context.SongServiceLinks.InsertManyAsync([Link(canonical.SongId!, "track"), Link(alias.SongId!, "track")]);
        var service = new DuplicateShareReconciliationService(new ShareRequestRepository(context), new SongServiceLinkRepository(context), new SongRepository(context), NullLogger<DuplicateShareReconciliationService>.Instance);

        var dryRun = await service.ReconcileAsync(new(canonical.ShareId, alias.ShareId, null, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);
        var apply = await service.ReconcileAsync(new(canonical.ShareId, alias.ShareId, null, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);
        var retry = await service.ReconcileAsync(new(canonical.ShareId, alias.ShareId, null, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);

        apply.Success.Should().BeTrue();
        apply.Changed.Should().BeTrue();
        retry.Success.Should().BeTrue();
        retry.Changed.Should().BeFalse();
        retry.OperationId.Should().Be(apply.OperationId);
        retry.Fingerprint.Should().Be(dryRun.Fingerprint);
        retry.CanonicalShareId.Should().Be(canonical.ShareId);
        retry.AliasShareId.Should().Be(alias.ShareId);
        retry.SharedIdentities.Should().ContainSingle().Which.ServiceSongId.Should().Be("track");
    }

    [Fact]
    public async Task ItWillAllowAtMostOneOpposingReconciliationAndNeverCreateACycle()
    {
        var context = new TestDbContext(database);
        var repo = new ShareRequestRepository(context);
        var (first, second) = await InsertPairAsync(context);
        var firstWrite = await WriteAsync(context, first, second);
        var opposingWrite = await WriteAsync(context, second, first);

        var results = await Task.WhenAll(repo.TryReconcileAsync(firstWrite), repo.TryReconcileAsync(opposingWrite));

        results.Count(x => x.Succeeded && x.Changed).Should().Be(1);
        var rows = await context.ShareRequests.Find(Builders<ShareRequest>.Filter.In(x => x.ShareId, [first.ShareId, second.ShareId])).ToListAsync();
        rows.Should().NotContain(x => x.CanonicalShareId == x.ShareId);
        rows.Count(x => !string.IsNullOrWhiteSpace(x.CanonicalShareId)).Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public async Task ItWillAllowAtMostOneOverlappingReconciliationAndNeverCreateAnAliasChain()
    {
        var context = new TestDbContext(database);
        var repo = new ShareRequestRepository(context);
        var first = Completed("aaaaaaaaaaaa", ObjectId.GenerateNewId().ToString());
        var second = Completed("bbbbbbbbbbbb", ObjectId.GenerateNewId().ToString());
        var third = Completed("cccccccccccc", ObjectId.GenerateNewId().ToString());
        await context.ShareRequests.InsertManyAsync([first, second, third]);
        await context.Songs.InsertManyAsync([ResolvedSong(first.SongId!), ResolvedSong(second.SongId!), ResolvedSong(third.SongId!)]);
        // A/B and B/C are each independently proven by a different exact identity.
        await context.SongServiceLinks.InsertManyAsync([
            Link(first.SongId!, "spotify-x", ServiceType.Spotify),
            Link(second.SongId!, "spotify-x", ServiceType.Spotify),
            Link(second.SongId!, "youtube-y", ServiceType.YouTubeMusic),
            Link(third.SongId!, "youtube-y", ServiceType.YouTubeMusic)]);
        var firstWrite = await WriteAsync(context, first, second);
        var overlappingWrite = await WriteAsync(context, second, third);

        var results = await Task.WhenAll(repo.TryReconcileAsync(firstWrite), repo.TryReconcileAsync(overlappingWrite));

        results.Count(x => x.Succeeded && x.Changed).Should().Be(1);
        var rows = await context.ShareRequests.Find(Builders<ShareRequest>.Filter.In(x => x.ShareId, [first.ShareId, second.ShareId, third.ShareId])).ToListAsync();
        rows.Should().OnlyContain(row => row.CanonicalShareId == null || rows.Single(x => x.ShareId == row.CanonicalShareId).CanonicalShareId == null);
    }

    private static async Task<(ShareRequest First, ShareRequest Second)> InsertPairAsync(TestDbContext context)
    {
        var first = Completed("aaaaaaaaaaaa", ObjectId.GenerateNewId().ToString());
        var second = Completed("bbbbbbbbbbbb", ObjectId.GenerateNewId().ToString());
        await context.ShareRequests.InsertManyAsync([first, second]);
        await context.Songs.InsertManyAsync([ResolvedSong(first.SongId!), ResolvedSong(second.SongId!)]);
        await context.SongServiceLinks.InsertManyAsync([Link(first.SongId!, "track"), Link(second.SongId!, "track")]);
        return (first, second);
    }

    private static async Task<ReconciliationWrite> WriteAsync(TestDbContext context, ShareRequest canonical, ShareRequest alias)
    {
        // Mongo stores DateTime at millisecond precision; build the dry-run equivalent
        // from persisted requests so the repository's later CAS sees the exact values.
        var persisted = await context.ShareRequests.Find(Builders<ShareRequest>.Filter.In(x => x.ShareId, [canonical.ShareId, alias.ShareId])).ToListAsync();
        canonical = persisted.Single(x => x.ShareId == canonical.ShareId);
        alias = persisted.Single(x => x.ShareId == alias.ShareId);
        var songs = await context.Songs.Find(Builders<Song>.Filter.In(x => x.Id, [canonical.SongId!, alias.SongId!])).ToListAsync();
        var links = await context.SongServiceLinks.Find(Builders<SongServiceLink>.Filter.In(x => x.SongId, [canonical.SongId!, alias.SongId!])).ToListAsync();
        var snapshot = ReconciliationSnapshots.TryCreate(canonical, alias, songs, links, [canonical, alias], 0, 0)!;
        return new(canonical.ShareId, alias.ShareId, $"reconcile-{snapshot.Fingerprint}", snapshot.Fingerprint, canonical.SongId!, alias.SongId!, ShareStatus.Completed, ShareStatus.Completed, canonical.CreatedAt, alias.CreatedAt, null, 0, 0);
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
    private static SongServiceLink Link(string songId, string identity, ServiceType serviceType = ServiceType.Spotify) => new() { Id = ObjectId.GenerateNewId().ToString(), SongId = songId, ServiceType = serviceType, ServiceSongId = identity, OriginalUrl = "https://example.test", NormalizedUrl = "https://example.test", CreatedAt = DateTime.UnixEpoch };

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
