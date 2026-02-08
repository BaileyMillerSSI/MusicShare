using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services.Music.Apple;

namespace MusicShare.Tests.Unit.Services.Music;

public class AppleMusicMockAdapterTests
{
    private static AppleMusicMockAdapter CreateSut()
    {
        using var mock = AutoMock.GetLoose();
        return mock.Create<AppleMusicMockAdapter>();
    }

    [Fact]
    public void ItWillReturnAppleMusicServiceType()
    {
        var sut = CreateSut();
        sut.ServiceType.Should().Be(ServiceType.AppleMusic);
    }

    #region ExtractSongId Tests

    [Fact]
    public void ItWillReturnSongIdForSongUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.apple.com/us/song/1234567890");

        result.Should().Be("1234567890");
    }

    [Fact]
    public void ItWillReturnSongIdForAlbumUrlWithSongParam()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.apple.com/us/album/album-name/987654321?i=1234567890");

        result.Should().Be("1234567890");
    }

    [Fact]
    public void ItWillReturnSongIdForAlbumUrlWithMultipleParams()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.apple.com/us/album/name/987?i=1234567890&other=value");

        result.Should().Be("1234567890");
    }

    [Fact]
    public void ItWillReturnNullForNonAppleMusicUrl()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://open.spotify.com/track/abc");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForAppleMusicAlbumUrlWithoutSongId()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.apple.com/us/album/album-name/987654321");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnSongIdWithoutParamsForSongUrlWithQueryParams()
    {
        var sut = CreateSut();
        var result = sut.ExtractSongId("https://music.apple.com/us/song/1234567890?extra=param");

        result.Should().Be("1234567890");
    }

    #endregion

    #region NormalizeUrl Tests

    [Fact]
    public void ItWillReturnCanonicalUrlForSongUrl()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://music.apple.com/us/song/1234567890");

        result.Should().Be("https://music.apple.com/us/song/1234567890");
    }

    [Fact]
    public void ItWillReturnCanonicalSongUrlForAlbumUrlWithSongParam()
    {
        var sut = CreateSut();
        var result = sut.NormalizeUrl("https://music.apple.com/us/album/name/987?i=1234567890");

        result.Should().Be("https://music.apple.com/us/song/1234567890");
    }

    [Fact]
    public void ItWillReturnOriginalForNonAppleMusicUrl()
    {
        var sut = CreateSut();
        var url = "https://open.spotify.com/track/abc";
        var result = sut.NormalizeUrl(url);

        result.Should().Be(url);
    }

    #endregion

    #region ResolveMetadataAsync Tests

    [Fact]
    public async Task ItWillReturnMockMetadataForValidUrl()
    {
        var sut = CreateSut();
        var result = await sut.ResolveMetadataAsync("https://music.apple.com/us/song/12345");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Song 12345");
        result.Artists.Should().Contain("Artist A");
        result.Artists.Should().Contain("Artist B");
        result.Album.Should().Be("Album 12345");
    }

    [Fact]
    public async Task ItWillReturnNullWhenNoSongId()
    {
        var sut = CreateSut();
        var result = await sut.ResolveMetadataAsync("https://music.apple.com/us/album/name/987");

        result.Should().BeNull();
    }

    #endregion

    #region FindSongsAsync Tests

    [Fact]
    public async Task ItWillReturnMultipleAppleMusicResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>
        {
            new("track1", "Test Song", ["Artist One"], "Album One", "https://artwork.apple.com/1.jpg", TimeSpan.FromMinutes(3), false),
            new("track2", "Test Song (Remix)", ["Artist Two"], "Album Two", "https://artwork.apple.com/2.jpg", TimeSpan.FromMinutes(4), true)
        }.AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Test Song", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().HaveCount(2);
        results[0].Url.Should().Be("https://music.apple.com/us/song/track1");
        results[1].Url.Should().Be("https://music.apple.com/us/song/track2");
    }

    [Fact]
    public async Task ItWillTransformAppleTrackToSongSearchResult()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>
        {
            new(
                "track123",
                "Complete Song",
                ["Primary Artist", "Featured Artist"],
                "Test Album",
                "https://artwork.apple.com/complete.jpg",
                TimeSpan.FromSeconds(215),
                true)
        }.AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Complete Song", Artists = ["Primary Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        var firstResult = results.Should().ContainSingle().Subject;
        firstResult.Url.Should().Be("https://music.apple.com/us/song/track123");
        firstResult.FoundMetadata.Title.Should().Be("Complete Song");
        firstResult.FoundMetadata.Artists.Should().BeEquivalentTo(["Primary Artist", "Featured Artist"]);
        firstResult.FoundMetadata.Album.Should().Be("Test Album");
        firstResult.FoundMetadata.ArtworkUrl.Should().Be("https://artwork.apple.com/complete.jpg");
        firstResult.FoundMetadata.Duration.Should().Be(TimeSpan.FromSeconds(215));
        firstResult.FoundMetadata.IsExplicit.Should().BeTrue();
    }

    [Fact]
    public async Task ItWillGenerateCorrectAppleMusicUrls()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>
        {
            new("1234567890", "Song", ["Artist"], null, null, TimeSpan.FromMinutes(3), false)
        }.AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Song", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.First().Url.Should().Be("https://music.apple.com/us/song/1234567890");
    }

    [Fact]
    public async Task ItWillReturnConsistentResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>
        {
            new("track123", "Consistent Song", ["Artist"], "Album", null, TimeSpan.FromMinutes(3), false)
        }.AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata1 = new SongMetadata { Title = "Consistent Song", Artists = ["A"] };
        var metadata2 = new SongMetadata { Title = "Consistent Song", Artists = ["B"] };

        var results1 = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata1))
        {
            results1.Add(result);
        }

        var results2 = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata2))
        {
            results2.Add(result);
        }

        results1.First().Url.Should().Be(results2.First().Url);
    }

    [Fact]
    public async Task ItWillIncludeCompleteMetadata()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>
        {
            new(
                "track1",
                "Song Title",
                ["Artist Name"],
                "Album Name",
                "https://artwork.apple.com/image.jpg",
                TimeSpan.FromMinutes(4),
                false)
        }.AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Song Title", Artists = ["Artist Name"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        var foundMetadata = results.First().FoundMetadata;
        foundMetadata.Title.Should().NotBeNullOrEmpty();
        foundMetadata.Artists.Should().NotBeEmpty();
        foundMetadata.Album.Should().NotBeNullOrEmpty();
        foundMetadata.ArtworkUrl.Should().NotBeNullOrEmpty();
        foundMetadata.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ItWillHandleMissingAlbumInfo()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>
        {
            new("track1", "Song Without Album", ["Artist"], null, "https://artwork.apple.com/1.jpg", TimeSpan.FromMinutes(3), false)
        }.AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].FoundMetadata.Album.Should().BeNull();
    }

    [Fact]
    public async Task ItWillHandleEmptySearchResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>().AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
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

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((query, _, _) => capturedQuery = query)
            .ReturnsAsync(new List<Services.Services.Music.Apple.AppleMusicTrack>().AsReadOnly());

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Song Title", Artists = ["Artist Name"] };

        await foreach (var _ in sut.FindSongsAsync(metadata))
        {
            // Consume enumerable to trigger search
        }

        capturedQuery.Should().Be("Song Title Artist Name");
    }

    [Fact]
    public async Task ItWillHandleNullSearchResponse()
    {
        using var mock = AutoMock.GetLoose();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Services.Services.Music.Apple.AppleMusicTrack>?)null);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
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

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Apple Music API error"));

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillHandleMissingArtworkUrl()
    {
        using var mock = AutoMock.GetLoose();
        var searchResults = new List<Services.Services.Music.Apple.AppleMusicTrack>
        {
            new("track1", "Song", ["Artist"], "Album", null, TimeSpan.FromMinutes(3), false)
        }.AsReadOnly();

        mock.Mock<Services.Services.Music.Apple.IAppleMusicService>()
            .Setup(x => x.SearchTracksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var sut = mock.Create<Services.Services.Music.Apple.AppleMusicMockAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].FoundMetadata.ArtworkUrl.Should().BeNull();
    }

    #endregion
}
