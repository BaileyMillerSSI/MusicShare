using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;
using MusicShare.Api.Sagas.ShareRequest;
using MusicShare.Api.Sagas.ShareRequest.Activities;

namespace MusicShare.Tests.Unit.Api.Sagas;

public class FailSagaActivityTests
{
    private static ShareRequestSagaState CreateSaga(string shareId = "share-abc123") =>
        new()
        {
            CorrelationId = Guid.NewGuid(),
            CurrentState = "ResolvingMetadata",
            ShareId = shareId,
            SourceService = ServiceType.Spotify,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

    [Fact]
    public async Task ItWillSetCompletedAtOnSaga()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga();
        var beforeExecute = DateTime.UtcNow;

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataFailed>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataFailed>>();

        var sut = mock.Create<FailSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        saga.CompletedAt.Should().NotBeNull();
        saga.CompletedAt.Should().BeOnOrAfter(beforeExecute);
    }

    [Fact]
    public async Task ItWillCallShareStatusServiceWithFailedStatus()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga();

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataFailed>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataFailed>>();

        var sut = mock.Create<FailSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<IShareStatusService>().Verify(
            x => x.UpdateStatusAsync("share-abc123", ShareStatus.Failed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillCallNextExecute()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga();

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataFailed>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataFailed>>();

        var sut = mock.Create<FailSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        next.Verify(x => x.Execute(context.Object), Times.Once);
    }

    [Fact]
    public async Task ItWillLookupShareRequestByCorrectShareId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var saga = CreateSaga(shareId: "custom-share-id");

        var context = new Mock<BehaviorContext<ShareRequestSagaState, SourceMetadataFailed>>();
        context.Setup(x => x.Saga).Returns(saga);
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataFailed>>();

        var sut = mock.Create<FailSagaActivity>();

        // Act
        await sut.Execute(context.Object, next.Object);

        // Assert
        mock.Mock<IShareStatusService>().Verify(
            x => x.UpdateStatusAsync("custom-share-id", ShareStatus.Failed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillDelegateFaultedToNext()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var faultContext = new Mock<BehaviorExceptionContext<ShareRequestSagaState, SourceMetadataFailed, Exception>>();
        var next = new Mock<IBehavior<ShareRequestSagaState, SourceMetadataFailed>>();

        var sut = mock.Create<FailSagaActivity>();

        // Act
        await sut.Faulted(faultContext.Object, next.Object);

        // Assert
        next.Verify(x => x.Faulted(faultContext.Object), Times.Once);
    }
}
