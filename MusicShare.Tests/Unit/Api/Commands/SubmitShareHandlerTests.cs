using MusicShare.Api.Commands;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services;

namespace MusicShare.Tests.Unit.Api.Commands;

public class SubmitShareHandlerTests
{
    [Fact]
    public async Task ItWillReturnSuccessResponseForValidSpotifyUrl()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://open.spotify.com/track/123");
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.Spotify);
        mock.Mock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123def456");

        var sut = mock.Create<SubmitShare.Handler>();

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ShareId.Should().Be("abc123def456");
        result.Status.Should().Be("Pending");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnSuccessResponseForValidAppleMusicUrl()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://music.apple.com/us/album/song/123");
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.AppleMusic);
        mock.Mock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.AppleMusic, It.IsAny<CancellationToken>()))
            .ReturnsAsync("share-apple-1");

        var sut = mock.Create<SubmitShare.Handler>();

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ShareId.Should().Be("share-apple-1");
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ItWillReturnSuccessResponseForValidYouTubeMusicUrl()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://music.youtube.com/watch?v=abc");
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.YouTubeMusic);
        mock.Mock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.YouTubeMusic, It.IsAny<CancellationToken>()))
            .ReturnsAsync("share-yt-1");

        var sut = mock.Create<SubmitShare.Handler>();

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ShareId.Should().Be("share-yt-1");
    }

    [Fact]
    public async Task ItWillReturnFailureResponseForUnsupportedUrl()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://unknown-service.com/track/123");
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns((ServiceType?)null);

        var sut = mock.Create<SubmitShare.Handler>();

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Unsupported music service URL");
        result.ShareId.Should().BeNull();
        result.Status.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnFailureResponseForUnknownServiceType()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://example.com/track");
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.Unknown);

        var sut = mock.Create<SubmitShare.Handler>();

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Unsupported music service URL");
    }

    [Fact]
    public async Task ItWillNotCallShareRequestServiceForUnsupportedUrl()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://unknown.com/track");
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns((ServiceType?)null);

        var sut = mock.Create<SubmitShare.Handler>();

        // Act
        await sut.Handle(request, CancellationToken.None);

        // Assert
        mock.Mock<IShareRequestService>().Verify(
            x => x.Create(It.IsAny<string>(), It.IsAny<ServiceType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToServiceForValidUrl()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://open.spotify.com/track/123");
        var cts = new CancellationTokenSource();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.Spotify);
        mock.Mock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.Spotify, cts.Token))
            .ReturnsAsync("share-123");

        var sut = mock.Create<SubmitShare.Handler>();

        // Act
        await sut.Handle(request, cts.Token);

        // Assert
        mock.Mock<IShareRequestService>().Verify(
            x => x.Create(request.Url, ServiceType.Spotify, cts.Token),
            Times.Once);
    }
}
