using MusicShare.Contracts;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;
using MusicShare.Api.Consumers;

namespace MusicShare.Tests.Unit.Worker.Consumers;

public class SpotifyLinkConsumerUnitTests
{
    [Fact]
    public void ItWillReturnSpotifyAdapter()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        adapterMock.Setup(x => x.ServiceType).Returns(ServiceType.Spotify);
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        mock.Create<SpotifyLinkConsumer>();

        // Act
        var adapter = mock.Mock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.Spotify);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.Spotify);
    }

    [Fact]
    public void ItWillReturnNullWhenNoAdapterRegistered()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns((IMusicServiceAdapter?)null);

        mock.Create<SpotifyLinkConsumer>();

        // Act & Assert
        mock.Mock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.Spotify).Should().BeNull();
    }
}

public class AppleMusicLinkConsumerUnitTests
{
    [Fact]
    public void ItWillReturnAppleMusicAdapter()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        adapterMock.Setup(x => x.ServiceType).Returns(ServiceType.AppleMusic);
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.AppleMusic))
            .Returns(adapterMock.Object);

        mock.Create<AppleMusicLinkConsumer>();

        // Act
        var adapter = mock.Mock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.AppleMusic);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.AppleMusic);
    }
}

public class YouTubeMusicLinkConsumerUnitTests
{
    [Fact]
    public void ItWillReturnYouTubeMusicAdapter()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        adapterMock.Setup(x => x.ServiceType).Returns(ServiceType.YouTubeMusic);
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.YouTubeMusic))
            .Returns(adapterMock.Object);

        mock.Create<YouTubeMusicLinkConsumer>();

        // Act
        var adapter = mock.Mock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.YouTubeMusic);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.YouTubeMusic);
    }
}
