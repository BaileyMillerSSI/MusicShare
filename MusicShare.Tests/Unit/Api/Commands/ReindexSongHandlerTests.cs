using MusicShare.Api.Commands;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Commands;

public class ReindexSongHandlerTests
{
    [Fact]
    public async Task ItWillReturnSuccessWhenSongCanBeReindexed()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestService>()
            .Setup(x => x.ReindexSongAsync("507f1f77bcf86cd799439011", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = mock.Create<ReindexSong.Handler>();

        // Act
        var result = await sut.Handle(new ReindexSong.Request("507f1f77bcf86cd799439011"), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Found.Should().BeTrue();
    }

    [Fact]
    public async Task ItWillReturnNotFoundWhenSongCannotBeReindexed()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestService>()
            .Setup(x => x.ReindexSongAsync("507f1f77bcf86cd799439011", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = mock.Create<ReindexSong.Handler>();

        // Act
        var result = await sut.Handle(new ReindexSong.Request("507f1f77bcf86cd799439011"), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Found.Should().BeFalse();
    }
}
