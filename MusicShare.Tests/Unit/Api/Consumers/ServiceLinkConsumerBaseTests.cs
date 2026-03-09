using MassTransit;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;
using MusicShare.Api.Consumers;

namespace MusicShare.Tests.Unit.Worker.Consumers;

/// <summary>
/// Tests the ServiceLinkConsumerBase logic through SpotifyLinkConsumer as a concrete implementation.
/// </summary>
public class ServiceLinkConsumerBaseTests
{
    private static ResolveServiceLink CreateMessage(
        string songId = "song-1",
        string shareId = "share-1",
        Guid? correlationId = null)
    {
        return new ResolveServiceLink
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            SongId = songId,
            ShareId = shareId,
            TargetService = ServiceType.Spotify,
            Metadata = new SongMetadataPayload
            {
                Title = "Test Song",
                Artists = ["Artist One"],
                Album = "Test Album"
            }
        };
    }

    private static Mock<ConsumeContext<ResolveServiceLink>> CreateContext(ResolveServiceLink message)
    {
        var context = new Mock<ConsumeContext<ResolveServiceLink>>();
        context.Setup(x => x.Message).Returns(message);
        context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task ItWillCreateLinkAndPublishSuccessWhenSongFoundOnService()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        var message = CreateMessage();
        var context = CreateContext(message);

        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        var foundUrl = "https://open.spotify.com/track/found123";
        adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foundUrl);
        adapterMock
            .Setup(x => x.NormalizeUrl(foundUrl))
            .Returns(foundUrl);
        adapterMock
            .Setup(x => x.ExtractSongId(foundUrl))
            .Returns("found123");

        var sut = mock.Create<SpotifyLinkConsumer>();

        // Act
        await sut.Consume(context.Object);

        // Assert
        mock.Mock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(
                It.Is<SongServiceLink>(l =>
                    l.SongId == "song-1" &&
                    l.ServiceType == ServiceType.Spotify &&
                    l.ServiceSongId == "found123" &&
                    l.OriginalUrl == foundUrl &&
                    l.NormalizedUrl == foundUrl),
                It.IsAny<CancellationToken>()),
            Times.Once);

        context.Verify(
            x => x.Publish(
                It.Is<ServiceLinkResolved>(e =>
                    e.SongId == "song-1" &&
                    e.ServiceType == ServiceType.Spotify &&
                    e.ResolvedUrl == foundUrl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillPublishFailureWhenSongNotFoundOnService()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        var message = CreateMessage();
        var context = CreateContext(message);

        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = mock.Create<SpotifyLinkConsumer>();

        // Act
        await sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<ServiceLinkFailed>(e =>
                    e.SongId == "song-1" &&
                    e.ServiceType == ServiceType.Spotify &&
                    e.ErrorMessage.Contains("Song not found")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mock.Mock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(It.IsAny<SongServiceLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillPublishFailureWhenFindSongReturnsEmptyString()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        var message = CreateMessage();
        var context = CreateContext(message);

        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        var sut = mock.Create<SpotifyLinkConsumer>();

        // Act
        await sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(It.IsAny<ServiceLinkFailed>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillPublishSuccessWithoutCreatingNewWhenLinkExists()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        var correlationId = Guid.NewGuid();
        var message = CreateMessage(correlationId: correlationId);
        var context = CreateContext(message);

        var existingLink = new SongServiceLink
        {
            Id = "existing-link",
            SongId = "song-1",
            ServiceType = ServiceType.Spotify,
            ServiceSongId = "existing-id",
            OriginalUrl = "https://open.spotify.com/track/existing",
            NormalizedUrl = "https://open.spotify.com/track/existing"
        };
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        var sut = mock.Create<SpotifyLinkConsumer>();

        // Act
        await sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<ServiceLinkResolved>(e =>
                    e.CorrelationId == correlationId &&
                    e.ResolvedUrl == "https://open.spotify.com/track/existing"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should not create a new link
        mock.Mock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(It.IsAny<SongServiceLink>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Should not try to find the song again
        adapterMock.Verify(
            x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillPublishFailureAndNotRethrowWhenAdapterThrowsException()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        var message = CreateMessage();
        var context = CreateContext(message);

        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API error"));

        var sut = mock.Create<SpotifyLinkConsumer>();

        // Act - should not throw
        await sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<ServiceLinkFailed>(e =>
                    e.ErrorMessage.Contains("API error")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillUseFoundUrlAsServiceSongIdWhenExtractSongIdReturnsNull()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        var message = CreateMessage();
        var context = CreateContext(message);

        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        var foundUrl = "https://open.spotify.com/track/xyz";
        adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foundUrl);
        adapterMock
            .Setup(x => x.NormalizeUrl(foundUrl))
            .Returns(foundUrl);
        adapterMock
            .Setup(x => x.ExtractSongId(foundUrl))
            .Returns((string?)null);

        var sut = mock.Create<SpotifyLinkConsumer>();

        // Act
        await sut.Consume(context.Object);

        // Assert
        mock.Mock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(
                It.Is<SongServiceLink>(l => l.ServiceSongId == foundUrl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillConvertPayloadToSongMetadata()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(adapterMock.Object);

        var message = new ResolveServiceLink
        {
            CorrelationId = Guid.NewGuid(),
            SongId = "song-1",
            ShareId = "share-1",
            TargetService = ServiceType.Spotify,
            Metadata = new SongMetadataPayload
            {
                Title = "Specific Title",
                Artists = ["Artist A", "Artist B"],
                Album = "Specific Album",
                ArtworkUrl = "https://art.example.com/img.jpg",
                Duration = TimeSpan.FromSeconds(180),
                IsExplicit = true
            }
        };
        var context = CreateContext(message);

        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = mock.Create<SpotifyLinkConsumer>();

        // Act
        await sut.Consume(context.Object);

        // Assert
        adapterMock.Verify(
            x => x.FindSongAsync(
                It.Is<SongMetadata>(m =>
                    m.Title == "Specific Title" &&
                    m.Artists.Contains("Artist A") &&
                    m.Artists.Contains("Artist B") &&
                    m.Album == "Specific Album" &&
                    m.ArtworkUrl == "https://art.example.com/img.jpg" &&
                    m.Duration == TimeSpan.FromSeconds(180) &&
                    m.IsExplicit == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
