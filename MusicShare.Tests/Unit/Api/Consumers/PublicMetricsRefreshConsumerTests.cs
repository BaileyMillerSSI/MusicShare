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
        mock.Mock<IPublicMetricsService>().Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new PublicMetricsRefreshResult(true, PublicMetricsResponse.Empty()));
        mock.Mock<IFrontendRevalidateService>().Setup(x => x.RevalidateMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var context = CreateContext();
        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);
        mock.Mock<IFrontendRevalidateService>().Verify(x => x.RevalidateMetricsAsync(It.IsAny<CancellationToken>()), Times.Once);
        mock.Mock<IPublicMetricsInvalidationRetryService>().Verify(x => x.ScheduleRetry(), Times.Never);
    }

    [Fact]
    public async Task ItWillNotRevalidateWhenSnapshotIsSuperseded()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsService>().Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new PublicMetricsRefreshResult(false, PublicMetricsResponse.Empty()));
        var context = CreateContext();
        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);
        mock.Mock<IFrontendRevalidateService>().Verify(x => x.RevalidateMetricsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillScheduleCheapInvalidationRetryWhenTheFrontendIsNotReady()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsService>().Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new PublicMetricsRefreshResult(true, PublicMetricsResponse.Empty()));
        mock.Mock<IFrontendRevalidateService>().Setup(x => x.RevalidateMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var context = CreateContext();

        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);

        mock.Mock<IPublicMetricsInvalidationRetryService>().Verify(x => x.ScheduleRetry(), Times.Once);
    }

    [Fact]
    public async Task ItWillPassTheReconciliationDecreaseAuthorizationToTheMetricsService()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IPublicMetricsService>().Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(new PublicMetricsRefreshResult(false, PublicMetricsResponse.Empty()));
        var context = CreateContext(allowReconciliationDecrease: true);

        await mock.Create<PublicMetricsRefreshConsumer>().Consume(context.Object);

        mock.Mock<IPublicMetricsService>().Verify(x => x.RefreshAsync(It.IsAny<CancellationToken>(), true), Times.Once);
    }

    private static Mock<ConsumeContext<RefreshPublicMetrics>> CreateContext(bool allowReconciliationDecrease = false)
    {
        var context = new Mock<ConsumeContext<RefreshPublicMetrics>>();
        context.SetupGet(x => x.Message).Returns(new RefreshPublicMetrics(allowReconciliationDecrease));
        return context;
    }
}
