using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using MusicShare.Contracts;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Integration;

/// <summary>Exercises the actual MongoDB aggregation and compare-and-set filters, not rendered BSON alone.</summary>
public class PublicMetricsMongoRepositoryTests : IAsyncLifetime
{
    private MongoDbRunner? _runner;
    private IMongoDatabase _database = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("MUSICSHARE_TEST_MONGODB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _runner = MongoDbRunner.Start(singleNodeReplSet: true);
            connectionString = _runner.ConnectionString;
        }

        _database = new MongoClient(connectionString).GetDatabase($"metrics-{Guid.NewGuid():N}");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _runner?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ItWillAggregateDistinctCompletedSongsAndResolvedLinksInDeterministicBoundedOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TestDbContext(_database);
        var requests = _database.GetCollection<BsonDocument>("requests");
        var old = DateTime.UtcNow.AddMinutes(-1);
        var newest = DateTime.UtcNow;
        var firstSong = ObjectId.GenerateNewId().ToString();
        var secondSong = ObjectId.GenerateNewId().ToString();
        var pendingSong = ObjectId.GenerateNewId().ToString();
        await context.Songs.InsertManyAsync([
            new Song { Id = firstSong, Title = "First", Artists = ["Artist"], CreatedAt = old },
            new Song { Id = secondSong, Title = "Second", Artists = ["Artist"], CreatedAt = newest }
        ], cancellationToken: cancellationToken);
        await requests.InsertManyAsync([
            RequestDocument(firstSong, "share-old", ServiceType.Spotify, old),
            RequestDocument(firstSong, "share-new", ServiceType.YouTubeMusic, newest),
            RequestDocument(secondSong, "share-second", ServiceType.AppleMusic, newest),
            RequestDocument(ObjectId.GenerateNewId().ToString(), "unknown", ServiceType.Unknown, newest),
            RequestDocument(ObjectId.GenerateNewId().ToString(), "undefined", (ServiceType)999, newest),
            RequestDocument(pendingSong, "pending", ServiceType.Spotify, newest, ShareStatus.Pending)
        ], cancellationToken: cancellationToken);
        await _database.GetCollection<BsonDocument>("links").InsertManyAsync([
            LinkDocument(firstSong, ServiceType.Spotify),
            LinkDocument(firstSong, ServiceType.YouTubeMusic),
            LinkDocument(firstSong, ServiceType.YouTubeMusic),
            LinkDocument(secondSong, ServiceType.AppleMusic),
            LinkDocument(pendingSong, ServiceType.Spotify),
            LinkDocument(ObjectId.GenerateNewId().ToString(), ServiceType.Spotify),
            LinkDocument(ObjectId.GenerateNewId().ToString(), ServiceType.Unknown),
            new BsonDocument { { "songId", "not-an-object-id" }, { "serviceType", ServiceType.Spotify.ToString() } }
        ], cancellationToken: cancellationToken);
        var repository = new ShareRequestRepository(context);
        var links = new SongServiceLinkRepository(context);

        var total = await repository.GetCompletedDistinctSongCountAsync(cancellationToken);
        var counts = await links.GetCompletedDistinctSongLinkCountsAsync(cancellationToken);
        var recent = await repository.GetRecentCompletedDistinctAsync(1, cancellationToken);

        total.Should().Be(2);
        counts.Should().BeEquivalentTo(new Dictionary<ServiceType, long>
        {
            [ServiceType.Spotify] = 1, [ServiceType.YouTubeMusic] = 1, [ServiceType.AppleMusic] = 1
        });
        var only = recent.Should().ContainSingle().Which;
        only.SongId.Should().Be(secondSong);
        only.ShareId.Should().Be("share-second", "shareId breaks equal timestamp ties descending");
    }

    [Fact]
    public async Task ItWillUseCanonicalSongDatesForDailyBucketsAndRecentOrdering()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TestDbContext(_database);
        var requests = _database.GetCollection<BsonDocument>("requests");
        var firstWeek = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        var duplicateSong = ObjectId.GenerateNewId().ToString();
        var secondWeekSong = ObjectId.GenerateNewId().ToString();
        var malformedSong = ObjectId.GenerateNewId().ToString();
        await context.Songs.InsertManyAsync([
            new Song { Id = duplicateSong, Title = "First week", Artists = ["Artist"], CreatedAt = firstWeek.AddDays(1) },
            new Song { Id = secondWeekSong, Title = "Second week", Artists = ["Artist"], CreatedAt = firstWeek.AddDays(7) }
        ], cancellationToken: cancellationToken);
        await _database.GetCollection<BsonDocument>("songs").InsertOneAsync(new BsonDocument
        {
            { "_id", ObjectId.Parse(malformedSong) }, { "title", "Malformed" }, { "artists", new BsonArray { "Artist" } }, { "createdAt", "not-a-date" }
        }, cancellationToken: cancellationToken);
        await requests.InsertManyAsync([
            RequestDocument(duplicateSong, "first", ServiceType.Spotify, firstWeek.AddDays(8)),
            RequestDocument(duplicateSong, "later", ServiceType.YouTubeMusic, firstWeek.AddDays(9)),
            RequestDocument(secondWeekSong, "second-week", ServiceType.Spotify, firstWeek.AddDays(1)),
            RequestDocument(ObjectId.GenerateNewId().ToString(), "orphan", ServiceType.Spotify, firstWeek.AddDays(2)),
            RequestDocument(malformedSong, "malformed", ServiceType.Spotify, firstWeek.AddDays(2)),
            RequestDocument(ObjectId.GenerateNewId().ToString(), "excluded", ServiceType.Unknown, firstWeek.AddDays(2))
        ], cancellationToken: cancellationToken);

        var repository = new ShareRequestRepository(context);
        var result = await repository.GetCompletedDistinctSongCountsByDayAsync(firstWeek, firstWeek.AddDays(14), cancellationToken);

        result.Should().BeEquivalentTo([
            new DailyCompletedSongCount(firstWeek.AddDays(1), 1),
            new DailyCompletedSongCount(firstWeek.AddDays(7), 1)
        ], options => options.WithStrictOrdering());
        var recent = await repository.GetRecentCompletedDistinctAsync(10, cancellationToken);
        recent.Select(x => x.SongId).Should().Equal(secondWeekSong, duplicateSong);
        recent.First().CreatedAt.Should().Be(firstWeek.AddDays(7));
    }

    [Fact]
    public async Task ItWillInsertReplaceAndRejectStaleSnapshotCandidatesIncludingEqualTotals()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new PublicMetricsSnapshotRepository(new TestDbContext(_database));

        (await repository.TryReplaceAsync(Snapshot(2, 10), cancellationToken)).Should().BeTrue();
        (await repository.TryReplaceAsync(Snapshot(3, 11), cancellationToken)).Should().BeTrue();
        (await repository.TryReplaceAsync(Snapshot(2, 12), cancellationToken)).Should().BeFalse();
        (await repository.TryReplaceAsync(Snapshot(3, 10), cancellationToken)).Should().BeFalse();
        (await repository.GetAsync(cancellationToken)).Should().Match<PublicMetricsSnapshot>(x => x.TotalCompletedSongs == 3 && x.SnapshotVersion == 11);
    }

    [Fact]
    public async Task ItWillAcceptOnlyOneOfConcurrentDuplicateCandidates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new PublicMetricsSnapshotRepository(new TestDbContext(_database));
        var candidate = Snapshot(4, 20);

        var accepted = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => repository.TryReplaceAsync(candidate, cancellationToken)));

        accepted.Count(x => x).Should().Be(1);
        (await repository.GetAsync(cancellationToken)).Should().Match<PublicMetricsSnapshot>(x => x.TotalCompletedSongs == 4 && x.SnapshotVersion == 20);
    }

    [Fact]
    public async Task ItWillRejectAnOlderEqualTotalRefreshAfterANewerAuthoritativeViewPersists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TestDbContext(_database);
        var oldCreatedAt = TruncateToMongoMilliseconds(DateTime.UtcNow.AddMinutes(-2));
        var changedCreatedAt = TruncateToMongoMilliseconds(DateTime.UtcNow.AddMinutes(-1));
        var unchangedGlobalMaximum = TruncateToMongoMilliseconds(DateTime.UtcNow);
        var changedSongId = ObjectId.GenerateNewId().ToString();
        var anchorSongId = ObjectId.GenerateNewId().ToString();
        await context.Songs.InsertManyAsync([
            new Song { Id = changedSongId, Title = "Changed Song", Artists = ["Artist"], CreatedAt = oldCreatedAt },
            new Song { Id = anchorSongId, Title = "Anchor Song", Artists = ["Artist"], CreatedAt = unchangedGlobalMaximum }
        ], cancellationToken: cancellationToken);
        await _database.GetCollection<BsonDocument>("requests").InsertManyAsync([
            RequestDocument(changedSongId, "share-old", ServiceType.Spotify, oldCreatedAt),
            RequestDocument(anchorSongId, "share-anchor", ServiceType.Spotify, unchangedGlobalMaximum)
        ], cancellationToken: cancellationToken);

        var delayedRequests = new DelayedAfterRecentReadRepository(new ShareRequestRepository(context));
        var snapshots = new PublicMetricsSnapshotRepository(context);
        var olderRefresh = new PublicMetricsService(delayedRequests, new SongServiceLinkRepository(context), new SongRepository(context), snapshots).RefreshAsync(cancellationToken);
        await delayedRequests.RecentRead.WaitAsync(cancellationToken);

        await _database.GetCollection<BsonDocument>("requests").InsertOneAsync(
            RequestDocument(changedSongId, "share-new", ServiceType.YouTubeMusic, changedCreatedAt),
            cancellationToken: cancellationToken);
        var newerResult = await new PublicMetricsService(new ShareRequestRepository(context), new SongServiceLinkRepository(context), new SongRepository(context), snapshots).RefreshAsync(cancellationToken);
        delayedRequests.Release();
        var olderResult = await olderRefresh;

        newerResult.Accepted.Should().BeTrue();
        olderResult.Accepted.Should().BeFalse();
        (await snapshots.GetAsync(cancellationToken)).Should().Match<PublicMetricsSnapshot>(x =>
            x.TotalCompletedSongs == 2 &&
            x.SnapshotVersion == 2 &&
            x.RecentSongs.Any(song => song.ShareId == "share-new" && song.SourceService == ServiceType.YouTubeMusic) &&
            x.RecentSongs.Max(song => song.CreatedAt) == unchangedGlobalMaximum);
    }

    [Fact]
    public async Task ItWillReserveDatabaseOrderedVersionsAcrossConcurrentCallers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new PublicMetricsSnapshotRepository(new TestDbContext(_database));

        var versions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => repository.ReserveVersionAsync(cancellationToken)));

        versions.Should().OnlyHaveUniqueItems();
        versions.Order().Should().Equal(Enumerable.Range(1, 8).Select(x => (long)x));
    }

    private static PublicMetricsSnapshot Snapshot(long total, long version) => new() { TotalCompletedSongs = total, SnapshotVersion = version, GeneratedAt = DateTime.UtcNow };
    private static DateTime TruncateToMongoMilliseconds(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    private static BsonDocument RequestDocument(string songId, string shareId, ServiceType service, DateTime createdAt, ShareStatus status = ShareStatus.Completed) => new()
    {
        { "_id", ObjectId.GenerateNewId() }, { "shareId", shareId }, { "sourceUrl", "https://example.test" },
        { "sourceService", service.ToString() }, { "songId", ObjectId.Parse(songId) }, { "status", status.ToString() }, { "createdAt", createdAt }
    };

    private static BsonDocument LinkDocument(string songId, ServiceType service) => new()
    {
        { "_id", ObjectId.GenerateNewId() }, { "songId", ObjectId.Parse(songId) }, { "serviceType", service.ToString() },
        { "serviceSongId", Guid.NewGuid().ToString("N") }, { "originalUrl", "https://example.test" }, { "normalizedUrl", "https://example.test" },
        { "createdAt", DateTime.UtcNow }
    };

    private sealed class TestDbContext(IMongoDatabase database) : IMusicShareDbContext
    {
        public IMongoDatabase Database => database;
        public IMongoCollection<ShareRequest> ShareRequests => database.GetCollection<ShareRequest>("requests");
        public IMongoCollection<Song> Songs => database.GetCollection<Song>("songs");
        public IMongoCollection<SongServiceLink> SongServiceLinks => database.GetCollection<SongServiceLink>("links");
        public IMongoCollection<WorkflowState> WorkflowStates => database.GetCollection<WorkflowState>("workflows");
        public IMongoCollection<PublicMetricsSnapshot> PublicMetricsSnapshots => database.GetCollection<PublicMetricsSnapshot>("snapshots");
    }

    private sealed class DelayedAfterRecentReadRepository(IShareRequestRepository inner) : IShareRequestRepository
    {
        private readonly TaskCompletionSource _recentRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RecentRead => _recentRead.Task;
        public void Release() => _release.SetResult();
        public Task<ShareRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<ShareRequest?> GetByShareIdAsync(string shareId, CancellationToken cancellationToken = default) => inner.GetByShareIdAsync(shareId, cancellationToken);
        public Task<ShareRequest?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default) => inner.GetByCorrelationIdAsync(correlationId, cancellationToken);
        public Task<ShareRequest?> GetBySongIdAsync(string songId, CancellationToken cancellationToken = default) => inner.GetBySongIdAsync(songId, cancellationToken);
        public Task<ShareRequest?> GetByServiceTrackIdAsync(ServiceType serviceType, string serviceTrackId, CancellationToken cancellationToken = default) => inner.GetByServiceTrackIdAsync(serviceType, serviceTrackId, cancellationToken);
        public Task<ShareRequest> InsertAsync(ShareRequest request, CancellationToken cancellationToken = default) => inner.InsertAsync(request, cancellationToken);
        public Task UpdateAsync(ShareRequest request, CancellationToken cancellationToken = default) => inner.UpdateAsync(request, cancellationToken);
        public Task<long> GetCompletedDistinctSongCountAsync(CancellationToken cancellationToken = default) => inner.GetCompletedDistinctSongCountAsync(cancellationToken);
        public Task<IReadOnlyList<DailyCompletedSongCount>> GetCompletedDistinctSongCountsByDayAsync(DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default) => inner.GetCompletedDistinctSongCountsByDayAsync(rangeStartUtc, rangeEndUtc, cancellationToken);

        public async Task<IReadOnlyList<CompletedShareRequest>> GetRecentCompletedDistinctAsync(int maximum, CancellationToken cancellationToken = default)
        {
            var requests = await inner.GetRecentCompletedDistinctAsync(maximum, cancellationToken);
            _recentRead.SetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return requests;
        }
    }
}
