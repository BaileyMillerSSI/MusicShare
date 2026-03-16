using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class ShareStatusServiceTests
{
    private static ShareRequest CreateShareRequest(string shareId = "share-abc123") =>
        new() { ShareId = shareId, Status = ShareStatus.Processing };

    [Fact]
    public async Task ItWillUpdateShareRequestStatusToCompleted()
    {
        using var mock = AutoMock.GetLoose();
        var shareRequest = CreateShareRequest();

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var sut = mock.Create<ShareStatusService>();

        await sut.UpdateStatusAsync("share-abc123", ShareStatus.Completed, TestContext.Current.CancellationToken);

        shareRequest.Status.Should().Be(ShareStatus.Completed);
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.UpdateAsync(shareRequest, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillUpdateShareRequestStatusToFailed()
    {
        using var mock = AutoMock.GetLoose();
        var shareRequest = CreateShareRequest();

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var sut = mock.Create<ShareStatusService>();

        await sut.UpdateStatusAsync("share-abc123", ShareStatus.Failed, TestContext.Current.CancellationToken);

        shareRequest.Status.Should().Be(ShareStatus.Failed);
    }

    [Fact]
    public async Task ItWillSkipUpdateWhenShareRequestNotFound()
    {
        using var mock = AutoMock.GetLoose();

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        var sut = mock.Create<ShareStatusService>();

        await sut.UpdateStatusAsync("share-abc123", ShareStatus.Completed, TestContext.Current.CancellationToken);

        mock.Mock<IShareRequestRepository>().Verify(
            x => x.UpdateAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillLookupShareRequestByCorrectShareId()
    {
        using var mock = AutoMock.GetLoose();
        var shareRequest = CreateShareRequest("custom-share-id");

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("custom-share-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var sut = mock.Create<ShareStatusService>();

        await sut.UpdateStatusAsync("custom-share-id", ShareStatus.Completed, TestContext.Current.CancellationToken);

        mock.Mock<IShareRequestRepository>().Verify(
            x => x.GetByShareIdAsync("custom-share-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToRepository()
    {
        using var mock = AutoMock.GetLoose();
        var shareRequest = CreateShareRequest();
        var cts = new CancellationTokenSource();

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-abc123", cts.Token))
            .ReturnsAsync(shareRequest);

        var sut = mock.Create<ShareStatusService>();

        await sut.UpdateStatusAsync("share-abc123", ShareStatus.Completed, cts.Token);

        mock.Mock<IShareRequestRepository>().Verify(
            x => x.GetByShareIdAsync("share-abc123", cts.Token), Times.Once);
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.UpdateAsync(shareRequest, cts.Token), Times.Once);
    }
}
