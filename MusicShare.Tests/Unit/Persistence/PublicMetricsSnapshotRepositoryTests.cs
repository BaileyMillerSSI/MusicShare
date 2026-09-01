using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Tests.Unit.Persistence;

public class PublicMetricsSnapshotRepositoryTests
{
    [Fact]
    public void ItWillOmitTheReconciliationFloorUntilAnAuthorizedDecreaseSetsIt()
    {
        var snapshot = new PublicMetricsSnapshot();

        snapshot.ToBsonDocument().Contains(nameof(PublicMetricsSnapshot.ReconciliationDecreaseVersionFloor)).Should().BeFalse();

        snapshot.ReconciliationDecreaseVersionFloor = 12;
        snapshot.ToBsonDocument()[nameof(PublicMetricsSnapshot.ReconciliationDecreaseVersionFloor)].ToInt64().Should().Be(12);
    }

    [Fact]
    public void ItWillOnlyReplaceTheSingletonWhenTheStoredSnapshotIsOlder()
    {
        var rendered = PublicMetricsSnapshotRepository.BuildNonRegressionFilter(42, 100)
            .Render(new RenderArgs<PublicMetricsSnapshot>(
                BsonSerializer.SerializerRegistry.GetSerializer<PublicMetricsSnapshot>(),
                BsonSerializer.SerializerRegistry));

        var filter = rendered.ToString();
        filter.Should().Contain(PublicMetricsSnapshot.SingletonId)
            .And.Contain("TotalCompletedSongs")
            .And.Contain("SnapshotVersion")
            .And.Contain("ReconciliationDecreaseVersionFloor");
    }

    [Fact]
    public void ItWillBuildTheAuthorizedFilterFromTheStrictlyNewerVersionOnly()
    {
        var rendered = PublicMetricsSnapshotRepository.BuildNewerVersionFilter(100)
            .Render(new RenderArgs<PublicMetricsSnapshot>(
                BsonSerializer.SerializerRegistry.GetSerializer<PublicMetricsSnapshot>(),
                BsonSerializer.SerializerRegistry));

        var filter = rendered.ToString();
        filter.Should().Contain(PublicMetricsSnapshot.SingletonId)
            .And.Contain("SnapshotVersion")
            .And.Contain("ReconciliationDecreaseVersionFloor")
            .And.NotContain("TotalCompletedSongs");
    }
}
