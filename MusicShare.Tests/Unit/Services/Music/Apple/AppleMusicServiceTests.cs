using MusicShare.Services.Services.Music.Apple;

namespace MusicShare.Tests.Unit.Services.Music.Apple;

public class AppleMusicServiceTests
{
    [Fact]
    public async Task ItWillReturnMockTracksForValidQuery()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.SearchTracksAsync("test query", 5, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        result[0].Name.Should().Be("Mock Song 1");
        result[0].Artists.Should().Contain("Mock Artist 1");
        result[0].AlbumName.Should().Be("Mock Album 1");
        result[0].Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(0)));
        result[0].ArtworkUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ItWillGenerateDeterministicDataForSameQuery()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result1 = await sut.SearchTracksAsync("consistent query", 3, TestContext.Current.CancellationToken);
        var result2 = await sut.SearchTracksAsync("consistent query", 3, TestContext.Current.CancellationToken);

        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);

        // Verify track IDs are identical (hash-based generation)
        for (int i = 0; i < 3; i++)
        {
            result1[i].Id.Should().Be(result2[i].Id);
            result1[i].Name.Should().Be(result2[i].Name);
            result1[i].Artists.Should().BeEquivalentTo(result2[i].Artists);
        }
    }

    [Fact]
    public async Task ItWillGenerateDifferentDataForDifferentQuery()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result1 = await sut.SearchTracksAsync("query one", 3, TestContext.Current.CancellationToken);
        var result2 = await sut.SearchTracksAsync("query two", 3, TestContext.Current.CancellationToken);

        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);

        // Track IDs should be different due to different hash
        result1[0].Id.Should().NotBe(result2[0].Id);
        result1[1].Id.Should().NotBe(result2[1].Id);
        result1[2].Id.Should().NotBe(result2[2].Id);
    }

    [Fact]
    public async Task ItWillReturnMockTrackByIdForValidId()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.GetTrackAsync("track-123", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be("track-123");
        result.Name.Should().Be("Song track-123");
        result.Artists.Should().Contain("Artist A");
        result.Artists.Should().Contain("Artist B");
        result.AlbumName.Should().Be("Album track-123");
        result.Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(30)));
        result.ArtworkUrl.Should().Be("https://is1-ssl.mzstatic.com/image/track-123");
    }

    [Fact]
    public async Task ItWillReturnNullForEmptyTrackId()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.GetTrackAsync("", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnNullForWhitespaceTrackId()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.GetTrackAsync("   ", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnConsistentMockDataStructure()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.SearchTracksAsync("structure test", 10, TestContext.Current.CancellationToken);

        result.Should().HaveCount(10);

        foreach (var track in result)
        {
            // Verify all required fields are populated
            track.Id.Should().NotBeNullOrEmpty();
            track.Name.Should().NotBeNullOrEmpty();
            track.Artists.Should().NotBeNull();
            track.Artists.Should().NotBeEmpty();
            track.AlbumName.Should().NotBeNullOrEmpty();
            track.ArtworkUrl.Should().NotBeNullOrEmpty();
            track.ArtworkUrl.Should().StartWith("https://is1-ssl.mzstatic.com/image/");
            track.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }

    [Fact]
    public async Task ItWillGenerateDifferentDurationsForEachTrack()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.SearchTracksAsync("duration test", 5, TestContext.Current.CancellationToken);

        result.Should().HaveCount(5);

        // Each track should have a different duration based on index
        result[0].Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(0)));
        result[1].Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(15)));
        result[2].Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(30)));
        result[3].Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(45)));
        result[4].Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public async Task ItWillSetExplicitFlagBasedOnIndex()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.SearchTracksAsync("explicit test", 10, TestContext.Current.CancellationToken);

        result.Should().HaveCount(10);

        // Every 3rd track (index % 3 == 0) should be marked explicit
        result[0].IsExplicit.Should().BeTrue();
        result[1].IsExplicit.Should().BeNull();
        result[2].IsExplicit.Should().BeNull();
        result[3].IsExplicit.Should().BeTrue();
        result[4].IsExplicit.Should().BeNull();
        result[5].IsExplicit.Should().BeNull();
        result[6].IsExplicit.Should().BeTrue();
    }

    [Fact]
    public async Task ItWillHandleEmptyQueryGracefully()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        // Empty query should still work (hash will be generated)
        var result = await sut.SearchTracksAsync("", 3, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ItWillRespectLimitParameter()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result1 = await sut.SearchTracksAsync("limit test", 3, TestContext.Current.CancellationToken);
        var result2 = await sut.SearchTracksAsync("limit test", 7, TestContext.Current.CancellationToken);
        var result3 = await sut.SearchTracksAsync("limit test", 1, TestContext.Current.CancellationToken);

        result1.Should().HaveCount(3);
        result2.Should().HaveCount(7);
        result3.Should().HaveCount(1);
    }

    [Fact]
    public async Task ItWillReturnTrackWithConsistentIdFormatInGetTrack()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<AppleMusicService>();

        var result = await sut.GetTrackAsync("apple-music-123", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be("apple-music-123");
        result.Name.Should().Be("Song apple-music-123");
        result.AlbumName.Should().Be("Album apple-music-123");
    }
}
