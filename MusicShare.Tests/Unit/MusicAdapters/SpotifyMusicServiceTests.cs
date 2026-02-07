using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services.Music.Spotify;

namespace MusicShare.Tests.Unit.MusicAdapters;

public class SpotifyMusicServiceTests
{
    private static SpotifyMusicService CreateSut() => new(new HttpClient());

    [Fact]
    public void ItWillReturnSpotifyServiceType()
    {
        var sut = CreateSut();
        sut.ServiceType.Should().Be(ServiceType.Spotify);
    }

    #region ExtractSongId Tests

    [Fact]
    public void ItWillReturnTrackIdForStandardTrackUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6");

        result.Should().Be("6rqhFgbbKwnb9MLmUQDhG6");
    }

    [Fact]
    public void ItWillReturnTrackIdWithoutParamsForTrackUrlWithQueryParams()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6?si=abc123");

        result.Should().Be("6rqhFgbbKwnb9MLmUQDhG6");
    }

    [Fact]
    public void ItWillReturnTrackIdForSpotifyUri()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("spotify:track:6rqhFgbbKwnb9MLmUQDhG6");

        result.Should().Be("6rqhFgbbKwnb9MLmUQDhG6");
    }

    [Fact]
    public void ItWillReturnNullForAlbumUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://open.spotify.com/album/abc123");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForNonSpotifyUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.apple.com/us/song/123");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForEmptyString()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("");

        result.Should().BeNull();
    }

    #endregion

    #region NormalizeUrl Tests

    [Fact]
    public void ItWillReturnCanonicalUrlForStandardUrl()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://open.spotify.com/track/abc123");

        result.Should().Be("https://open.spotify.com/track/abc123");
    }

    [Fact]
    public void ItWillReturnCleanUrlForUrlWithQueryParams()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://open.spotify.com/track/abc123?si=extra_params");

        result.Should().Be("https://open.spotify.com/track/abc123");
    }

    [Fact]
    public void ItWillReturnWebUrlForSpotifyUri()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("spotify:track:abc123");

        result.Should().Be("https://open.spotify.com/track/abc123");
    }

    [Fact]
    public void ItWillReturnOriginalUrlForNonTrackUrl()
    {
        var sut = CreateSut();
        var url = "https://open.spotify.com/album/xyz";
        var result = sut.NormalizeUrl(url);

        result.Should().Be(url);
    }

    #endregion
}
