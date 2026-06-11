using System.Reflection;
using System.Runtime.CompilerServices;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services.Music.YouTube;
using YouTubeMusicAPI.Models;
using YouTubeMusicAPI.Models.Info;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.Tests.Unit.Services.Music;

public class YouTubeMusicAdapterTests
{
    private static SongVideoInfo? CreateSongVideoInfo(string name = "", string album = "", TimeSpan? duration = null, params string[] artists) =>
        new(
            name,
            name,
            name,
            "description",
            [.. artists.Select(x => new NamedEntity(x, x))],
            new NamedEntity(album, album),
            duration ?? TimeSpan.Zero,
            null,
            new PlayabilityStatus(true, string.Empty),
            false,
            false,
            false,
            false,
            false,
            false,
            0,
            DateTime.Today,
            DateTime.Today,
            null,
            [],
            []);

    #region ServiceType

    [Fact]
    public void ItWillReturnYouTubeMusicServiceType()
    {
        using var mock = AutoMock.GetLoose();
        mock.Create<YouTubeMusicAdapter>().ServiceType.Should().Be(ServiceType.YouTubeMusic);
    }

    #endregion

    #region ExtractSongId

    [Fact]
    public void ItWillExtractVideoIdFromMusicWatchUrl()
    {
        using var mock = AutoMock.GetLoose();
        mock.Create<YouTubeMusicAdapter>()
            .ExtractSongId("https://music.youtube.com/watch?v=dQw4w9WgXcQ")
            .Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillExtractVideoIdFromWatchUrlWithExtraParams()
    {
        using var mock = AutoMock.GetLoose();
        mock.Create<YouTubeMusicAdapter>()
            .ExtractSongId("https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=PL123")
            .Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillExtractVideoIdFromShortUrl()
    {
        using var mock = AutoMock.GetLoose();
        mock.Create<YouTubeMusicAdapter>()
            .ExtractSongId("https://youtu.be/dQw4w9WgXcQ?si=abc")
            .Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnNullExtractedIdForNonYouTubeUrl()
    {
        using var mock = AutoMock.GetLoose();
        mock.Create<YouTubeMusicAdapter>()
            .ExtractSongId("https://open.spotify.com/track/abc123")
            .Should().BeNull();
    }

    [Theory]
    [InlineData("https://attacker.example/?next=youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com.attacker.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=abc123")]
    [InlineData("https://music.youtube.com/playlist?list=dQw4w9WgXcQ")]
    public void ItWillReturnNullExtractedIdForMalformedOrSpoofedYouTubeUrl(string url)
    {
        using var mock = AutoMock.GetLoose();
        mock.Create<YouTubeMusicAdapter>()
            .ExtractSongId(url)
            .Should().BeNull();
    }

    #endregion

    #region NormalizeUrl

    [Fact]
    public void ItWillNormalizeShortUrlToCanonicalForm()
    {
        using var mock = AutoMock.GetLoose();
        mock.Create<YouTubeMusicAdapter>()
            .NormalizeUrl("https://youtu.be/dQw4w9WgXcQ")
            .Should().Be("https://music.youtube.com/watch?v=dQw4w9WgXcQ");
    }

    [Fact]
    public void ItWillReturnOriginalUrlForUnrecognisedUrl()
    {
        using var mock = AutoMock.GetLoose();
        const string url = "https://open.spotify.com/track/abc123";
        mock.Create<YouTubeMusicAdapter>()
            .NormalizeUrl(url)
            .Should().Be(url);
    }

    #endregion

    #region FindSongsAsync

    [Fact]
    public async Task ItWillReturnEmptyWhenServiceReturnsNoResults()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var results = await mock.Create<YouTubeMusicAdapter>()
            .FindSongsAsync(new SongMetadata { Title = "Song", Artists = ["Artist"] }, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillReturnMappedResultForMatchingSong()
    {
        using var mock = AutoMock.GetLoose();

        var ytResult = new SongSearchResult(
            name: "Never Gonna Give You Up",
            id: "dQw4w9WgXcQ",
            artists: [new NamedEntity("Rick Astley", "artistId")],
            album: null!,
            duration: TimeSpan.FromSeconds(213),
            isExplicit: false,
            playsInfo: null!,
            radio: null!,
            thumbnails: []);

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ytResult]);

        var results = await mock.Create<YouTubeMusicAdapter>()
            .FindSongsAsync(new SongMetadata { Title = "Never Gonna Give You Up", Artists = ["Rick Astley"] }, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        var result = results.Should().ContainSingle().Subject;
        result.Url.Should().Be("https://music.youtube.com/watch?v=dQw4w9WgXcQ");
        result.FoundMetadata.Title.Should().Be("Never Gonna Give You Up");
        result.FoundMetadata.Artists.Should().Contain("Rick Astley");
    }

    #endregion

    #region ResolveMetadataAsync

    [Fact]
    public async Task ItWillReturnNullForNonYouTubeUrl()
    {
        using var mock = AutoMock.GetLoose();
        var result = await mock.Create<YouTubeMusicAdapter>()
            .ResolveMetadataAsync("https://open.spotify.com/track/abc123", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnNullWhenServiceReturnsNull()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.GetSongVideoInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongVideoInfo?)null);

        var result = await mock.Create<YouTubeMusicAdapter>()
            .ResolveMetadataAsync("https://music.youtube.com/watch?v=dQw4w9WgXcQ", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnMappedMetadataFromVideoInfo()
    {
        using var mock = AutoMock.GetLoose();

        var info = CreateSongVideoInfo("Never Gonna Give You Up", duration: TimeSpan.FromSeconds(213), artists: ["Rick Astley"]);

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.GetSongVideoInfoAsync("dQw4w9WgXcQ", It.IsAny<CancellationToken>()))
            .ReturnsAsync(info);

        var result = await mock.Create<YouTubeMusicAdapter>()
            .ResolveMetadataAsync("https://music.youtube.com/watch?v=dQw4w9WgXcQ", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Never Gonna Give You Up");
        result.Artists.Should().Contain("Rick Astley");
        result.Duration.Should().Be(TimeSpan.FromSeconds(213));
    }

    #endregion
}
