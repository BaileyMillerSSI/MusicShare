using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Services.Services.Music.YouTube;
using YouTubeMusicAPI.Client;

namespace MusicShare.Tests.Unit.Services.Music;

public class YouTubeMusicAdapterTests
{
    private static YouTubeMusicAdapter CreateSut()
    {
        var client = new YouTubeMusicClient(
            logger: Mock.Of<ILogger>(),
            geographicalLocation: "US",
            httpClient: new HttpClient());
        return new YouTubeMusicAdapter(Mock.Of<ILogger<YouTubeMusicAdapter>>(), client);
    }

    [Fact]
    public void ItWillReturnYouTubeMusicServiceType()
    {
        var sut = CreateSut();
        sut.ServiceType.Should().Be(ServiceType.YouTubeMusic);
    }

    #region ExtractSongId Tests

    [Fact]
    public void ItWillReturnVideoIdForMusicYouTubeWatchUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.youtube.com/watch?v=dQw4w9WgXcQ");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnVideoIdForStandardYouTubeWatchUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://youtube.com/watch?v=dQw4w9WgXcQ");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnVideoIdForWatchUrlWithAdditionalParams()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.youtube.com/watch?v=abc123&list=PLxyz");

        result.Should().Be("abc123");
    }

    [Fact]
    public void ItWillReturnVideoIdForShortUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://youtu.be/dQw4w9WgXcQ");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnVideoIdForShortUrlWithParams()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://youtu.be/dQw4w9WgXcQ?t=10");

        result.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnNullForNonYouTubeUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://open.spotify.com/track/abc");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForEmptyString()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForNullOrWhitespace()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("   ");

        result.Should().BeNull();
    }

    #endregion

    #region NormalizeUrl Tests

    [Fact]
    public void ItWillReturnCanonicalUrlForMusicYouTubeUrl()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://music.youtube.com/watch?v=abc123");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnYouTubeMusicUrlForStandardYouTubeUrl()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://youtube.com/watch?v=abc123");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnYouTubeMusicUrlForShortUrl()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://youtu.be/abc123");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnCleanUrlForUrlWithExtraParams()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://music.youtube.com/watch?v=abc123&list=PLxyz");

        result.Should().Be("https://music.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void ItWillReturnOriginalForUnrecognizedUrl()
    {
        var sut = CreateSut();
        var url = "https://random-site.com/video";
        var result = sut.NormalizeUrl(url);

        result.Should().Be(url);
    }

    #endregion
}
