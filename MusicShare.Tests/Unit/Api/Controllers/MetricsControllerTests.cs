using MassTransit;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Controllers;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Controllers;

public class MetricsControllerTests
{
    [Fact]
    public async Task ItWillReturnTheStoredSnapshot()
    {
        var expected = new PublicMetricsResponse(1, DateTime.UtcNow, [new PublicMetricsServiceCountResponse(ServiceType.Spotify, 1)], [], []);
        var metrics = new Mock<IPublicMetricsService>();
        metrics.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var publish = new Mock<IPublishEndpoint>();

        var result = await new MetricsController(metrics.Object, publish.Object).Get(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ItWillReturnTheSafeEmptySnapshot()
    {
        var metrics = new Mock<IPublicMetricsService>();
        metrics.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(PublicMetricsResponse.Empty());
        var publish = new Mock<IPublishEndpoint>();

        var result = await new MetricsController(metrics.Object, publish.Object).Get(CancellationToken.None);

        var value = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PublicMetricsResponse>().Which;
        value.TotalCompletedSongs.Should().Be(0);
        value.RecentSongs.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillQueueARefreshWithoutAggregatingInline()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var metrics = new Mock<IPublicMetricsService>();
        var publish = new Mock<IPublishEndpoint>();
        publish.Setup(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), cancellationToken))
            .Returns(Task.CompletedTask);

        var result = await new MetricsController(metrics.Object, publish.Object).Refresh(cancellationToken);

        result.StatusCode.Should().Be(202);
        publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), cancellationToken), Times.Once);
        metrics.Verify(x => x.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
