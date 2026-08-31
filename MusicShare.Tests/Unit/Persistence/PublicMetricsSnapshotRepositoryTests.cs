using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Tests.Unit.Persistence;

public class PublicMetricsSnapshotRepositoryTests
{
    [Fact]
    public void ItWillOnlyReplaceTheSingletonWhenTheStoredTotalDoesNotExceedTheCandidate()
    {
        var rendered = PublicMetricsSnapshotRepository.BuildNonRegressionFilter(42)
            .Render(new RenderArgs<PublicMetricsSnapshot>(
                BsonSerializer.SerializerRegistry.GetSerializer<PublicMetricsSnapshot>(),
                BsonSerializer.SerializerRegistry));

        rendered["_id"].AsString.Should().Be(PublicMetricsSnapshot.SingletonId);
        rendered["TotalCompletedSongs"].AsBsonDocument["$lte"].ToInt64().Should().Be(42);
    }
}
