using MongoDB.Bson;
using MusicShare.Contracts;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Tests.Unit.Persistence;

public class ShareRequestRepositoryMetricsTests
{
    [Fact]
    public void ItWillBuildACompletedDistinctPipelineForObjectIdSongIds()
    {
        var pipeline = ShareRequestRepository.DistinctCompletedPipeline();

        var match = pipeline[0]["$match"].AsBsonDocument;
        match.ElementCount.Should().Be(3);
        match["status"].AsString.Should().Be(ShareStatus.Completed.ToString());
        match["songId"].AsBsonDocument["$type"].AsString.Should().Be("objectId");
        match["sourceService"].AsBsonDocument["$in"].AsBsonArray.Select(x => x.AsString)
            .Should().BeEquivalentTo([ServiceType.Spotify.ToString(), ServiceType.AppleMusic.ToString(), ServiceType.YouTubeMusic.ToString()]);
        pipeline[1]["$sort"].AsBsonDocument.Names.Should().ContainInOrder("createdAt", "shareId");
        pipeline[2]["$group"].AsBsonDocument["_id"].AsString.Should().Be("$songId");
    }

    [Fact]
    public void ItWillMaterializeObjectIdSongIdsAndIgnoreMalformedAggregateRows()
    {
        var songId = ObjectId.GenerateNewId();
        var createdAt = DateTime.UtcNow;
        var rows = new[]
        {
            new BsonDocument
            {
                { "songId", songId }, { "shareId", "share-1" }, { "sourceService", ServiceType.Spotify.ToString() }, { "createdAt", createdAt }
            },
            new BsonDocument
            {
                { "songId", "not-an-object-id" }, { "shareId", "share-2" }, { "sourceService", ServiceType.Spotify.ToString() }, { "createdAt", createdAt }
            },
            new BsonDocument
            {
                { "songId", ObjectId.GenerateNewId() }, { "shareId", "share-3" }, { "sourceService", ServiceType.Unknown.ToString() }, { "createdAt", createdAt }
            },
            new BsonDocument
            {
                { "songId", ObjectId.GenerateNewId() }, { "shareId", "share-4" }, { "sourceService", "999" }, { "createdAt", createdAt }
            }
        };

        var result = ShareRequestRepository.MaterializeCompletedRequests(rows);

        var completed = result.Should().ContainSingle().Which;
        completed.SongId.Should().Be(songId.ToString());
        completed.ShareId.Should().Be("share-1");
        completed.SourceService.Should().Be(ServiceType.Spotify);
        completed.CreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void ItWillBuildAndMaterializeSundayUtcWeeklyCounts()
    {
        var start = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        var pipeline = ShareRequestRepository.DistinctCompletedEarliestPipeline();
        pipeline[1]["$sort"].AsBsonDocument["createdAt"].Should().Be(1);
        pipeline[2]["$group"].AsBsonDocument["createdAt"].AsBsonDocument["$first"].AsString.Should().Be("$createdAt");

        var result = ShareRequestRepository.MaterializeWeeklyCompletedSongCounts([
            new BsonDocument { { "weekStart", start }, { "count", 2 } },
            new BsonDocument { { "weekStart", start.AddDays(7) }, { "count", -1 } },
            new BsonDocument { { "weekStart", "bad" }, { "count", 4 } }
        ]);

        result.Should().Equal(new WeeklyCompletedSongCount(start, 2));
    }
}
