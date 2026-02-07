using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class SongServiceTests
{
    private static Song CreateSong(string id = "song-1") =>
        new() { Id = id, Title = "Test Song", Artists = ["Artist"], Status = SongStatus.Pending };

    [Fact]
    public async Task ItWillUpdateSongStatusToResolved()
    {
        using var mock = AutoMock.GetLoose();
        var song = CreateSong();

        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);

        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync("song-1", SongStatus.Resolved);

        song.Status.Should().Be(SongStatus.Resolved);
        mock.Mock<ISongRepository>().Verify(
            x => x.UpdateAsync(song, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillUpdateSongStatusToPartiallyResolved()
    {
        using var mock = AutoMock.GetLoose();
        var song = CreateSong();

        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);

        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync("song-1", SongStatus.PartiallyResolved);

        song.Status.Should().Be(SongStatus.PartiallyResolved);
    }

    [Fact]
    public async Task ItWillUpdateSongStatusToFailed()
    {
        using var mock = AutoMock.GetLoose();
        var song = CreateSong();

        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);

        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync("song-1", SongStatus.Failed);

        song.Status.Should().Be(SongStatus.Failed);
    }

    [Fact]
    public async Task ItWillSetUpdatedAtTimestamp()
    {
        using var mock = AutoMock.GetLoose();
        var song = CreateSong();
        var beforeUpdate = DateTime.UtcNow;

        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);

        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync("song-1", SongStatus.Resolved);

        song.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task ItWillSkipUpdateWhenSongIdIsNull()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync(null, SongStatus.Resolved);

        mock.Mock<ISongRepository>().Verify(
            x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mock.Mock<ISongRepository>().Verify(
            x => x.UpdateAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillSkipUpdateWhenSongIdIsEmpty()
    {
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync("", SongStatus.Resolved);

        mock.Mock<ISongRepository>().Verify(
            x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillSkipUpdateWhenSongNotFound()
    {
        using var mock = AutoMock.GetLoose();

        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Song?)null);

        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync("song-1", SongStatus.Resolved);

        mock.Mock<ISongRepository>().Verify(
            x => x.UpdateAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToRepository()
    {
        using var mock = AutoMock.GetLoose();
        var song = CreateSong();
        var cts = new CancellationTokenSource();

        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-1", cts.Token))
            .ReturnsAsync(song);

        var sut = mock.Create<SongService>();

        await sut.UpdateStatusAsync("song-1", SongStatus.Resolved, cts.Token);

        mock.Mock<ISongRepository>().Verify(
            x => x.GetByIdAsync("song-1", cts.Token), Times.Once);
        mock.Mock<ISongRepository>().Verify(
            x => x.UpdateAsync(song, cts.Token), Times.Once);
    }
}
