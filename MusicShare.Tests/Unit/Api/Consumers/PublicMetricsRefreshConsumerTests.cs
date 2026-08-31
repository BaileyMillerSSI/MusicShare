using MassTransit;
using MusicShare.Api.Services;
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
        mock.Mock<IFrontendRevalidateService>().Setup(x => x.RevalidateMetricsAsync()).ReturnsAsync(true);
        var context = new Mock<ConsumeContext<RefreshPublicMetrics>>();
        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);
        mock.Mock<IFrontendRevalidateService>().Verify(x => x.RevalidateMetricsAsync(), Times.Once);
        mock.Mock<IPublicMetricsInvalidationRetryService>().Verify(x => x.ScheduleRetry(), Times.Never);
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

    [Fact]
    public async Task ItWillScheduleCheapInvalidationRetryWhenTheFrontendIsNotReady()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsService>().Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicMetricsRefreshResult(true, PublicMetricsResponse.Empty()));
        mock.Mock<IFrontendRevalidateService>().Setup(x => x.RevalidateMetricsAsync()).ReturnsAsync(false);
        var context = new Mock<ConsumeContext<RefreshPublicMetrics>>();

        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);

        mock.Mock<IPublicMetricsInvalidationRetryService>().Verify(x => x.ScheduleRetry(), Times.Once);
    }
}
