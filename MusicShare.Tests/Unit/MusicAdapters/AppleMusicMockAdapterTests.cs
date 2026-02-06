using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters.Services.Music.Apple;

namespace MusicShare.Tests.Unit.MusicAdapters;

public class AppleMusicMockAdapterTests
{
    private readonly AppleMusicMockAdapter _sut = new();

    [Fact]
    public void ItWillReturnAppleMusicServiceType()
    {
        _sut.ServiceType.Should().Be(ServiceType.AppleMusic);
    }

    #region ExtractSongId Tests

    [Fact]
    public void ItWillReturnSongIdForSongUrl()
    {
        var result = _sut.ExtractSongId("https://music.apple.com/us/song/1234567890");

        result.Should().Be("1234567890");
    }

    [Fact]
    public void ItWillReturnSongIdForAlbumUrlWithSongParam()
    {
        var result = _sut.ExtractSongId("https://music.apple.com/us/album/album-name/987654321?i=1234567890");

        result.Should().Be("1234567890");
    }

    [Fact]
    public void ItWillReturnSongIdForAlbumUrlWithMultipleParams()
    {
        var result = _sut.ExtractSongId("https://music.apple.com/us/album/name/987?i=1234567890&other=value");

        result.Should().Be("1234567890");
    }

    [Fact]
    public void ItWillReturnNullForNonAppleMusicUrl()
    {
        var result = _sut.ExtractSongId("https://open.spotify.com/track/abc");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnNullForAppleMusicAlbumUrlWithoutSongId()
    {
        var result = _sut.ExtractSongId("https://music.apple.com/us/album/album-name/987654321");

        result.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnSongIdWithoutParamsForSongUrlWithQueryParams()
    {
        var result = _sut.ExtractSongId("https://music.apple.com/us/song/1234567890?extra=param");

        result.Should().Be("1234567890");
    }

    #endregion

    #region NormalizeUrl Tests

    [Fact]
    public void ItWillReturnCanonicalUrlForSongUrl()
    {
        var result = _sut.NormalizeUrl("https://music.apple.com/us/song/1234567890");

        result.Should().Be("https://music.apple.com/us/song/1234567890");
    }

    [Fact]
    public void ItWillReturnCanonicalSongUrlForAlbumUrlWithSongParam()
    {
        var result = _sut.NormalizeUrl("https://music.apple.com/us/album/name/987?i=1234567890");

        result.Should().Be("https://music.apple.com/us/song/1234567890");
    }

    [Fact]
    public void ItWillReturnOriginalForNonAppleMusicUrl()
    {
        var url = "https://open.spotify.com/track/abc";
        var result = _sut.NormalizeUrl(url);

        result.Should().Be(url);
    }

    #endregion

    #region ResolveMetadataAsync Tests

    [Fact]
    public async Task ItWillReturnMockMetadataForValidUrl()
    {
        var result = await _sut.ResolveMetadataAsync("https://music.apple.com/us/song/12345");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Song 12345");
        result.Artists.Should().Contain("Artist A");
        result.Artists.Should().Contain("Artist B");
        result.Album.Should().Be("Album 12345");
    }

    [Fact]
    public async Task ItWillReturnNullWhenNoSongId()
    {
        var result = await _sut.ResolveMetadataAsync("https://music.apple.com/us/album/name/987");

        result.Should().BeNull();
    }

    #endregion

    #region FindSongAsync Tests

    [Fact]
    public async Task ItWillReturnMockUrlForAnyMetadata()
    {
        var metadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"]
        };

        var result = await _sut.FindSongAsync(metadata);

        result.Should().NotBeNull();
        result.Should().StartWith("https://music.apple.com/us/song/");
    }

    [Fact]
    public async Task ItWillReturnSameUrlForSameTitle()
    {
        var metadata1 = new SongMetadata { Title = "Consistent Song", Artists = ["A"] };
        var metadata2 = new SongMetadata { Title = "Consistent Song", Artists = ["B"] };

        var result1 = await _sut.FindSongAsync(metadata1);
        var result2 = await _sut.FindSongAsync(metadata2);

        result1.Should().Be(result2);
    }

    #endregion
}
