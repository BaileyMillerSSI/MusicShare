using MusicShare.Api.Models;
using MusicShare.Api.Queries;
using MusicShare.Api.Services;
using MusicShare.Contracts;

namespace MusicShare.Tests.Unit.Api.Queries;

public class GetShareResultHandlerTests
{
    [Fact]
    public async Task ItWillReturnSuccessResultForExistingShareId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareId = "abc123";
        var query = new GetShareResult.Query(shareId);
        var expectedResponse = new ShareResultResponse
        {
            ShareId = shareId,
            Status = "Completed",
            Song = null
        };
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetByShareIdAsync(shareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var sut = mock.Create<GetShareResult.Handler>();

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Found.Should().BeTrue();
        result.Response.Should().NotBeNull();
        result.Response!.ShareId.Should().Be(shareId);
        result.Response.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task ItWillReturnNotFoundResultForNonExistingShareId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var query = new GetShareResult.Query("nonexistent");
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetByShareIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareResultResponse?)null);

        var sut = mock.Create<GetShareResult.Handler>();

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Found.Should().BeFalse();
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnFullResultForShareWithSongDetails()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareId = "share-with-song";
        var query = new GetShareResult.Query(shareId);
        var expectedResponse = new ShareResultResponse
        {
            ShareId = shareId,
            Status = "Completed",
            Song = new SongDetails
            {
                Id = "song-1",
                Title = "Test Song",
                Artists = ["Artist One"],
                Album = "Test Album",
                ArtworkUrl = "https://example.com/art.jpg",
                Duration = TimeSpan.FromMinutes(3),
                IsExplicit = false,
                Status = "Resolved",
                Links = [
                    new ServiceLink { ServiceType = ServiceType.Spotify, Url = "https://open.spotify.com/track/123" }
                ]
            }
        };
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetByShareIdAsync(shareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var sut = mock.Create<GetShareResult.Handler>();

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Found.Should().BeTrue();
        result.Response!.Song.Should().NotBeNull();
        result.Response.Song!.Title.Should().Be("Test Song");
        result.Response.Song.Artists.Should().ContainSingle().Which.Should().Be("Artist One");
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToService()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var cts = new CancellationTokenSource();
        var query = new GetShareResult.Query("test-id");
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetByShareIdAsync("test-id", cts.Token))
            .ReturnsAsync((ShareResultResponse?)null);

        var sut = mock.Create<GetShareResult.Handler>();

        // Act
        await sut.Handle(query, cts.Token);

        // Assert
        mock.Mock<IShareRequestService>().Verify(
            x => x.GetByShareIdAsync("test-id", cts.Token),
            Times.Once);
    }
}
