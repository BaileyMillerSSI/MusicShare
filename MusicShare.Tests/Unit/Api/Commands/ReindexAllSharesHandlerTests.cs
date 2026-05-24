using MusicShare.Api.Commands;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Commands;

public class ReindexAllSharesHandlerTests
{
    [Fact]
    public async Task ItWillReturnReindexedCount()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestService>()
            .Setup(x => x.ReindexAllCompletedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var sut = mock.Create<ReindexAllShares.Handler>();

        // Act
        var result = await sut.Handle(new ReindexAllShares.Request(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Count.Should().Be(3);
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToService()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var cts = new CancellationTokenSource();
        mock.Mock<IShareRequestService>()
            .Setup(x => x.ReindexAllCompletedAsync(cts.Token))
            .ReturnsAsync(0);

        var sut = mock.Create<ReindexAllShares.Handler>();

        // Act
        await sut.Handle(new ReindexAllShares.Request(), cts.Token);

        // Assert
        mock.Mock<IShareRequestService>().Verify(
            x => x.ReindexAllCompletedAsync(cts.Token),
            Times.Once);
    }
}
