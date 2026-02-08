using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services.Music.YouTube;
using YouTubeMusicAPI.Models;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.Tests.Unit.Services.Music;

public class YouTubeMusicAdapterTests
{
    private static YouTubeMusicAdapter CreateSut()
    {
        using var mock = AutoMock.GetLoose();
        return mock.Create<YouTubeMusicAdapter>();
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

    #region FindSongsAsync Tests

    [Fact]
    public async Task ItWillReturnMultipleYouTubeResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<SongSearchResult>
        {
            new()
            {
                Id = "video1",
                Name = "Test Song",
                Artists = [new Artist { Name = "Artist One", Id = "artist1" }],
                Album = new YouTubeMusicAPI.Models.Album { Name = "Album One", Id = "album1" },
                Duration = TimeSpan.FromMinutes(3),
                IsExplicit = false,
                Thumbnails = [new Thumbnail { Url = "https://i.ytimg.com/vi/video1/maxresdefault.jpg", Width = 1280, Height = 720 }]
            },
            new()
            {
                Id = "video2",
                Name = "Test Song (Live)",
                Artists = [new Artist { Name = "Artist Two", Id = "artist2" }],
                Album = new YouTubeMusicAPI.Models.Album { Name = "Album Two", Id = "album2" },
                Duration = TimeSpan.FromMinutes(4),
                IsExplicit = true,
                Thumbnails = [new Thumbnail { Url = "https://i.ytimg.com/vi/video2/maxresdefault.jpg", Width = 1280, Height = 720 }]
            }
        }.AsReadOnly();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test Song", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().HaveCount(2);
        results[0].Url.Should().Be("https://music.youtube.com/watch?v=video1");
        results[1].Url.Should().Be("https://music.youtube.com/watch?v=video2");
    }

    [Fact]
    public async Task ItWillTransformYouTubeVideoToSongSearchResult()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<SongSearchResult>
        {
            new()
            {
                Id = "video1",
                Name = "Complete Song",
                Artists =
                [
                    new Artist { Name = "Primary Artist", Id = "artist1" },
                    new Artist { Name = "Featured Artist", Id = "artist2" }
                ],
                Album = new YouTubeMusicAPI.Models.Album { Name = "Test Album", Id = "album1" },
                Duration = TimeSpan.FromSeconds(215),
                IsExplicit = true,
                Thumbnails =
                [
                    new Thumbnail { Url = "https://i.ytimg.com/vi/video1/default.jpg", Width = 120, Height = 90 },
                    new Thumbnail { Url = "https://i.ytimg.com/vi/video1/maxresdefault.jpg", Width = 1280, Height = 720 }
                ]
            }
        }.AsReadOnly();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Complete Song", Artists = ["Primary Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        var firstResult = results.Should().ContainSingle().Subject;
        firstResult.Url.Should().Be("https://music.youtube.com/watch?v=video1");
        firstResult.FoundMetadata.Title.Should().Be("Complete Song");
        firstResult.FoundMetadata.Artists.Should().BeEquivalentTo(["Primary Artist", "Featured Artist"]);
        firstResult.FoundMetadata.Album.Should().Be("Test Album");
        firstResult.FoundMetadata.Duration.Should().Be(TimeSpan.FromSeconds(215));
        firstResult.FoundMetadata.IsExplicit.Should().BeTrue();
    }

    [Fact]
    public async Task ItWillGenerateCorrectYouTubeUrls()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<SongSearchResult>
        {
            new()
            {
                Id = "dQw4w9WgXcQ",
                Name = "Song",
                Artists = [new Artist { Name = "Artist", Id = "artist1" }],
                Duration = TimeSpan.FromMinutes(3),
                Thumbnails = []
            }
        }.AsReadOnly();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Song", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.First().Url.Should().Be("https://music.youtube.com/watch?v=dQw4w9WgXcQ");
    }

    [Fact]
    public async Task ItWillHandleEmptySearchResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<SongSearchResult>().AsReadOnly();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Nonexistent Song", Artists = ["Unknown Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillBuildSearchQueryWithArtistAndTitle()
    {
        using var mock = AutoMock.GetLoose();
        string? capturedQuery = null;

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((query, _, _) => capturedQuery = query)
            .ReturnsAsync(new List<SongSearchResult>().AsReadOnly());

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Song Title", Artists = ["Artist Name"] };

        await foreach (var _ in sut.FindSongsAsync(metadata))
        {
            // Consume enumerable to trigger search
        }

        capturedQuery.Should().Be("Song Title Artist Name");
    }

    [Fact]
    public async Task ItWillFilterOutVideosWithoutId()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<SongSearchResult>
        {
            new()
            {
                Id = "video1",
                Name = "Valid Video",
                Artists = [new Artist { Name = "Artist", Id = "artist1" }],
                Duration = TimeSpan.FromMinutes(3),
                Thumbnails = []
            },
            new()
            {
                Id = null!, // Missing ID
                Name = "Invalid Video",
                Artists = [new Artist { Name = "Artist", Id = "artist1" }],
                Duration = TimeSpan.FromMinutes(3),
                Thumbnails = []
            }
        }.AsReadOnly();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].FoundMetadata.Title.Should().Be("Valid Video");
    }

    [Fact]
    public async Task ItWillMapThumbnailsToArtworkUrl()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<SongSearchResult>
        {
            new()
            {
                Id = "video1",
                Name = "Song",
                Artists = [new Artist { Name = "Artist", Id = "artist1" }],
                Duration = TimeSpan.FromMinutes(3),
                Thumbnails =
                [
                    new Thumbnail { Url = "https://i.ytimg.com/vi/video1/default.jpg", Width = 120, Height = 90 },
                    new Thumbnail { Url = "https://i.ytimg.com/vi/video1/hqdefault.jpg", Width = 480, Height = 360 },
                    new Thumbnail { Url = "https://i.ytimg.com/vi/video1/maxresdefault.jpg", Width = 1280, Height = 720 }
                ]
            }
        }.AsReadOnly();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Song", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.First().FoundMetadata.ArtworkUrl.Should().Be("https://i.ytimg.com/vi/video1/maxresdefault.jpg");
    }

    [Fact]
    public async Task ItWillHandleNullSearchResponse()
    {
        using var mock = AutoMock.GetLoose();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SongSearchResult>?)null);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillHandleSearchException()
    {
        using var mock = AutoMock.GetLoose();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("YouTube API error"));

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillHandleMissingAlbumInfo()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<SongSearchResult>
        {
            new()
            {
                Id = "video1",
                Name = "Song Without Album",
                Artists = [new Artist { Name = "Artist", Id = "artist1" }],
                Album = null, // No album info
                Duration = TimeSpan.FromMinutes(3),
                Thumbnails = []
            }
        }.AsReadOnly();

        mock.Mock<IYouTubeMusicService>()
            .Setup(x => x.SearchSongsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<YouTubeMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].FoundMetadata.Album.Should().BeNull();
    }

    #endregion
}
