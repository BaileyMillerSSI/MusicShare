using MusicShare.Contracts;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;

namespace MusicShare.Tests.Unit.Services;

public class MusicServiceResolverTests
{
    private static MusicServiceResolver CreateResolverWithAllAdapters()
    {
        var spotifyAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.Spotify);
        var appleMusicAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.AppleMusic);
        var youTubeMusicAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.YouTubeMusic);

        return new MusicServiceResolver([spotifyAdapter, appleMusicAdapter, youTubeMusicAdapter]);
    }

    #region DetectServiceType Tests

    [Theory]
    [InlineData("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("https://play.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("spotify:track:6rqhFgbbKwnb9MLmUQDhG6")]
    public void ItWillReturnSpotifyForSpotifyUrl(string url)
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var result = sut.DetectServiceType(url);

        // Assert
        result.Should().Be(ServiceType.Spotify);
    }

    [Theory]
    [InlineData("https://music.apple.com/us/album/song/123")]
    [InlineData("https://music.apple.com/gb/song/test/456")]
    public void ItWillReturnAppleMusicForAppleMusicUrl(string url)
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var result = sut.DetectServiceType(url);

        // Assert
        result.Should().Be(ServiceType.AppleMusic);
    }

    [Theory]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    public void ItWillReturnYouTubeMusicForYouTubeMusicUrl(string url)
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var result = sut.DetectServiceType(url);

        // Assert
        result.Should().Be(ServiceType.YouTubeMusic);
    }

    [Theory]
    [InlineData("https://example.com/track/123")]
    [InlineData("https://soundcloud.com/artist/track")]
    [InlineData("https://tidal.com/browse/track/123")]
    [InlineData("https://spotify.com.attacker.example/track/6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("https://open.spotify.com/album/xyz")]
    [InlineData("https://open.spotify.com/track/short")]
    [InlineData("https://attacker.example/?next=youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=short")]
    [InlineData("http://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("ftp://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("spotify:track:invalid")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void ItWillReturnNullForUnsupportedUrl(string url)
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var result = sut.DetectServiceType(url);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAdapter Tests

    [Fact]
    public void ItWillReturnSpotifyAdapterForSpotify()
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var adapter = sut.GetAdapter(ServiceType.Spotify);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.Spotify);
    }

    [Fact]
    public void ItWillReturnAppleMusicAdapterForAppleMusic()
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var adapter = sut.GetAdapter(ServiceType.AppleMusic);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.AppleMusic);
    }

    [Fact]
    public void ItWillReturnYouTubeMusicAdapterForYouTubeMusic()
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var adapter = sut.GetAdapter(ServiceType.YouTubeMusic);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ServiceType.Should().Be(ServiceType.YouTubeMusic);
    }

    [Fact]
    public void ItWillReturnNullForUnknown()
    {
        // Act
        var sut = CreateResolverWithAllAdapters();
        var adapter = sut.GetAdapter(ServiceType.Unknown);

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
        var sut = CreateResolverWithAllAdapters();
        var adapters = sut.GetAllAdapters().ToList();

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
        var sut = CreateResolverWithAllAdapters();
        var adapters = sut.GetOtherAdapters(ServiceType.Spotify).ToList();

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
        var sut = CreateResolverWithAllAdapters();
        var adapters = sut.GetOtherAdapters(ServiceType.AppleMusic).ToList();

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
        var sut = CreateResolverWithAllAdapters();
        var adapters = sut.GetOtherAdapters(ServiceType.YouTubeMusic).ToList();

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
        var sut = CreateResolverWithAllAdapters();
        var adapters = sut.GetOtherAdapters(ServiceType.Unknown).ToList();

        // Assert
        adapters.Should().HaveCount(3);
    }

    #endregion
}
