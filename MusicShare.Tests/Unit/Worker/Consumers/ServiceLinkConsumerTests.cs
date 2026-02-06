using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services;
using MusicShare.Worker.Consumers;

namespace MusicShare.Tests.Unit.Worker.Consumers;

public class SpotifyLinkConsumerUnitTests
{
    [Fact]
    public void ItWillReturnSpotifyAdapter()
    {
        // Arrange
        var mocker = new AutoMocker();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        adapterMock.Setup(x => x.ServiceType).Returns(ServiceType.Spotify);
        mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        mocker.CreateInstance<SpotifyLinkConsumer>();

        // Act
        var adapter = mocker.GetMock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.Spotify);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.Spotify);
    }

    [Fact]
    public void ItWillReturnNullWhenNoAdapterRegistered()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns((IMusicServiceAdapter?)null);

        mocker.CreateInstance<SpotifyLinkConsumer>();

        // Act & Assert
        mocker.GetMock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.Spotify).Should().BeNull();
    }
}

public class AppleMusicLinkConsumerUnitTests
{
    [Fact]
    public void ItWillReturnAppleMusicAdapter()
    {
        // Arrange
        var mocker = new AutoMocker();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        adapterMock.Setup(x => x.ServiceType).Returns(ServiceType.AppleMusic);
        mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.AppleMusic))
            .Returns(adapterMock.Object);

        mocker.CreateInstance<AppleMusicLinkConsumer>();

        // Act
        var adapter = mocker.GetMock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.AppleMusic);

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
        var mocker = new AutoMocker();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        adapterMock.Setup(x => x.ServiceType).Returns(ServiceType.YouTubeMusic);
        mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.YouTubeMusic))
            .Returns(adapterMock.Object);

        mocker.CreateInstance<YouTubeMusicLinkConsumer>();

        // Act
        var adapter = mocker.GetMock<IMusicServiceResolver>().Object.GetAdapter(ServiceType.YouTubeMusic);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.YouTubeMusic);
    }
}
