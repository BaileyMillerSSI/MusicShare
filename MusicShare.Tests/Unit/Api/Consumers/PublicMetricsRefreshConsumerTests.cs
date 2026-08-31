using MassTransit;
using MusicShare.Api.Consumers;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Consumers;

public class PublicMetricsRefreshConsumerTests
{
    [Fact]
    public async Task ItWillRevalidateOnlyAfterAnAcceptedSnapshot()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsService>().Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicMetricsRefreshResult(true, PublicMetricsResponse.Empty()));
        var context = new Mock<ConsumeContext<RefreshPublicMetrics>>();
        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);
        mock.Mock<IFrontendRevalidateService>().Verify(x => x.RevalidateMetricsAsync(), Times.Once);
    }

    [Fact]
    public async Task ItWillNotRevalidateWhenSnapshotIsSuperseded()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsService>().Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicMetricsRefreshResult(false, PublicMetricsResponse.Empty()));
        var context = new Mock<ConsumeContext<RefreshPublicMetrics>>();
        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);
        mock.Mock<IFrontendRevalidateService>().Verify(x => x.RevalidateMetricsAsync(), Times.Never);
    }
}
