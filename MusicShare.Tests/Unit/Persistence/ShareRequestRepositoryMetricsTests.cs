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
        match.ElementCount.Should().Be(2);
        match["status"].AsString.Should().Be(ShareStatus.Completed.ToString());
        match["songId"].AsBsonDocument["$type"].AsString.Should().Be("objectId");
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
            }
        };

        var result = ShareRequestRepository.MaterializeCompletedRequests(rows);

        var completed = result.Should().ContainSingle().Which;
        completed.SongId.Should().Be(songId.ToString());
        completed.ShareId.Should().Be("share-1");
        completed.SourceService.Should().Be(ServiceType.Spotify);
        completed.CreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromMilliseconds(1));
    }
}
