using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Controllers;
using MusicShare.Contracts;
using MusicShare.Services.Models;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Controllers;

public class MetricsControllerTests
{
    [Fact]
    public async Task ItWillReturnTheStoredSnapshot()
    {
        var expected = new PublicMetricsResponse(1, DateTime.UtcNow, [new PublicMetricsServiceCountResponse(ServiceType.Spotify, 1)], []);
        var metrics = new Mock<IPublicMetricsService>();
        metrics.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new MetricsController(metrics.Object).Get(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ItWillReturnTheSafeEmptySnapshot()
    {
        var metrics = new Mock<IPublicMetricsService>();
        metrics.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(PublicMetricsResponse.Empty());

        var result = await new MetricsController(metrics.Object).Get(CancellationToken.None);

        var value = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PublicMetricsResponse>().Which;
        value.TotalCompletedSongs.Should().Be(0);
        value.RecentSongs.Should().BeEmpty();
    }
}
