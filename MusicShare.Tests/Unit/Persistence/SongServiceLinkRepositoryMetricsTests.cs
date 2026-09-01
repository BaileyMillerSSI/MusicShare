using MongoDB.Bson;
using MusicShare.Contracts;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Tests.Unit.Persistence;

public class SongServiceLinkRepositoryMetricsTests
{
    [Fact]
    public void ItWillBuildACompletedShareLookupAfterDeduplicatingSongServicePairs()
    {
        var pipeline = SongServiceLinkRepository.BuildCompletedDistinctSongLinkCountPipeline("test-requests");

        pipeline[0]["$match"].AsBsonDocument["songId"].AsBsonDocument["$type"].AsString.Should().Be("objectId");
        pipeline[1]["$group"].AsBsonDocument["_id"].AsBsonDocument.Names.Should().ContainInOrder("songId", "serviceType");
        var lookup = pipeline[2]["$lookup"].AsBsonDocument;
        lookup["from"].AsString.Should().Be("test-requests");
        lookup["pipeline"].AsBsonArray.Should().ContainSingle();
        lookup["pipeline"].AsBsonArray[0]["$match"].AsBsonDocument["$expr"].AsBsonDocument["$and"].AsBsonArray
            .Should().Contain(x => x.AsBsonDocument["$eq"].AsBsonArray[1].AsString == "missing");
        pipeline[3]["$match"].AsBsonDocument.Names.Should().Contain("completedShares.0");
        pipeline[4]["$group"].AsBsonDocument["_id"].AsString.Should().Be("$serviceType");
    }
}
