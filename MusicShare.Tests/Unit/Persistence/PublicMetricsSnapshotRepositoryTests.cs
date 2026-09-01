using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Tests.Unit.Persistence;

public class PublicMetricsSnapshotRepositoryTests
{
    [Fact]
    public void ItWillOnlyReplaceTheSingletonWhenTheStoredSnapshotIsOlder()
    {
        var rendered = PublicMetricsSnapshotRepository.BuildNonRegressionFilter(42, 100)
            .Render(new RenderArgs<PublicMetricsSnapshot>(
                BsonSerializer.SerializerRegistry.GetSerializer<PublicMetricsSnapshot>(),
                BsonSerializer.SerializerRegistry));

        rendered["_id"].AsString.Should().Be(PublicMetricsSnapshot.SingletonId);
        var candidates = rendered["$or"].AsBsonArray;
        candidates.Should().HaveCount(2);
        candidates[0].AsBsonDocument["TotalCompletedSongs"].AsBsonDocument["$lt"].ToInt64().Should().Be(42);
        candidates[1].AsBsonDocument["SnapshotVersion"].AsBsonDocument["$lt"].ToInt64().Should().Be(100);
    }

    [Fact]
    public void ItWillBuildTheAuthorizedFilterFromTheStrictlyNewerVersionOnly()
    {
        var rendered = PublicMetricsSnapshotRepository.BuildNewerVersionFilter(100)
            .Render(new RenderArgs<PublicMetricsSnapshot>(
                BsonSerializer.SerializerRegistry.GetSerializer<PublicMetricsSnapshot>(),
                BsonSerializer.SerializerRegistry));

        rendered["_id"].AsString.Should().Be(PublicMetricsSnapshot.SingletonId);
        rendered["SnapshotVersion"].AsBsonDocument["$lt"].ToInt64().Should().Be(100);
        rendered.Contains("TotalCompletedSongs").Should().BeFalse();
    }
}
