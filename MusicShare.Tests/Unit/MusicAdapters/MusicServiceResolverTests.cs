using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services;

namespace MusicShare.Tests.Unit.MusicAdapters;

public class MusicServiceResolverTests
{
    private readonly MusicServiceResolver _sut;

    public MusicServiceResolverTests()
    {
        var spotifyAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.Spotify);
        var appleMusicAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.AppleMusic);
        var youTubeMusicAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.YouTubeMusic);

        _sut = new MusicServiceResolver([spotifyAdapter, appleMusicAdapter, youTubeMusicAdapter]);
    }

    #region DetectServiceType Tests

    [Theory]
    [InlineData("https://open.spotify.com/track/abc123")]
    [InlineData("https://open.spotify.com/album/xyz")]
    [InlineData("https://play.spotify.com/track/123")]
    [InlineData("spotify:track:abc123")]
    public void ItWillReturnSpotifyForSpotifyUrl(string url)
    {
        // Act
        var result = _sut.DetectServiceType(url);

        // Assert
        result.Should().Be(ServiceType.Spotify);
    }

    [Theory]
    [InlineData("https://music.apple.com/us/album/song/123")]
    [InlineData("https://music.apple.com/gb/song/test/456")]
    public void ItWillReturnAppleMusicForAppleMusicUrl(string url)
    {
        // Act
        var result = _sut.DetectServiceType(url);

        // Assert
        result.Should().Be(ServiceType.AppleMusic);
    }

    [Theory]
    [InlineData("https://music.youtube.com/watch?v=abc123")]
    [InlineData("https://www.youtube.com/watch?v=abc123")]
    [InlineData("https://youtube.com/watch?v=abc123")]
    [InlineData("https://youtu.be/abc123")]
    public void ItWillReturnYouTubeMusicForYouTubeMusicUrl(string url)
    {
        // Act
        var result = _sut.DetectServiceType(url);

        // Assert
        result.Should().Be(ServiceType.YouTubeMusic);
    }

    [Theory]
    [InlineData("https://example.com/track/123")]
    [InlineData("https://soundcloud.com/artist/track")]
    [InlineData("https://tidal.com/browse/track/123")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void ItWillReturnNullForUnsupportedUrl(string url)
    {
        // Act
        var result = _sut.DetectServiceType(url);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAdapter Tests

    [Fact]
    public void ItWillReturnSpotifyAdapterForSpotify()
    {
        // Act
        var adapter = _sut.GetAdapter(ServiceType.Spotify);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.Spotify);
    }

    [Fact]
    public void ItWillReturnAppleMusicAdapterForAppleMusic()
    {
        // Act
        var adapter = _sut.GetAdapter(ServiceType.AppleMusic);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.AppleMusic);
    }

    [Fact]
    public void ItWillReturnYouTubeMusicAdapterForYouTubeMusic()
    {
        // Act
        var adapter = _sut.GetAdapter(ServiceType.YouTubeMusic);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.YouTubeMusic);
    }

    [Fact]
    public void ItWillReturnNullForUnknown()
    {
        // Act
        var adapter = _sut.GetAdapter(ServiceType.Unknown);

        // Assert
        adapter.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForUnregisteredServiceType()
    {
        // Arrange - create resolver with only Spotify
        var spotifyAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.Spotify);
        var resolver = new MusicServiceResolver([spotifyAdapter]);

        // Act
        var adapter = resolver.GetAdapter(ServiceType.AppleMusic);

        // Assert
        adapter.Should().BeNull();
    }

    #endregion

    #region GetAllAdapters Tests

    [Fact]
    public void ItWillReturnAllRegisteredAdapters()
    {
        // Act
        var adapters = _sut.GetAllAdapters().ToList();

        // Assert
        adapters.Should().HaveCount(3);
        adapters.Select(a => a.ServiceType).Should()
            .Contain(ServiceType.Spotify)
            .And.Contain(ServiceType.AppleMusic)
            .And.Contain(ServiceType.YouTubeMusic);
    }

    [Fact]
    public void ItWillReturnEmptyForEmptyResolver()
    {
        // Arrange
        var resolver = new MusicServiceResolver([]);

        // Act
        var adapters = resolver.GetAllAdapters();

        // Assert
        adapters.Should().BeEmpty();
    }

    #endregion

    #region GetOtherAdapters Tests

    [Fact]
    public void ItWillReturnAppleAndYouTubeWhenExcludingSpotify()
    {
        // Act
        var adapters = _sut.GetOtherAdapters(ServiceType.Spotify).ToList();

        // Assert
        adapters.Should().HaveCount(2);
        adapters.Select(a => a.ServiceType).Should()
            .Contain(ServiceType.AppleMusic)
            .And.Contain(ServiceType.YouTubeMusic)
            .And.NotContain(ServiceType.Spotify);
    }

    [Fact]
    public void ItWillReturnSpotifyAndYouTubeWhenExcludingAppleMusic()
    {
        // Act
        var adapters = _sut.GetOtherAdapters(ServiceType.AppleMusic).ToList();

        // Assert
        adapters.Should().HaveCount(2);
        adapters.Select(a => a.ServiceType).Should()
            .Contain(ServiceType.Spotify)
            .And.Contain(ServiceType.YouTubeMusic)
            .And.NotContain(ServiceType.AppleMusic);
    }

    [Fact]
    public void ItWillReturnSpotifyAndAppleWhenExcludingYouTubeMusic()
    {
        // Act
        var adapters = _sut.GetOtherAdapters(ServiceType.YouTubeMusic).ToList();

        // Assert
        adapters.Should().HaveCount(2);
        adapters.Select(a => a.ServiceType).Should()
            .Contain(ServiceType.Spotify)
            .And.Contain(ServiceType.AppleMusic)
            .And.NotContain(ServiceType.YouTubeMusic);
    }

    [Fact]
    public void ItWillReturnAllWhenExcludingNonExistent()
    {
        // Act
        var adapters = _sut.GetOtherAdapters(ServiceType.Unknown).ToList();

        // Assert
        adapters.Should().HaveCount(3);
    }

    #endregion
}
