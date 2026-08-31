using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class PublicMetricsServiceTests
{
    [Fact]
    public async Task ItWillReturnZeroesForEveryKnownServiceBeforeBootstrap()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsSnapshotRepository>().Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((PublicMetricsSnapshot?)null);
        var result = await mock.Create<PublicMetricsService>().GetAsync(TestContext.Current.CancellationToken);
        result.TotalCompletedSongs.Should().Be(0);
        result.ServiceCounts.Should().Contain(x => x.Service == ServiceType.Spotify && x.Count == 0)
            .And.Contain(x => x.Service == ServiceType.AppleMusic && x.Count == 0)
            .And.Contain(x => x.Service == ServiceType.YouTubeMusic && x.Count == 0);
    }

    [Fact]
    public async Task ItWillCreateBoundedSnapshotAndPreserveCanonicalShareIds()
    {
        using var mock = AutoMock.GetLoose();
        var requests = Enumerable.Range(1, 21).Select(i => new CompletedShareRequest($"song-{i}", $"share-{i}", ServiceType.Spotify, DateTime.UtcNow.AddMinutes(-i))).ToList();
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetCompletedDistinctSongCountsBySourceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ServiceType, long> { [ServiceType.Spotify] = 21 });
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetRecentCompletedDistinctAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(requests.Take(20).ToList());
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests.Select(x => new Song { Id = x.SongId, Title = x.SongId, Artists = ["Artist"] }).ToList());
        mock.Mock<IPublicMetricsSnapshotRepository>().Setup(x => x.TryReplaceAsync(It.IsAny<PublicMetricsSnapshot>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await mock.Create<PublicMetricsService>().RefreshAsync(TestContext.Current.CancellationToken);
        result.Accepted.Should().BeTrue();
        result.Snapshot.RecentSongs.Should().HaveCount(20);
        result.Snapshot.RecentSongs.First().ShareId.Should().Be("share-1");
    }
}
