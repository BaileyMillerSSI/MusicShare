using MassTransit;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters.Services;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Worker.Consumers;

namespace MusicShare.Tests.Unit.Worker.Consumers;

public class SourceMetadataConsumerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly SourceMetadataConsumer _sut;

    public SourceMetadataConsumerTests()
    {
        _sut = _mocker.CreateInstance<SourceMetadataConsumer>();
    }

    private static ResolveSourceMetadata CreateMessage(
        string shareId = "share-1",
        string sourceUrl = "https://open.spotify.com/track/abc",
        ServiceType sourceService = ServiceType.Spotify,
        Guid? correlationId = null)
    {
        return new ResolveSourceMetadata
        {
            ShareId = shareId,
            SourceUrl = sourceUrl,
            SourceService = sourceService,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
    }

    private Mock<ConsumeContext<ResolveSourceMetadata>> CreateContext(ResolveSourceMetadata message)
    {
        var context = new Mock<ConsumeContext<ResolveSourceMetadata>>();
        context.Setup(x => x.Message).Returns(message);
        context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task ItWillCreateSongAndLinkForValidMetadata()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        var adapterMock = new Mock<IMusicServiceAdapter>();
        _mocker.GetMock<IMusicServiceResolver>().Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapterMock.Object);

        var metadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Artist One"],
            Album = "Test Album",
            ArtworkUrl = "https://img.example.com/art.jpg",
            Duration = TimeSpan.FromMinutes(3),
            IsExplicit = false
        };
        adapterMock.Setup(x => x.ResolveMetadataAsync(message.SourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        adapterMock.Setup(x => x.NormalizeUrl(message.SourceUrl)).Returns(message.SourceUrl);
        adapterMock.Setup(x => x.ExtractSongId(message.SourceUrl)).Returns("abc");

        var insertedSong = new Song
        {
            Id = "song-new-id",
            Title = "Test Song",
            Artists = ["Artist One"],
            Album = "Test Album",
            Status = SongStatus.Pending
        };
        _mocker.GetMock<ISongRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(insertedSong);

        var shareRequest = new ShareRequest
        {
            ShareId = message.ShareId,
            SourceUrl = message.SourceUrl,
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Pending
        };
        _mocker.GetMock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync(message.ShareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _mocker.GetMock<ISongRepository>().Verify(
            x => x.InsertAsync(
                It.Is<Song>(s => s.Title == "Test Song" && s.Artists.Contains("Artist One")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mocker.GetMock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(
                It.Is<SongServiceLink>(l =>
                    l.SongId == "song-new-id" &&
                    l.ServiceType == ServiceType.Spotify &&
                    l.ServiceSongId == "abc"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillUpdateShareRequestStatusForValidMetadata()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        var adapterMock = new Mock<IMusicServiceAdapter>();
        _mocker.GetMock<IMusicServiceResolver>().Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapterMock.Object);

        adapterMock.Setup(x => x.ResolveMetadataAsync(message.SourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SongMetadata { Title = "Song", Artists = ["A"] });
        adapterMock.Setup(x => x.NormalizeUrl(message.SourceUrl)).Returns(message.SourceUrl);
        adapterMock.Setup(x => x.ExtractSongId(message.SourceUrl)).Returns("abc");

        _mocker.GetMock<ISongRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Song { Id = "song-1", Title = "Song", Artists = ["A"] });

        var shareRequest = new ShareRequest
        {
            ShareId = message.ShareId,
            Status = ShareStatus.Pending
        };
        _mocker.GetMock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync(message.ShareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        shareRequest.SongId.Should().Be("song-1");
        shareRequest.Status.Should().Be(ShareStatus.Processing);
        _mocker.GetMock<IShareRequestRepository>().Verify(
            x => x.UpdateAsync(shareRequest, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillPublishSourceMetadataResolvedForValidMetadata()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var message = CreateMessage(correlationId: correlationId);
        var context = CreateContext(message);

        var adapterMock = new Mock<IMusicServiceAdapter>();
        _mocker.GetMock<IMusicServiceResolver>().Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapterMock.Object);

        adapterMock.Setup(x => x.ResolveMetadataAsync(message.SourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SongMetadata { Title = "Song", Artists = ["A"], Album = "Album" });
        adapterMock.Setup(x => x.NormalizeUrl(message.SourceUrl)).Returns(message.SourceUrl);
        adapterMock.Setup(x => x.ExtractSongId(message.SourceUrl)).Returns("abc");

        _mocker.GetMock<ISongRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Song { Id = "song-1", Title = "Song", Artists = ["A"] });

        _mocker.GetMock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync(message.ShareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<SourceMetadataResolved>(e =>
                    e.CorrelationId == correlationId &&
                    e.SongId == "song-1" &&
                    e.ShareId == message.ShareId &&
                    e.SourceService == ServiceType.Spotify &&
                    e.Metadata.Title == "Song"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillPublishFailureWhenNoAdapterFound()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        _mocker.GetMock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(ServiceType.Spotify))
            .Returns((IMusicServiceAdapter?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<SourceMetadataFailed>(e =>
                    e.CorrelationId == message.CorrelationId &&
                    e.ShareId == message.ShareId &&
                    e.ErrorMessage.Contains("No adapter found")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mocker.GetMock<ISongRepository>().Verify(
            x => x.InsertAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillPublishFailureWhenMetadataResolutionReturnsNull()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        var adapterMock = new Mock<IMusicServiceAdapter>();
        _mocker.GetMock<IMusicServiceResolver>().Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapterMock.Object);

        adapterMock.Setup(x => x.ResolveMetadataAsync(message.SourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongMetadata?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        context.Verify(
            x => x.Publish(
                It.Is<SourceMetadataFailed>(e =>
                    e.ErrorMessage.Contains("Could not resolve metadata")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillPublishFailureAndRethrowWhenExceptionThrown()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        var adapterMock = new Mock<IMusicServiceAdapter>();
        _mocker.GetMock<IMusicServiceResolver>().Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapterMock.Object);

        adapterMock.Setup(x => x.ResolveMetadataAsync(message.SourceUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.Consume(context.Object));

        context.Verify(
            x => x.Publish(
                It.Is<SourceMetadataFailed>(e =>
                    e.ErrorMessage.Contains("Network error")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillUseSourceUrlAsServiceSongIdWhenExtractSongIdReturnsNull()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        var adapterMock = new Mock<IMusicServiceAdapter>();
        _mocker.GetMock<IMusicServiceResolver>().Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapterMock.Object);

        adapterMock.Setup(x => x.ResolveMetadataAsync(message.SourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SongMetadata { Title = "Song", Artists = ["A"] });
        adapterMock.Setup(x => x.NormalizeUrl(message.SourceUrl)).Returns(message.SourceUrl);
        adapterMock.Setup(x => x.ExtractSongId(message.SourceUrl)).Returns((string?)null);

        _mocker.GetMock<ISongRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Song { Id = "song-1", Title = "Song", Artists = ["A"] });

        _mocker.GetMock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync(message.ShareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _mocker.GetMock<ISongServiceLinkRepository>().Verify(
            x => x.InsertAsync(
                It.Is<SongServiceLink>(l => l.ServiceSongId == message.SourceUrl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillSkipUpdateButContinueWhenShareRequestNotFound()
    {
        // Arrange
        var message = CreateMessage();
        var context = CreateContext(message);

        var adapterMock = new Mock<IMusicServiceAdapter>();
        _mocker.GetMock<IMusicServiceResolver>().Setup(x => x.GetAdapter(ServiceType.Spotify)).Returns(adapterMock.Object);

        adapterMock.Setup(x => x.ResolveMetadataAsync(message.SourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SongMetadata { Title = "Song", Artists = ["A"] });
        adapterMock.Setup(x => x.NormalizeUrl(message.SourceUrl)).Returns(message.SourceUrl);
        adapterMock.Setup(x => x.ExtractSongId(message.SourceUrl)).Returns("abc");

        _mocker.GetMock<ISongRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<Song>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Song { Id = "song-1", Title = "Song", Artists = ["A"] });

        _mocker.GetMock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync(message.ShareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act
        await _sut.Consume(context.Object);

        // Assert - should still publish success
        context.Verify(
            x => x.Publish(It.IsAny<SourceMetadataResolved>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mocker.GetMock<IShareRequestRepository>().Verify(
            x => x.UpdateAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
