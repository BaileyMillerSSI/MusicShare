using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Models;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class PublicMetricsServiceTests
{
    [Fact]
    public async Task ItWillReturnZeroesForMetricsPlatformsBeforeBootstrap()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsSnapshotRepository>().Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((PublicMetricsSnapshot?)null);
        var result = await mock.Create<PublicMetricsService>().GetAsync(TestContext.Current.CancellationToken);
        result.TotalCompletedSongs.Should().Be(0);
        result.ServiceCounts.Should().Contain(x => x.Service == ServiceType.Spotify && x.Count == 0)
            .And.Contain(x => x.Service == ServiceType.YouTubeMusic && x.Count == 0);
        result.ServiceCounts.Should().NotContain(x => x.Service == ServiceType.AppleMusic);
        result.DailyCompletedSongs.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillHideAppleCountsFromLegacySnapshots()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsSnapshotRepository>().Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PublicMetricsSnapshot
        {
            TotalCompletedSongs = 1,
            ServiceCounts =
            [
                new PublicMetricsServiceCount { Service = ServiceType.Spotify, Count = 1 },
                new PublicMetricsServiceCount { Service = ServiceType.AppleMusic, Count = 99 }
            ]
        });

        var result = await mock.Create<PublicMetricsService>().GetAsync(TestContext.Current.CancellationToken);

        result.ServiceCounts.Should().BeEquivalentTo([
            new PublicMetricsServiceCountResponse(ServiceType.Spotify, 1),
            new PublicMetricsServiceCountResponse(ServiceType.YouTubeMusic, 0)
        ]);
    }

    [Fact]
    public async Task ItWillCreateBoundedSnapshotAndPreserveCanonicalShareIds()
    {
        using var mock = AutoMock.GetLoose();
        var requests = Enumerable.Range(1, 21).Select(i => new CompletedShareRequest($"song-{i}", $"share-{i}", ServiceType.Spotify, DateTime.UtcNow.AddMinutes(-i))).ToList();
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetCompletedDistinctSongCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(21);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetCompletedDistinctSongLinkCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ServiceType, long> { [ServiceType.Spotify] = 21, [ServiceType.YouTubeMusic] = 1 });
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetRecentCompletedDistinctAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(requests.Take(20).ToList());
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests.Select(x => new Song { Id = x.SongId, Title = x.SongId, Artists = ["Artist"] }).ToList());
        mock.Mock<IPublicMetricsSnapshotRepository>().Setup(x => x.TryReplaceAsync(It.IsAny<PublicMetricsSnapshot>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await mock.Create<PublicMetricsService>().RefreshAsync(TestContext.Current.CancellationToken);
        result.Accepted.Should().BeTrue();
        result.Snapshot.RecentSongs.Should().HaveCount(20);
        result.Snapshot.RecentSongs.First().ShareId.Should().Be("share-1");
    }

    [Fact]
    public async Task ItWillPublishIndependentCompletedSongAndResolvedLinkCountsForMetricsPlatformsOnly()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetCompletedDistinctSongCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetCompletedDistinctSongLinkCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ServiceType, long>
            {
                [ServiceType.Spotify] = 1, [ServiceType.YouTubeMusic] = 1, [ServiceType.AppleMusic] = 9,
                [ServiceType.Unknown] = 4, [(ServiceType)999] = 8
            });
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetRecentCompletedDistinctAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CompletedShareRequest("song", "share", (ServiceType)999, DateTime.UtcNow)]);
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        mock.Mock<IPublicMetricsSnapshotRepository>().Setup(x => x.TryReplaceAsync(It.IsAny<PublicMetricsSnapshot>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await mock.Create<PublicMetricsService>().RefreshAsync(TestContext.Current.CancellationToken);

        result.Snapshot.TotalCompletedSongs.Should().Be(1);
        result.Snapshot.ServiceCounts.Should().BeEquivalentTo([
            new PublicMetricsServiceCountResponse(ServiceType.Spotify, 1),
            new PublicMetricsServiceCountResponse(ServiceType.YouTubeMusic, 1)
        ]);
        result.Snapshot.ServiceCounts.Select(x => x.Service).Should().NotContain(ServiceType.Unknown);
        result.Snapshot.ServiceCounts.Should().NotContain(x => x.Service == ServiceType.AppleMusic || !Enum.IsDefined(x.Service));
        result.Snapshot.RecentSongs.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillZeroFillSevenUtcDailyBuckets()
    {
        using var mock = AutoMock.GetLoose();
        var currentDay = PublicMetricsService.GetDayStartUtc(DateTime.UtcNow);
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetCompletedDistinctSongCountsByDayAsync(
            currentDay.AddDays(-6), currentDay.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyCompletedSongCount(currentDay, 3)]);
        mock.Mock<IPublicMetricsSnapshotRepository>().Setup(x => x.TryReplaceAsync(It.IsAny<PublicMetricsSnapshot>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await mock.Create<PublicMetricsService>().RefreshAsync(TestContext.Current.CancellationToken);

        result.Snapshot.DailyCompletedSongs.Should().HaveCount(7);
        result.Snapshot.DailyCompletedSongs.Last().Should().BeEquivalentTo(new PublicMetricsDailyCompletedSongResponse(currentDay, 3));
        result.Snapshot.DailyCompletedSongs.Take(6).Should().OnlyContain(x => x.Count == 0);
    }

    [Fact]
    public void ItWillCalculateUtcDayStarts()
    {
        PublicMetricsService.GetDayStartUtc(new DateTime(2026, 1, 10, 23, 59, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        Action action = () => PublicMetricsService.GetDayStartUtc(DateTime.Now);
        action.Should().Throw<ArgumentException>();
    }
}
