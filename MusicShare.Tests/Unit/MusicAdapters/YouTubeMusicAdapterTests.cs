using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services.Music.YouTube;
using YouTubeMusicAPI.Client;

namespace MusicShare.Tests.Unit.MusicAdapters;

public class YouTubeMusicAdapterTests
{
    private readonly YouTubeMusicAdapter _sut;

    public YouTubeMusicAdapterTests()
    {
        var mocker = new AutoMocker();
        mocker.Use(new YouTubeMusicClient(
            logger: Mock.Of<ILogger>(),
            geographicalLocation: "US",
            httpClient: new HttpClient()));
        _sut = mocker.CreateInstance<YouTubeMusicAdapter>();
    }

    [Fact]
    public void ItWillReturnYouTubeMusicServiceType()
    {
        _sut.ServiceType.Should().Be(ServiceType.YouTubeMusic);
    }

    #region ExtractSongId Tests

    [Fact]
    public void ItWillReturnVideoIdForMusicYouTubeWatchUrl()
    {
        var result = _sut.ExtractSongId("https://music.youtube.com/watch?v=dQw4w9WgXcQ");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnVideoIdForStandardYouTubeWatchUrl()
    {
        var result = _sut.ExtractSongId("https://youtube.com/watch?v=dQw4w9WgXcQ");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnVideoIdForWatchUrlWithAdditionalParams()
    {
        var result = _sut.ExtractSongId("https://music.youtube.com/watch?v=abc123&list=PLxyz");

        result.Should().Be("abc123");
    }

    [Fact]
    public void ItWillReturnVideoIdForShortUrl()
    {
        var result = _sut.ExtractSongId("https://youtu.be/dQw4w9WgXcQ");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnVideoIdForShortUrlWithParams()
    {
        var result = _sut.ExtractSongId("https://youtu.be/dQw4w9WgXcQ?t=10");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnNullForNonYouTubeUrl()
    {
        var result = _sut.ExtractSongId("https://open.spotify.com/track/abc");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForEmptyString()
    {
        var result = _sut.ExtractSongId("");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForNullOrWhitespace()
    {
        var result = _sut.ExtractSongId("   ");

        result.Should().BeNull();
    }

    #endregion

    #region NormalizeUrl Tests

    [Fact]
    public void ItWillReturnCanonicalUrlForMusicYouTubeUrl()
    {
        var result = _sut.NormalizeUrl("https://music.youtube.com/watch?v=abc123");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnYouTubeMusicUrlForStandardYouTubeUrl()
    {
        var result = _sut.NormalizeUrl("https://youtube.com/watch?v=abc123");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnYouTubeMusicUrlForShortUrl()
    {
        var result = _sut.NormalizeUrl("https://youtu.be/abc123");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnCleanUrlForUrlWithExtraParams()
    {
        var result = _sut.NormalizeUrl("https://music.youtube.com/watch?v=abc123&list=PLxyz");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnOriginalForUnrecognizedUrl()
    {
        var url = "https://random-site.com/video";
        var result = _sut.NormalizeUrl(url);

        result.Should().Be(url);
    }

    #endregion
}
