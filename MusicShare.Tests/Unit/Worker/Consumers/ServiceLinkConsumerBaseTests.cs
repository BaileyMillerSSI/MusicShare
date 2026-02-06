using MassTransit;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters.Services;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Worker.Consumers;

namespace MusicShare.Tests.Unit.Worker.Consumers;

/// <summary>
/// Tests the ServiceLinkConsumerBase logic through SpotifyLinkConsumer as a concrete implementation.
/// </summary>
public class ServiceLinkConsumerBaseTests
{
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IMusicServiceAdapter> _adapterMock = new();
    private readonly SpotifyLinkConsumer _sut;

    public ServiceLinkConsumerBaseTests()
    {
        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns(_adapterMock.Object);

        _sut = _mocker.CreateInstance<SpotifyLinkConsumer>();
    }

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
        var message = CreateMessage();
        var context = CreateContext(message);

        _mocker.GetMock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        var foundUrl = "https://open.spotify.com/track/found123";
        _adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foundUrl);
        _adapterMock
            .Setup(x => x.NormalizeUrl(foundUrl))
            .Returns(foundUrl);
        _adapterMock
            .Setup(x => x.ExtractSongId(foundUrl))
            .Returns("found123");

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _mocker.GetMock<ISongServiceLinkRepository>().Verify(
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
        var message = CreateMessage();
        var context = CreateContext(message);

        _mocker.GetMock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        _adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<ServiceLinkFailed>(e =>
                    e.SongId == "song-1" &&
                    e.ServiceType == ServiceType.Spotify &&
                    e.ErrorMessage.Contains("Song not found")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mocker.GetMock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(It.IsAny<SongServiceLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillPublishFailureWhenFindSongReturnsEmptyString()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        _mocker.GetMock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        _adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        // Act
        await _sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(It.IsAny<ServiceLinkFailed>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillPublishSuccessWithoutCreatingNewWhenLinkExists()
    {
        // Arrange
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
        _mocker.GetMock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<ServiceLinkResolved>(e =>
                    e.CorrelationId == correlationId &&
                    e.ResolvedUrl == "https://open.spotify.com/track/existing"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should not create a new link
        _mocker.GetMock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(It.IsAny<SongServiceLink>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Should not try to find the song again
        _adapterMock.Verify(
            x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillPublishFailureAndNotRethrowWhenAdapterThrowsException()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        _mocker.GetMock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        _adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API error"));

        // Act - should not throw
        await _sut.Consume(context.Object);

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
        var message = CreateMessage();
        var context = CreateContext(message);

        _mocker.GetMock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        var foundUrl = "https://open.spotify.com/track/xyz";
        _adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foundUrl);
        _adapterMock
            .Setup(x => x.NormalizeUrl(foundUrl))
            .Returns(foundUrl);
        _adapterMock
            .Setup(x => x.ExtractSongId(foundUrl))
            .Returns((string?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _mocker.GetMock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(
                It.Is<SongServiceLink>(l => l.ServiceSongId == foundUrl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillConvertPayloadToSongMetadata()
    {
        // Arrange
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

        _mocker.GetMock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAndServiceAsync("song-1", ServiceType.Spotify, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);

        _adapterMock
            .Setup(x => x.FindSongAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _adapterMock.Verify(
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
