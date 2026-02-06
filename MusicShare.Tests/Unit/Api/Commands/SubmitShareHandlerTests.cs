using MusicShare.Api.Commands;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services;

namespace MusicShare.Tests.Unit.Api.Commands;

public class SubmitShareHandlerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly SubmitShare.Handler _sut;

    public SubmitShareHandlerTests()
    {
        _sut = _mocker.CreateInstance<SubmitShare.Handler>();
    }

    [Fact]
    public async Task ItWillReturnSuccessResponseForValidSpotifyUrl()
    {
        // Arrange
        var request = new SubmitShare.Request("https://open.spotify.com/track/123");
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.Spotify);
        _mocker.GetMock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123def456");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

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
        var request = new SubmitShare.Request("https://music.apple.com/us/album/song/123");
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.AppleMusic);
        _mocker.GetMock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.AppleMusic, It.IsAny<CancellationToken>()))
            .ReturnsAsync("share-apple-1");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ShareId.Should().Be("share-apple-1");
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ItWillReturnSuccessResponseForValidYouTubeMusicUrl()
    {
        // Arrange
        var request = new SubmitShare.Request("https://music.youtube.com/watch?v=abc");
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.YouTubeMusic);
        _mocker.GetMock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.YouTubeMusic, It.IsAny<CancellationToken>()))
            .ReturnsAsync("share-yt-1");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ShareId.Should().Be("share-yt-1");
    }

    [Fact]
    public async Task ItWillReturnFailureResponseForUnsupportedUrl()
    {
        // Arrange
        var request = new SubmitShare.Request("https://unknown-service.com/track/123");
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns((ServiceType?)null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

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
        var request = new SubmitShare.Request("https://example.com/track");
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.Unknown);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Unsupported music service URL");
    }

    [Fact]
    public async Task ItWillNotCallShareRequestServiceForUnsupportedUrl()
    {
        // Arrange
        var request = new SubmitShare.Request("https://unknown.com/track");
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns((ServiceType?)null);

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mocker.GetMock<IShareRequestService>().Verify(
            x => x.Create(It.IsAny<string>(), It.IsAny<ServiceType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToServiceForValidUrl()
    {
        // Arrange
        var request = new SubmitShare.Request("https://open.spotify.com/track/123");
        var cts = new CancellationTokenSource();
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.DetectServiceType(request.Url))
            .Returns(ServiceType.Spotify);
        _mocker.GetMock<IShareRequestService>()
            .Setup(x => x.Create(request.Url, ServiceType.Spotify, cts.Token))
            .ReturnsAsync("share-123");

        // Act
        await _sut.Handle(request, cts.Token);

        // Assert
        _mocker.GetMock<IShareRequestService>().Verify(
            x => x.Create(request.Url, ServiceType.Spotify, cts.Token),
            Times.Once);
    }
}
