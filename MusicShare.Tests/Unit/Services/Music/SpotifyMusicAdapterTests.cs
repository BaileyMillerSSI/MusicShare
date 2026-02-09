using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services.Music.Spotify;

namespace MusicShare.Tests.Unit.Services.Music;

public class SpotifyMusicAdapterTests
{
    private static SpotifyMusicAdapter CreateSut()
    {
        using var mock = AutoMock.GetLoose();
        return mock.Create<SpotifyMusicAdapter>();
    }

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

    #region FindSongsAsync Tests

    [Fact]
    public async Task ItWillReturnMultipleSpotifyResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResponse = new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items =
                [
                    new SpotifyResponse
                    {
                        id = "track1",
                        name = "Test Song",
                        artists = [new SpotifyArtist { name = "Artist One" }],
                        album = new SpotifyAlbum { name = "Album One", images = [] },
                        external_urls = new SpotifyExternalUrls { spotify = "https://open.spotify.com/track/track1" },
                        Duration = 180000,
                        Explicit = false
                    },
                    new SpotifyResponse
                    {
                        id = "track2",
                        name = "Test Song (Remix)",
                        artists = [new SpotifyArtist { name = "Artist Two" }],
                        album = new SpotifyAlbum { name = "Album Two", images = [] },
                        external_urls = new SpotifyExternalUrls { spotify = "https://open.spotify.com/track/track2" },
                        Duration = 200000,
                        Explicit = true
                    }
                ]
            }
        };

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<SongMetadata>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test Song", Artists = ["Artist"] };

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().HaveCount(2);
        results[0].Url.Should().Be("https://open.spotify.com/track/track1");
        results[1].Url.Should().Be("https://open.spotify.com/track/track2");
    }

    [Fact]
    public async Task ItWillTransformSpotifyTrackToSongSearchResult()
    {
        using var mock = AutoMock.GetLoose();
        var searchResponse = new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items =
                [
                    new SpotifyResponse
                    {
                        id = "track1",
                        name = "Complete Song",
                        artists =
                        [
                            new SpotifyArtist { name = "Primary Artist" },
                            new SpotifyArtist { name = "Featured Artist" }
                        ],
                        album = new SpotifyAlbum
                        {
                            name = "Test Album",
                            images =
                            [
                                new SpotifyImage { url = "https://i.scdn.co/image/small.jpg", width = 64, height = 64 },
                                new SpotifyImage { url = "https://i.scdn.co/image/large.jpg", width = 640, height = 640 }
                            ]
                        },
                        external_urls = new SpotifyExternalUrls { spotify = "https://open.spotify.com/track/track1" },
                        Duration = 215000,
                        Explicit = true
                    }
                ]
            }
        };

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Complete Song", Artists = ["Primary Artist"] };

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        var firstResult = results.Should().ContainSingle().Subject;
        firstResult.Url.Should().Be("https://open.spotify.com/track/track1");
        firstResult.FoundMetadata.Title.Should().Be("Complete Song");
        firstResult.FoundMetadata.Artists.Should().BeEquivalentTo(["Primary Artist", "Featured Artist"]);
        firstResult.FoundMetadata.Album.Should().Be("Test Album");
        firstResult.FoundMetadata.Duration.Should().Be(TimeSpan.FromMilliseconds(215000));
        firstResult.FoundMetadata.IsExplicit.Should().BeTrue();
    }

    [Fact]
    public async Task ItWillIncludeCompleteMetadataInResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResponse = new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items =
                [
                    new SpotifyResponse
                    {
                        id = "track1",
                        name = "Song Title",
                        artists = [new SpotifyArtist { name = "Artist Name" }],
                        album = new SpotifyAlbum
                        {
                            name = "Album Name",
                            images = [new SpotifyImage { url = "https://artwork.url/image.jpg", width = 300, height = 300 }]
                        },
                        external_urls = new SpotifyExternalUrls { spotify = "https://open.spotify.com/track/track1" },
                        Duration = 240000,
                        Explicit = false
                    }
                ]
            }
        };

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Song Title", Artists = ["Artist Name"] };

        var results = new List<SongSearchResult>();
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
    public async Task ItWillHandleEmptySearchResults()
    {
        using var mock = AutoMock.GetLoose();
        var searchResponse = new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items = []
            }
        };

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Nonexistent Song", Artists = ["Unknown Artist"] };

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillBuildCorrectSearchQuery()
    {
        using var mock = AutoMock.GetLoose();
        string? capturedQuery = null;

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((query, _, _) => capturedQuery = query)
            .ReturnsAsync(new SpotifySearchResponse
            {
                tracks = new SpotifyTracksResult { items = [] }
            });

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Song Title", Artists = ["Artist Name"] };

        await foreach (var _ in sut.FindSongsAsync(metadata))
        {
            // Consume enumerable to trigger search
        }

        capturedQuery.Should().Be("track:Song Title artist:Artist Name");
    }

    [Fact]
    public async Task ItWillFilterOutTracksWithoutUrl()
    {
        using var mock = AutoMock.GetLoose();
        var searchResponse = new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items =
                [
                    new SpotifyResponse
                    {
                        id = "track1",
                        name = "Valid Track",
                        artists = [new SpotifyArtist { name = "Artist" }],
                        album = new SpotifyAlbum { name = "Album", images = [] },
                        external_urls = new SpotifyExternalUrls { spotify = "https://open.spotify.com/track/track1" },
                        Duration = 180000,
                        Explicit = false
                    },
                    new SpotifyResponse
                    {
                        id = "track2",
                        name = "Invalid Track",
                        artists = [new SpotifyArtist { name = "Artist" }],
                        album = new SpotifyAlbum { name = "Album", images = [] },
                        external_urls = null!, // Missing URL
                        Duration = 180000,
                        Explicit = false
                    }
                ]
            }
        };

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].FoundMetadata.Title.Should().Be("Valid Track");
    }

    [Fact]
    public async Task ItWillHandleNullSearchResponse()
    {
        using var mock = AutoMock.GetLoose();

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpotifySearchResponse?)null);

        var sut = mock.Create<SpotifyMusicAdapter>();
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

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API error"));

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Test", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillSelectLargestArtworkImage()
    {
        using var mock = AutoMock.GetLoose();
        var searchResponse = new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items =
                [
                    new SpotifyResponse
                    {
                        id = "track1",
                        name = "Song",
                        artists = [new SpotifyArtist { name = "Artist" }],
                        album = new SpotifyAlbum
                        {
                            name = "Album",
                            images =
                            [
                                new SpotifyImage { url = "https://i.scdn.co/image/small.jpg", width = 64, height = 64 },
                                new SpotifyImage { url = "https://i.scdn.co/image/medium.jpg", width = 300, height = 300 },
                                new SpotifyImage { url = "https://i.scdn.co/image/large.jpg", width = 640, height = 640 }
                            ]
                        },
                        external_urls = new SpotifyExternalUrls { spotify = "https://open.spotify.com/track/track1" },
                        Duration = 180000,
                        Explicit = false
                    }
                ]
            }
        };

        mock.Mock<ISpotifyMusicService>()
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        var sut = mock.Create<SpotifyMusicAdapter>();
        var metadata = new SongMetadata { Title = "Song", Artists = ["Artist"] };

        var results = new List<Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.First().FoundMetadata.ArtworkUrl.Should().Be("https://i.scdn.co/image/large.jpg");
    }

    #endregion
}
