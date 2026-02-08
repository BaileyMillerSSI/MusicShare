using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
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
    public async Task ItWillReturnMockResultsForAnyMetadata()
    {
        var sut = CreateSut();
        var metadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"]
        };

        var results = new List<MusicShare.Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata))
        {
            results.Add(result);
        }

        results.Should().NotBeEmpty();
        results.First().Url.Should().StartWith("https://music.apple.com/us/song/");
        results.First().FoundMetadata.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillReturnSameUrlForSameTitle()
    {
        var sut = CreateSut();
        var metadata1 = new SongMetadata { Title = "Consistent Song", Artists = ["A"] };
        var metadata2 = new SongMetadata { Title = "Consistent Song", Artists = ["B"] };

        var results1 = new List<MusicShare.Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata1))
        {
            results1.Add(result);
        }

        var results2 = new List<MusicShare.Services.Models.SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(metadata2))
        {
            results2.Add(result);
        }

        results1.First().Url.Should().Be(results2.First().Url);
    }

    #endregion
}
