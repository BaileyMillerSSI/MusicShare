using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;
using MusicShare.Api.Sagas.ShareRequest;
using MusicShare.Api.Sagas.ShareRequest.Activities;

namespace MusicShare.Tests.Unit.Api.Sagas;

public class CompleteSagaActivityTests
{
    private static ShareRequestSagaState CreateSaga(
        string shareId = "share-abc123",
        string? songId = "song-1",
        List<ServiceType>? resolvedServices = null,
        List<ServiceType>? failedServices = null)
    {
        return new ShareRequestSagaState
        {
            CorrelationId = Guid.NewGuid(),
            CurrentState = "AwaitingServiceLinks",
            ShareId = shareId,
            SongId = songId,
            SourceService = ServiceType.Spotify,
            ResolvedServices = resolvedServices ?? [],
            FailedServices = failedServices ?? [],
            PendingServices = [],
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
    }

    #region Execute via SourceMetadataResolved

    [Fact]
    public async Task ItWillCallSongServiceWithResolvedStatusWhenAllServicesResolved()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(
            resolvedServices: [ServiceType.AppleMusic, ServiceType.YouTubeMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync("song-1", SongStatus.Resolved, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillCallSongServiceWithPartiallyResolvedStatusWhenSomeServicesResolved()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(
            resolvedServices: [ServiceType.AppleMusic],
            failedServices: [ServiceType.YouTubeMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync("song-1", SongStatus.PartiallyResolved, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillCallSongServiceWithFailedStatusWhenNoServicesResolved()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(
            resolvedServices: [],
            failedServices: [ServiceType.AppleMusic, ServiceType.YouTubeMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync("song-1", SongStatus.Failed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillSetCompletedAtOnSaga()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(resolvedServices: [ServiceType.AppleMusic]);
        var beforeExecute = DateTime.UtcNow;

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        saga.CompletedAt.Should().NotBeNull();
        saga.CompletedAt.Should().BeOnOrAfter(beforeExecute);
    }

    [Fact]
    public async Task ItWillCallShareStatusServiceWithCompletedStatus()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(resolvedServices: [ServiceType.AppleMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<IShareStatusService>().Verify(
            x => x.UpdateStatusAsync("share-abc123", ShareStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillCallRevalidateAsyncWithShareId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(resolvedServices: [ServiceType.AppleMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<IFrontendRevalidateService>().Verify(
            x => x.RevalidateShareAsync("share-abc123"),
            Times.Once);
    }

    [Fact]
    public async Task ItWillCallNextExecuteForSourceMetadataResolved()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(resolvedServices: [ServiceType.AppleMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        next.Verify(x => x.Execute(context.Object), Times.Once);
    }

    [Fact]
    public async Task ItWillPassSongIdFromSagaToSongService()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(songId: null, resolvedServices: [ServiceType.AppleMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync(null, SongStatus.Resolved, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Execute via ServiceLinkResolved

    [Fact]
    public async Task ItWillCompleteViaServiceLinkResolvedMessage()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(resolvedServices: [ServiceType.AppleMusic, ServiceType.YouTubeMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, ServiceLinkResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, ServiceLinkResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync("song-1", SongStatus.Resolved, It.IsAny<CancellationToken>()), Times.Once);
        mock.Mock<IShareStatusService>().Verify(
            x => x.UpdateStatusAsync("share-abc123", ShareStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
        saga.CompletedAt.Should().NotBeNull();
        next.Verify(x => x.Execute(context.Object), Times.Once);
    }

    [Fact]
    public async Task ItWillCallNextExecuteForServiceLinkResolved()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(songId: null, resolvedServices: [ServiceType.AppleMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, ServiceLinkResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, ServiceLinkResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        next.Verify(x => x.Execute(context.Object), Times.Once);
    }

    #endregion

    #region Execute via ServiceLinkFailed

    [Fact]
    public async Task ItWillCompleteViaServiceLinkFailedMessage()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(
            resolvedServices: [],
            failedServices: [ServiceType.AppleMusic, ServiceType.YouTubeMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, ServiceLinkFailed>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, ServiceLinkFailed>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync("song-1", SongStatus.Failed, It.IsAny<CancellationToken>()), Times.Once);
        mock.Mock<IShareStatusService>().Verify(
            x => x.UpdateStatusAsync("share-abc123", ShareStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
        saga.CompletedAt.Should().NotBeNull();
        next.Verify(x => x.Execute(context.Object), Times.Once);
    }

    [Fact]
    public async Task ItWillCallNextExecuteForServiceLinkFailed()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(songId: null, failedServices: [ServiceType.AppleMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, ServiceLinkFailed>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, ServiceLinkFailed>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        next.Verify(x => x.Execute(context.Object), Times.Once);
    }

    [Fact]
    public async Task ItWillCallRevalidateForServiceLinkFailed()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(songId: null, failedServices: [ServiceType.AppleMusic]);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, ServiceLinkFailed>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, ServiceLinkFailed>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<IFrontendRevalidateService>().Verify(
            x => x.RevalidateShareAsync("share-abc123"),
            Times.Once);
    }

    #endregion

    #region Song status determination edge cases

    [Fact]
    public async Task ItWillSetResolvedWhenOnlyResolvedServicesExist()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(
            resolvedServices: [ServiceType.Spotify],
            failedServices: []);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync("song-1", SongStatus.Resolved, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillSetResolvedWhenBothListsAreEmpty()
    {
        // Arrange - 0 resolved out of 0 total: 0 == 0 is true, so Resolved
        // This is an edge case: no services at all means "resolved" by the logic
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(resolvedServices: [], failedServices: []);

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataResolved>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        // 0 resolved == 0 total => Resolved (vacuously true)
        mock.Mock<ISongService>().Verify(
            x => x.UpdateStatusAsync("song-1", SongStatus.Resolved, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Faulted delegation

    [Fact]
    public async Task ItWillDelegateFaultedForSourceMetadataResolved()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var faultContext = new Mock<BehaviorExceptionContext<ShareRequestSagaState, SourceMetadataResolved, Exception>>();
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Faulted(faultContext.Object, next.Object);

        // Assert
        next.Verify(x => x.Faulted(faultContext.Object), Times.Once);
    }

    [Fact]
    public async Task ItWillDelegateFaultedForServiceLinkResolved()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var faultContext = new Mock<BehaviorExceptionContext<ShareRequestSagaState, ServiceLinkResolved, Exception>>();
        var next = new Mock<IBehavior<ShareRequestSagaState, ServiceLinkResolved>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Faulted(faultContext.Object, next.Object);

        // Assert
        next.Verify(x => x.Faulted(faultContext.Object), Times.Once);
    }

    [Fact]
    public async Task ItWillDelegateFaultedForServiceLinkFailed()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var faultContext = new Mock<BehaviorExceptionContext<ShareRequestSagaState, ServiceLinkFailed, Exception>>();
        var next = new Mock<IBehavior<ShareRequestSagaState, ServiceLinkFailed>>();

        var sut = mock.Create<CompleteSagaActivity>();

        // Act
        await sut.Faulted(faultContext.Object, next.Object);

        // Assert
        next.Verify(x => x.Faulted(faultContext.Object), Times.Once);
    }

    #endregion
}
