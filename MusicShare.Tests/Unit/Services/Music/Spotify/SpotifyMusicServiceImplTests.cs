using System.Net;
using System.Text;
using System.Text.Json;
using MusicShare.Services.Services.Music.Spotify;
using Microsoft.Extensions.Logging;

namespace MusicShare.Tests.Unit.Services.Music.Spotify;

public class SpotifyMusicServiceImplTests
{
    [Fact]
    public async Task ItWillReturnSpotifyTracksForValidQuery()
    {
        using var mock = AutoMock.GetLoose();
        var response = CreateSpotifySearchResponse(3);
        var httpClient = CreateMockHttpClient((request, _) =>
        {
            request.RequestUri!.Query.Should().Contain("q=test%20query");
            request.RequestUri.Query.Should().Contain("type=track");
            request.RequestUri.Query.Should().Contain("limit=5");
            return Task.FromResult(CreateJsonResponse(response, HttpStatusCode.OK));
        });

        mock.Provide(httpClient);
        var sut = mock.Create<SpotifyMusicServiceImpl>();

        var result = await sut.SearchAsync("test query", 5, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.tracks.Should().NotBeNull();
        result.tracks.items.Should().HaveCount(3);
        result.tracks.items[0].name.Should().Be("Track 0");
        result.tracks.items[0].artists.Should().HaveCount(2);
        result.tracks.items[0].artists[0].name.Should().Be("Artist 0-0");
        result.tracks.items[0].album.name.Should().Be("Album 0");
        result.tracks.items[0].Duration.Should().Be(180000);
    }

    [Fact]
    public async Task ItWillReturnNullForNoResults()
    {
        using var mock = AutoMock.GetLoose();
        var response = CreateSpotifySearchResponse(0);
        var httpClient = CreateMockHttpClient((_, _) =>
            Task.FromResult(CreateJsonResponse(response, HttpStatusCode.OK)));

        mock.Provide(httpClient);
        var sut = mock.Create<SpotifyMusicServiceImpl>();

        var result = await sut.SearchAsync("unknown query", 5, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillHandleSpotifyApiError()
    {
        using var mock = AutoMock.GetLoose();
        var httpClient = CreateMockHttpClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        mock.Provide(httpClient);
        var sut = mock.Create<SpotifyMusicServiceImpl>();

        var result = await sut.SearchAsync("test query", 5, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnTrackByIdForValidTrackId()
    {
        using var mock = AutoMock.GetLoose();
        var track = CreateSpotifyTrack("track-123", "Test Song", 210000);
        var httpClient = CreateMockHttpClient((request, _) =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/tracks/track-123");
            return Task.FromResult(CreateJsonResponse(track, HttpStatusCode.OK));
        });

        mock.Provide(httpClient);
        var sut = mock.Create<SpotifyMusicServiceImpl>();

        var result = await sut.GetTrackAsync("track-123", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.id.Should().Be("track-123");
        result.name.Should().Be("Test Song");
        result.Duration.Should().Be(210000);
        result.artists.Should().HaveCount(1);
        result.artists[0].name.Should().Be("Artist Track");
        result.album.name.Should().Be("Album Track");
    }

    [Fact]
    public async Task ItWillReturnNullForInvalidTrackId()
    {
        using var mock = AutoMock.GetLoose();
        var httpClient = CreateMockHttpClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        mock.Provide(httpClient);
        var sut = mock.Create<SpotifyMusicServiceImpl>();

        var result = await sut.GetTrackAsync("invalid-id", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillParseSpotifyResponseCorrectly()
    {
        using var mock = AutoMock.GetLoose();
        var response = new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items = new[]
                {
                    new SpotifyResponse
                    {
                        id = "track-1",
                        name = "Bohemian Rhapsody",
                        artists = new[]
                        {
                            new SpotifyArtist { id = "artist-1", name = "Queen" }
                        },
                        album = new SpotifyAlbum
                        {
                            name = "A Night at the Opera",
                            images = new[]
                            {
                                new SpotifyImage { url = "https://i.scdn.co/image/abc", height = 640, width = 640 }
                            }
                        },
                        Duration = 354000,
                        Explicit = false,
                        is_playable = true,
                        external_urls = new SpotifyExternalUrls { spotify = "https://open.spotify.com/track/track-1" }
                    }
                },
                total = 1
            }
        };

        var httpClient = CreateMockHttpClient((_, _) =>
            Task.FromResult(CreateJsonResponse(response, HttpStatusCode.OK)));

        mock.Provide(httpClient);
        var sut = mock.Create<SpotifyMusicServiceImpl>();

        var result = await sut.SearchAsync("Bohemian Rhapsody", 5, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        var track = result!.tracks.items[0];
        track.id.Should().Be("track-1");
        track.name.Should().Be("Bohemian Rhapsody");
        track.artists[0].name.Should().Be("Queen");
        track.album.name.Should().Be("A Night at the Opera");
        track.Duration.Should().Be(354000);
        track.Explicit.Should().BeFalse();
        track.is_playable.Should().BeTrue();
        track.external_urls.spotify.Should().Be("https://open.spotify.com/track/track-1");
        track.album.images[0].url.Should().Be("https://i.scdn.co/image/abc");
    }

    [Fact]
    public async Task ItWillHandleHttpClientException()
    {
        using var mock = AutoMock.GetLoose();
        var httpClient = CreateMockHttpClient((_, _) =>
            throw new HttpRequestException("Network error"));

        mock.Provide(httpClient);
        var sut = mock.Create<SpotifyMusicServiceImpl>();

        var result = await sut.SearchAsync("test query", 5, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    private static HttpClient CreateMockHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpMessageHandler(handler);
        return new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T data, HttpStatusCode statusCode)
    {
        var json = JsonSerializer.Serialize(data);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static SpotifySearchResponse CreateSpotifySearchResponse(int trackCount)
    {
        return new SpotifySearchResponse
        {
            tracks = new SpotifyTracksResult
            {
                items = Enumerable.Range(0, trackCount)
                    .Select(i => CreateSpotifyTrack($"track-{i}", $"Track {i}", 180000 + (i * 1000)))
                    .ToArray(),
                total = trackCount
            }
        };
    }

    private static SpotifyResponse CreateSpotifyTrack(string id, string name, long durationMs)
    {
        return new SpotifyResponse
        {
            id = id,
            name = name,
            artists = new[]
            {
                new SpotifyArtist { id = $"artist-{id}-0", name = $"Artist {id.Split('-')[1]}-0" },
                new SpotifyArtist { id = $"artist-{id}-1", name = $"Artist {id.Split('-')[1]}-1" }
            },
            album = new SpotifyAlbum
            {
                name = $"Album {id.Split('-')[1]}",
                images = new[]
                {
                    new SpotifyImage { url = $"https://i.scdn.co/image/{id}", height = 640, width = 640 }
                }
            },
            Duration = durationMs,
            Explicit = false,
            is_playable = true,
            external_urls = new SpotifyExternalUrls { spotify = $"https://open.spotify.com/track/{id}" }
        };
    }

    private class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
