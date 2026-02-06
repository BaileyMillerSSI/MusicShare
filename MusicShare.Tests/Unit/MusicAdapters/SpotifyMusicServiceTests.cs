using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services.Music.Spotify;

namespace MusicShare.Tests.Unit.MusicAdapters;

public class SpotifyMusicServiceTests
{
    private readonly SpotifyMusicService _sut;

    public SpotifyMusicServiceTests()
    {
        _sut = new SpotifyMusicService(new HttpClient());
    }

    [Fact]
    public void ItWillReturnSpotifyServiceType()
    {
        _sut.ServiceType.Should().Be(ServiceType.Spotify);
    }

    #region ExtractSongId Tests

    [Fact]
    public void ItWillReturnTrackIdForStandardTrackUrl()
    {
        var result = _sut.ExtractSongId("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6");

        result.Should().Be("6rqhFgbbKwnb9MLmUQDhG6");
    }

    [Fact]
    public void ItWillReturnTrackIdWithoutParamsForTrackUrlWithQueryParams()
    {
        var result = _sut.ExtractSongId("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6?si=abc123");

        result.Should().Be("6rqhFgbbKwnb9MLmUQDhG6");
    }

    [Fact]
    public void ItWillReturnTrackIdForSpotifyUri()
    {
        var result = _sut.ExtractSongId("spotify:track:6rqhFgbbKwnb9MLmUQDhG6");

        result.Should().Be("6rqhFgbbKwnb9MLmUQDhG6");
    }

    [Fact]
    public void ItWillReturnNullForAlbumUrl()
    {
        var result = _sut.ExtractSongId("https://open.spotify.com/album/abc123");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForNonSpotifyUrl()
    {
        var result = _sut.ExtractSongId("https://music.apple.com/us/song/123");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForEmptyString()
    {
        var result = _sut.ExtractSongId("");

        result.Should().BeNull();
    }

    #endregion

    #region NormalizeUrl Tests

    [Fact]
    public void ItWillReturnCanonicalUrlForStandardUrl()
    {
        var result = _sut.NormalizeUrl("https://open.spotify.com/track/abc123");

        result.Should().Be("https://open.spotify.com/track/abc123");
    }

    [Fact]
    public void ItWillReturnCleanUrlForUrlWithQueryParams()
    {
        var result = _sut.NormalizeUrl("https://open.spotify.com/track/abc123?si=extra_params");

        result.Should().Be("https://open.spotify.com/track/abc123");
    }

    [Fact]
    public void ItWillReturnWebUrlForSpotifyUri()
    {
        var result = _sut.NormalizeUrl("spotify:track:abc123");

        result.Should().Be("https://open.spotify.com/track/abc123");
    }

    [Fact]
    public void ItWillReturnOriginalUrlForNonTrackUrl()
    {
        var url = "https://open.spotify.com/album/xyz";
        var result = _sut.NormalizeUrl(url);

        result.Should().Be(url);
    }

    #endregion
}
