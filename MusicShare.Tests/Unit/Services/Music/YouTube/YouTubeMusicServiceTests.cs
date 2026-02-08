using MusicShare.Services.Services.Music.YouTube;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models;
using YouTubeMusicAPI.Models.Info;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.Tests.Unit.Services.Music.YouTube;

public class YouTubeMusicServiceTests
{
    [Fact]
    public async Task ItWillReturnYouTubeResultsForValidQuery()
    {
        using var mock = AutoMock.GetLoose();
        var songResults = new List<SongSearchResult>
        {
            CreateMockSongSearchResult("videoId1", "Test Song 1", "Artist 1"),
            CreateMockSongSearchResult("videoId2", "Test Song 2", "Artist 2"),
            CreateMockSongSearchResult("videoId3", "Test Song 3", "Artist 3")
        };

        var mockSearchResults = new MockSearchResults(songResults);

        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), SearchCategory.Songs))
            .Returns(mockSearchResults);

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.SearchSongsAsync("test query", 10, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].VideoId.Should().Be("videoId1");
        result[0].Name.Should().Be("Test Song 1");
        result[1].VideoId.Should().Be("videoId2");
        result[2].VideoId.Should().Be("videoId3");
    }

    [Fact]
    public async Task ItWillReturnEmptyListForNoResults()
    {
        using var mock = AutoMock.GetLoose();
        var emptyResults = new MockSearchResults(new List<SongSearchResult>());

        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), SearchCategory.Songs))
            .Returns(emptyResults);

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.SearchSongsAsync("unknown query", 10, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillHandleYouTubeMusicApiError()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), SearchCategory.Songs))
            .Throws(new Exception("YouTube API error"));

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.SearchSongsAsync("test query", 10, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillReturnVideoInfoByIdForValidId()
    {
        using var mock = AutoMock.GetLoose();
        var videoInfo = CreateMockSongVideoInfo("videoId123", "Test Song", "Test Artist");

        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.GetSongVideoInfoAsync("videoId123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoInfo);

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.GetSongVideoInfoAsync("videoId123", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be("videoId123");
        result.Name.Should().Be("Test Song");
        result.Artists.Should().Contain(new Artist { Name = "Test Artist", Id = "artist123" });
    }

    [Fact]
    public async Task ItWillReturnNullForInvalidVideoId()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.GetSongVideoInfoAsync("invalid-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongVideoInfo?)null);

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.GetSongVideoInfoAsync("invalid-id", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillParseYouTubeResponseCorrectly()
    {
        using var mock = AutoMock.GetLoose();
        var songResults = new List<SongSearchResult>
        {
            CreateDetailedMockSongSearchResult(
                videoId: "detailed-1",
                name: "Stairway to Heaven",
                artists: new[] { "Led Zeppelin" },
                album: "Led Zeppelin IV",
                duration: TimeSpan.FromMinutes(8).Add(TimeSpan.FromSeconds(2)))
        };

        var mockSearchResults = new MockSearchResults(songResults);

        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), SearchCategory.Songs))
            .Returns(mockSearchResults);

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.SearchSongsAsync("Stairway to Heaven", 5, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var song = result[0];
        song.VideoId.Should().Be("detailed-1");
        song.Name.Should().Be("Stairway to Heaven");
        song.Artists.Should().HaveCount(1);
        song.Artists[0].Name.Should().Be("Led Zeppelin");
        song.Album.Should().NotBeNull();
        song.Album!.Name.Should().Be("Led Zeppelin IV");
        song.Duration.Should().Be(TimeSpan.FromMinutes(8).Add(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ItWillLimitResultsToMaxResults()
    {
        using var mock = AutoMock.GetLoose();

        // Create 20 results
        var allResults = Enumerable.Range(1, 20)
            .Select(i => CreateMockSongSearchResult($"video{i}", $"Song {i}", $"Artist {i}"))
            .ToList();

        var mockSearchResults = new MockSearchResults(allResults, maxResultsToReturn: 5);

        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), SearchCategory.Songs))
            .Returns(mockSearchResults);

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.SearchSongsAsync("test query", 5, TestContext.Current.CancellationToken);

        result.Should().HaveCount(5);
        result[0].VideoId.Should().Be("video1");
        result[4].VideoId.Should().Be("video5");
    }

    [Fact]
    public async Task ItWillFilterNonSongResults()
    {
        using var mock = AutoMock.GetLoose();

        // Mix of song and non-song results
        var mixedResults = new List<IYouTubeBaseItem>
        {
            CreateMockSongSearchResult("song1", "Song 1", "Artist 1"),
            Mock.Of<IYouTubeBaseItem>(), // Non-song result
            CreateMockSongSearchResult("song2", "Song 2", "Artist 2"),
            Mock.Of<IYouTubeBaseItem>()  // Another non-song result
        };

        var mockSearchResults = new MockSearchResults(mixedResults);

        mock.Mock<YouTubeMusicClient>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), SearchCategory.Songs))
            .Returns(mockSearchResults);

        var sut = mock.Create<YouTubeMusicService>();

        var result = await sut.SearchSongsAsync("test query", 10, TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result.Should().AllBeOfType<SongSearchResult>();
        result[0].VideoId.Should().Be("song1");
        result[1].VideoId.Should().Be("song2");
    }

    private static SongSearchResult CreateMockSongSearchResult(string videoId, string name, string artistName)
    {
        var artist = new Artist { Id = $"artist-{videoId}", Name = artistName };
        return new SongSearchResult
        {
            VideoId = videoId,
            Name = name,
            Artists = new List<Artist> { artist },
            Duration = TimeSpan.FromMinutes(3),
            Album = null,
            Thumbnails = new List<Thumbnail>()
        };
    }

    private static SongSearchResult CreateDetailedMockSongSearchResult(
        string videoId,
        string name,
        string[] artists,
        string album,
        TimeSpan duration)
    {
        return new SongSearchResult
        {
            VideoId = videoId,
            Name = name,
            Artists = artists.Select(a => new Artist { Id = $"artist-{a}", Name = a }).ToList(),
            Duration = duration,
            Album = new Album { Id = $"album-{videoId}", Name = album },
            Thumbnails = new List<Thumbnail>
            {
                new Thumbnail { Url = $"https://i.ytimg.com/vi/{videoId}/default.jpg", Height = 120, Width = 120 }
            }
        };
    }

    private static SongVideoInfo CreateMockSongVideoInfo(string id, string name, string artistName)
    {
        return new SongVideoInfo
        {
            Id = id,
            Name = name,
            Artists = new List<Artist>
            {
                new Artist { Id = "artist123", Name = artistName }
            },
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(45)),
            Album = new Album { Id = "album123", Name = "Test Album" },
            Thumbnails = new List<Thumbnail>()
        };
    }

    /// <summary>
    /// Mock implementation of ISearchResults for testing.
    /// </summary>
    private class MockSearchResults : ISearchResults
    {
        private readonly IReadOnlyList<IYouTubeBaseItem> _items;
        private readonly int? _maxResultsToReturn;

        public MockSearchResults(IReadOnlyList<IYouTubeBaseItem> items, int? maxResultsToReturn = null)
        {
            _items = items;
            _maxResultsToReturn = maxResultsToReturn;
        }

        public MockSearchResults(IReadOnlyList<SongSearchResult> songResults, int? maxResultsToReturn = null)
            : this(songResults.Cast<IYouTubeBaseItem>().ToList(), maxResultsToReturn)
        {
        }

        public Task<IReadOnlyList<IYouTubeBaseItem>> FetchItemsAsync(int offset, int count)
        {
            var itemsToReturn = _maxResultsToReturn.HasValue
                ? _items.Skip(offset).Take(Math.Min(count, _maxResultsToReturn.Value)).ToList()
                : _items.Skip(offset).Take(count).ToList();

            return Task.FromResult<IReadOnlyList<IYouTubeBaseItem>>(itemsToReturn);
        }
    }
}
