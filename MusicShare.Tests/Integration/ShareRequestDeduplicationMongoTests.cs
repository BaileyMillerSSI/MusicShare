using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using MassTransit;
using Moq;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;

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
    public async Task ItWillConvergeConcurrentServiceCreatesAcrossPendingProcessingAndCompletedStatesWithOnePublication()
    {
        var context = new TestDbContext(database);
        await new ShareIdentityIndexInitializer(context).StartAsync(TestContext.Current.CancellationToken);
        var published = new System.Collections.Concurrent.ConcurrentBag<SongShareSubmitted>();
        var endpoint = new Mock<IPublishEndpoint>(MockBehavior.Loose);
        endpoint.Setup(x => x.Publish(It.IsAny<SongShareSubmitted>(), It.IsAny<CancellationToken>()))
            .Callback<SongShareSubmitted, CancellationToken>((message, _) => published.Add(message))
            .Returns(Task.CompletedTask);
        var adapter = new Mock<IMusicServiceAdapter>(MockBehavior.Strict);
        const string url = "https://open.spotify.com/track/canonical-track";
        adapter.Setup(x => x.ExtractSongId(url)).Returns("canonical-track");
        adapter.Setup(x => x.NormalizeUrl(url)).Returns(url);
        var resolver = new Mock<IMusicServiceResolver>(MockBehavior.Strict);
        resolver.Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapter.Object);
        var requests = new ShareRequestRepository(context);
        var service = new ShareRequestService(endpoint.Object, requests, new SongRepository(context), new SongServiceLinkRepository(context), resolver.Object);

        var initial = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => service.Create(url, ServiceType.Spotify, TestContext.Current.CancellationToken)));
        initial.Distinct(StringComparer.Ordinal).Should().ContainSingle();
        var canonicalId = initial[0];
        (await context.ShareRequests.CountDocumentsAsync(Builders<ShareRequest>.Filter.Empty)).Should().Be(1);
        published.Should().ContainSingle(message => message.ShareId == canonicalId);

        foreach (var state in new[] { ShareStatus.Pending, ShareStatus.Processing, ShareStatus.Completed })
        {
            await context.ShareRequests.UpdateOneAsync(x => x.ShareId == canonicalId, Builders<ShareRequest>.Update.Set(x => x.Status, state));
            if (state == ShareStatus.Completed)
            {
                var winner = (await requests.GetByShareIdAsync(canonicalId))!;
                var songId = ObjectId.GenerateNewId().ToString();
                await context.Songs.InsertOneAsync(ResolvedSong(songId));
                await context.ShareRequests.UpdateOneAsync(x => x.ShareId == canonicalId, Builders<ShareRequest>.Update.Set(x => x.SongId, songId));
                await context.SongServiceLinks.InsertOneAsync(Link(songId, "canonical-track"));
            }
            var repeated = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.Create(url, ServiceType.Spotify, TestContext.Current.CancellationToken)));
            repeated.Should().OnlyContain(id => id == canonicalId, state.ToString());
            published.Should().ContainSingle();
        }
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
        var write = new ReconciliationWrite("aaaaaaaaaaaa", "bbbbbbbbbbbb", "op", snapshot.Fingerprint, canonicalSong, aliasSong, ShareStatus.Completed, ShareStatus.Completed, canonical.CreatedAt, aliasRequest.CreatedAt,
            snapshot.CanonicalSourceService, snapshot.CanonicalServiceTrackId, snapshot.CanonicalSourceIdentityKey, snapshot.CanonicalPreClaimVersion, snapshot.AliasPreClaimVersion);

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
    public async Task ItWillReconcileLegacyBsonWithoutClaimFieldsAndAdvanceTheFencedVersion()
    {
        var context = new TestDbContext(database);
        var canonicalSong = ObjectId.GenerateNewId().ToString();
        var aliasSong = ObjectId.GenerateNewId().ToString();
        var created = DateTime.UtcNow.AddMinutes(-5);
        // These are deliberately raw BSON documents: typed zero values would serialize the
        // new field and fail to exercise production rows created before reconciliation.
        await database.GetCollection<BsonDocument>("shareRequests").InsertManyAsync([
            LegacyCompletedDocument("aaaaaaaaaaaa", canonicalSong, created),
            LegacyCompletedDocument("bbbbbbbbbbbb", aliasSong, created.AddSeconds(1))]);
        await context.Songs.InsertManyAsync([ResolvedSong(canonicalSong), ResolvedSong(aliasSong)]);
        await context.SongServiceLinks.InsertManyAsync([Link(canonicalSong, "track"), Link(aliasSong, "track")]);
        var service = new DuplicateShareReconciliationService(new ShareRequestRepository(context), new SongServiceLinkRepository(context), new SongRepository(context), NullLogger<DuplicateShareReconciliationService>.Instance);

        var dryRun = await service.ReconcileAsync(new("aaaaaaaaaaaa", "bbbbbbbbbbbb", null, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);
        var apply = await service.ReconcileAsync(new("aaaaaaaaaaaa", "bbbbbbbbbbbb", null, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);
        var retry = await service.ReconcileAsync(new("aaaaaaaaaaaa", "bbbbbbbbbbbb", null, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);

        dryRun.Success.Should().BeTrue();
        apply.Should().Match<DuplicateShareReconciliationResult>(x => x.Success && x.Changed);
        retry.Should().Match<DuplicateShareReconciliationResult>(x => x.Success && !x.Changed);
        var persisted = await context.ShareRequests.Find(Builders<ShareRequest>.Filter.In(x => x.ShareId, ["aaaaaaaaaaaa", "bbbbbbbbbbbb"])).ToListAsync();
        persisted.Single(x => x.ShareId == "bbbbbbbbbbbb").CanonicalShareId.Should().Be("aaaaaaaaaaaa");
        persisted.Should().OnlyContain(x => x.ReconciliationClaimVersion >= 1);
        (await database.GetCollection<BsonDocument>("shareRequests").Find(new BsonDocument("shareId", "aaaaaaaaaaaa")).FirstAsync()).Contains("reconciliationClaimVersion").Should().BeTrue();
    }

    [Fact]
    public async Task ItWillRejectPromotingAShareWithIncomingAliasesButAllowItsCanonicalPeer()
    {
        var context = new TestDbContext(database);
        var a = Completed("aaaaaaaaaaaa", ObjectId.GenerateNewId().ToString());
        var b = Completed("bbbbbbbbbbbb", ObjectId.GenerateNewId().ToString());
        var c = Completed("cccccccccccc", ObjectId.GenerateNewId().ToString());
        c.CanonicalShareId = a.ShareId;
        await context.ShareRequests.InsertManyAsync([a, b, c]);
        await context.Songs.InsertManyAsync([ResolvedSong(a.SongId!), ResolvedSong(b.SongId!), ResolvedSong(c.SongId!)]);
        await context.SongServiceLinks.InsertManyAsync([Link(a.SongId!, "track"), Link(b.SongId!, "track")]);
        var service = new DuplicateShareReconciliationService(new ShareRequestRepository(context), new SongServiceLinkRepository(context), new SongRepository(context), NullLogger<DuplicateShareReconciliationService>.Instance);

        var unsafePlan = await service.ReconcileAsync(new(a.ShareId, b.ShareId, b.ShareId, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);
        unsafePlan.Success.Should().BeFalse();
        var untouched = await context.ShareRequests.Find(Builders<ShareRequest>.Filter.In(x => x.ShareId, [a.ShareId, b.ShareId])).ToListAsync();
        untouched.Should().OnlyContain(x => x.SourceIdentityKey == null && x.CanonicalShareId == null && x.ReconciliationId == null && x.ReconciliationFingerprint == null);

        var dryRun = await service.ReconcileAsync(new(a.ShareId, b.ShareId, a.ShareId, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);
        var apply = await service.ReconcileAsync(new(a.ShareId, b.ShareId, a.ShareId, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);
        var retry = await service.ReconcileAsync(new(a.ShareId, b.ShareId, a.ShareId, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);
        apply.Should().Match<DuplicateShareReconciliationResult>(x => x.Success && x.Changed);
        retry.Should().Match<DuplicateShareReconciliationResult>(x => x.Success && !x.Changed && x.SharedIdentities.Count == 1);
        var rows = await context.ShareRequests.Find(Builders<ShareRequest>.Filter.In(x => x.ShareId, [a.ShareId, b.ShareId, c.ShareId])).ToListAsync();
        rows.Single(x => x.ShareId == b.ShareId).CanonicalShareId.Should().Be(a.ShareId);
        rows.Single(x => x.ShareId == c.ShareId).CanonicalShareId.Should().Be(a.ShareId);
        var resolvedC = await new ShareRequestRepository(context).ResolveCanonicalAsync(rows.Single(x => x.ShareId == c.ShareId));
        resolvedC!.ShareId.Should().Be(a.ShareId);
    }

    [Fact]
    public async Task ItWillRejectAnIncomingAliasAddedAfterDryRunAndKeepTheAliasUnchanged()
    {
        var context = new TestDbContext(database);
        var a = Completed("aaaaaaaaaaaa", ObjectId.GenerateNewId().ToString());
        var b = Completed("bbbbbbbbbbbb", ObjectId.GenerateNewId().ToString());
        var c = Completed("cccccccccccc", ObjectId.GenerateNewId().ToString());
        await context.ShareRequests.InsertManyAsync([a, b, c]);
        await context.Songs.InsertManyAsync([ResolvedSong(a.SongId!), ResolvedSong(b.SongId!), ResolvedSong(c.SongId!)]);
        await context.SongServiceLinks.InsertManyAsync([Link(a.SongId!, "track"), Link(b.SongId!, "track")]);
        var service = new DuplicateShareReconciliationService(new ShareRequestRepository(context), new SongServiceLinkRepository(context), new SongRepository(context), NullLogger<DuplicateShareReconciliationService>.Instance);

        var dryRun = await service.ReconcileAsync(new(a.ShareId, b.ShareId, a.ShareId, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);
        await context.ShareRequests.UpdateOneAsync(x => x.ShareId == c.ShareId, Builders<ShareRequest>.Update.Set(x => x.CanonicalShareId, b.ShareId));
        var apply = await service.ReconcileAsync(new(a.ShareId, b.ShareId, a.ShareId, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);

        apply.Success.Should().BeFalse();
        (await context.ShareRequests.Find(x => x.ShareId == b.ShareId).FirstAsync()).CanonicalShareId.Should().BeNull();
    }

    [Fact]
    public async Task ItWillNotCreateAnIncomingAliasWhileItsTargetIsClaimed()
    {
        var context = new TestDbContext(database);
        var canonical = Completed("aaaaaaaaaaaa", ObjectId.GenerateNewId().ToString());
        var alias = Completed("bbbbbbbbbbbb", ObjectId.GenerateNewId().ToString());
        await context.ShareRequests.InsertManyAsync([canonical, alias]);
        await context.Songs.InsertManyAsync([ResolvedSong(canonical.SongId!), ResolvedSong(alias.SongId!)]);
        await context.SongServiceLinks.InsertManyAsync([Link(canonical.SongId!, "track"), Link(alias.SongId!, "track")]);
        var write = await WriteAsync(context, canonical, alias);
        // Any operation which would write B -> A must claim A as well. An exact-token holder
        // therefore blocks it before it can observe/write a reverse alias state.
        await context.ShareRequests.UpdateOneAsync(x => x.ShareId == canonical.ShareId, Builders<ShareRequest>.Update
            .Set(x => x.ReconciliationClaimToken, "holder")
            .Set(x => x.ReconciliationClaimExpiresAt, DateTime.UtcNow.AddMinutes(1)));

        var result = await new ShareRequestRepository(context).TryReconcileAsync(write);

        result.Succeeded.Should().BeFalse();
        (await context.ShareRequests.Find(x => x.ShareId == alias.ShareId).FirstAsync()).CanonicalShareId.Should().BeNull();
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
    [InlineData("source-service")]
    [InlineData("source-track")]
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
            canonicalSong, aliasSong, ShareStatus.Completed, ShareStatus.Completed, canonical.CreatedAt, alias.CreatedAt,
            snapshot.CanonicalSourceService, snapshot.CanonicalServiceTrackId, snapshot.CanonicalSourceIdentityKey, 0, 0);

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
            case "source-service":
                await context.ShareRequests.UpdateOneAsync(x => x.ShareId == canonical.ShareId, Builders<ShareRequest>.Update.Set(x => x.SourceService, ServiceType.YouTubeMusic));
                break;
            case "source-track":
                await context.ShareRequests.UpdateOneAsync(x => x.ShareId == canonical.ShareId, Builders<ShareRequest>.Update.Set(x => x.ServiceTrackId, "replacement-track"));
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
        return new(canonical.ShareId, alias.ShareId, $"reconcile-{snapshot.Fingerprint}", snapshot.Fingerprint, canonical.SongId!, alias.SongId!, ShareStatus.Completed, ShareStatus.Completed, canonical.CreatedAt, alias.CreatedAt,
            snapshot.CanonicalSourceService, snapshot.CanonicalServiceTrackId, snapshot.CanonicalSourceIdentityKey, 0, 0);
    }

    private static ShareRequest Request(string id, string? key) => new()
    {
        Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(), ShareId = id, SourceUrl = "https://example.test", SourceService = ServiceType.Spotify,
        CorrelationId = Guid.NewGuid(), SourceIdentityKey = key
    };

    private static ShareRequest Completed(string shareId, string songId) => new()
    {
        Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(), ShareId = shareId, SongId = songId, SourceUrl = "https://example.test",
        SourceService = ServiceType.Spotify, ServiceTrackId = "track", CorrelationId = Guid.NewGuid(), Status = ShareStatus.Completed, CreatedAt = DateTime.UtcNow
    };

    private static Song ResolvedSong(string id) => new() { Id = id, Status = SongStatus.Resolved, CreatedAt = DateTime.UnixEpoch, UpdatedAt = DateTime.UnixEpoch };
    private static SongServiceLink Link(string songId, string identity, ServiceType serviceType = ServiceType.Spotify) => new() { Id = ObjectId.GenerateNewId().ToString(), SongId = songId, ServiceType = serviceType, ServiceSongId = identity, OriginalUrl = "https://example.test", NormalizedUrl = "https://example.test", CreatedAt = DateTime.UnixEpoch };
    private static BsonDocument LegacyCompletedDocument(string shareId, string songId, DateTime createdAt) => new()
    {
        { "_id", ObjectId.GenerateNewId() }, { "shareId", shareId }, { "sourceUrl", "https://example.test" }, { "sourceService", ServiceType.Spotify.ToString() },
        { "serviceTrackId", "track" }, { "songId", ObjectId.Parse(songId) }, { "status", ShareStatus.Completed.ToString() },
        { "correlationId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) }, { "createdAt", createdAt }
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
