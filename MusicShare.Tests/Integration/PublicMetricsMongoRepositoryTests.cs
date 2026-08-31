using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using MusicShare.Contracts;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Tests.Integration;

/// <summary>Exercises the actual MongoDB aggregation and compare-and-set filters, not rendered BSON alone.</summary>
public class PublicMetricsMongoRepositoryTests : IAsyncLifetime
{
    private MongoDbRunner _runner = null!;
    private IMongoDatabase _database = null!;

    public ValueTask InitializeAsync()
    {
        _runner = MongoDbRunner.Start(singleNodeReplSet: true);
        _database = new MongoClient(_runner.ConnectionString).GetDatabase($"metrics-{Guid.NewGuid():N}");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _runner.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ItWillAggregateOnlyValidDistinctCompletedSongsInDeterministicBoundedOrder()
    {
        var context = new TestDbContext(_database);
        var requests = _database.GetCollection<BsonDocument>("requests");
        var old = DateTime.UtcNow.AddMinutes(-1);
        var newest = DateTime.UtcNow;
        var firstSong = ObjectId.GenerateNewId().ToString();
        var secondSong = ObjectId.GenerateNewId().ToString();
        await requests.InsertManyAsync([
            RequestDocument(firstSong, "share-old", ServiceType.Spotify, old),
            RequestDocument(firstSong, "share-new", ServiceType.YouTubeMusic, newest),
            RequestDocument(secondSong, "share-second", ServiceType.AppleMusic, newest),
            RequestDocument(ObjectId.GenerateNewId().ToString(), "unknown", ServiceType.Unknown, newest),
            RequestDocument(ObjectId.GenerateNewId().ToString(), "undefined", (ServiceType)999, newest),
            RequestDocument(ObjectId.GenerateNewId().ToString(), "pending", ServiceType.Spotify, newest, ShareStatus.Pending)
        ]);
        var repository = new ShareRequestRepository(context);

        var counts = await repository.GetCompletedDistinctSongCountsBySourceAsync();
        var recent = await repository.GetRecentCompletedDistinctAsync(1);

        counts.Should().BeEquivalentTo(new Dictionary<ServiceType, long> { [ServiceType.YouTubeMusic] = 1, [ServiceType.AppleMusic] = 1 });
        var only = recent.Should().ContainSingle().Which;
        only.SongId.Should().Be(secondSong);
        only.ShareId.Should().Be("share-second", "shareId breaks equal timestamp ties descending");
    }

    [Fact]
    public async Task ItWillInsertReplaceAndRejectStaleSnapshotCandidatesIncludingEqualTotals()
    {
        var repository = new PublicMetricsSnapshotRepository(new TestDbContext(_database));

        (await repository.TryReplaceAsync(Snapshot(2, 10))).Should().BeTrue();
        (await repository.TryReplaceAsync(Snapshot(3, 11))).Should().BeTrue();
        (await repository.TryReplaceAsync(Snapshot(2, 12))).Should().BeFalse();
        (await repository.TryReplaceAsync(Snapshot(3, 10))).Should().BeFalse();
        (await repository.GetAsync()).Should().Match<PublicMetricsSnapshot>(x => x.TotalCompletedSongs == 3 && x.SnapshotVersion == 11);
    }

    [Fact]
    public async Task ItWillAcceptOnlyOneOfConcurrentDuplicateCandidates()
    {
        var repository = new PublicMetricsSnapshotRepository(new TestDbContext(_database));
        var candidate = Snapshot(4, 20);

        var accepted = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => repository.TryReplaceAsync(candidate)));

        accepted.Count(x => x).Should().Be(1);
        (await repository.GetAsync()).Should().Match<PublicMetricsSnapshot>(x => x.TotalCompletedSongs == 4 && x.SnapshotVersion == 20);
    }

    private static PublicMetricsSnapshot Snapshot(long total, long version) => new() { TotalCompletedSongs = total, SnapshotVersion = version, GeneratedAt = DateTime.UtcNow };
    private static BsonDocument RequestDocument(string songId, string shareId, ServiceType service, DateTime createdAt, ShareStatus status = ShareStatus.Completed) => new()
    {
        { "_id", ObjectId.GenerateNewId() }, { "shareId", shareId }, { "sourceUrl", "https://example.test" },
        { "sourceService", service.ToString() }, { "songId", ObjectId.Parse(songId) }, { "status", status.ToString() }, { "createdAt", createdAt }
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
}
