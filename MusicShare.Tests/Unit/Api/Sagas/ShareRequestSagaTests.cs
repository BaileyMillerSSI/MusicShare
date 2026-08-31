using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;
using MusicShare.Api.Sagas.ShareRequest;
using MusicShare.Api.Sagas.ShareRequest.Activities;

namespace MusicShare.Tests.Unit.Api.Sagas;

public class ShareRequestSagaTests
{
    private sealed class TestHarnessContext : IAsyncDisposable
    {
        public ServiceProvider Provider { get; init; } = null!;
        public ITestHarness Harness { get; init; } = null!;
        public ISagaStateMachineTestHarness<ShareRequestSaga, ShareRequestSagaState> SagaHarness { get; init; } = null!;

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
        }
    }

    private static IMusicServiceResolver CreateDefaultResolver()
    {
        var spotifyAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.Spotify);
        var appleAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.AppleMusic);
        var youtubeAdapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == ServiceType.YouTubeMusic);

        return new MusicServiceResolver([spotifyAdapter, appleAdapter, youtubeAdapter]);
    }

    private static IMusicServiceResolver CreateSingleServiceResolver(ServiceType serviceType = ServiceType.Spotify)
    {
        var adapter = Mock.Of<IMusicServiceAdapter>(a => a.ServiceType == serviceType);
        return new MusicServiceResolver([adapter]);
    }

    private static async Task<TestHarnessContext> CreateHarness(IMusicServiceResolver? resolver = null)
    {
        resolver ??= CreateDefaultResolver();

        var services = new ServiceCollection();

        // Saga constructor dependency
        services.AddSingleton(resolver);

        // Activity dependencies (CompleteSagaActivity and FailSagaActivity)
        var mockSongService = new Mock<ISongService>();
        mockSongService
            .Setup(x => x.UpdateStatusAsync(It.IsAny<string?>(), It.IsAny<SongStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockShareStatusService = new Mock<IShareStatusService>();
        mockShareStatusService
            .Setup(x => x.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<ShareStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRevalidateService = new Mock<IFrontendRevalidateService>();
        mockRevalidateService
            .Setup(x => x.RevalidateShareAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        services.AddSingleton(mockSongService.Object);
        services.AddSingleton(mockShareStatusService.Object);
        services.AddSingleton(mockRevalidateService.Object);

        services.AddLogging();

        // Register activities so the saga can resolve them via DI
        services.AddTransient<CompleteSagaActivity>();
        services.AddTransient<FailSagaActivity>();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<ShareRequestSaga, ShareRequestSagaState>()
                .InMemoryRepository();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<ShareRequestSaga, ShareRequestSagaState>();

        return new TestHarnessContext
        {
            Provider = provider,
            Harness = harness,
            SagaHarness = sagaHarness
        };
    }

    private static SongMetadataPayload CreateTestMetadata() => new()
    {
        Title = "Test Song",
        Artists = ["Test Artist"],
        Album = "Test Album",
        ArtworkUrl = "https://example.com/art.jpg"
    };

    #region SongShareSubmitted (Initial State)

    [Fact]
    public async Task ItWillCreateSagaInstanceOnSongShareSubmitted()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);

        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Created.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.ResolvingMetadata);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillTransitionToResolvingMetadataState()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);

        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.ResolvingMetadata);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillSetShareIdOnSagaState()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-xyz",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);

        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.ResolvingMetadata);
        instance.Should().NotBeNull();
        instance!.ShareId.Should().Be("share-xyz");
    }

    [Fact]
    public async Task ItWillSetSourceServiceOnSagaState()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);

        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.ResolvingMetadata);
        instance.Should().NotBeNull();
        instance!.SourceService.Should().Be(ServiceType.Spotify);
    }

    [Fact]
    public async Task ItWillSetPendingServicesExcludingSource()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);

        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.ResolvingMetadata);
        instance.Should().NotBeNull();
        instance!.PendingServices.Should().BeEquivalentTo(
            [ServiceType.AppleMusic, ServiceType.YouTubeMusic]);
    }

    [Fact]
    public async Task ItWillPublishResolveSourceMetadataCommand()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);

        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        (await ctx.Harness.Published.Any<ResolveSourceMetadata>(x =>
            x.Context.Message.CorrelationId == correlationId &&
            x.Context.Message.ShareId == "share-123" &&
            x.Context.Message.SourceUrl == "https://open.spotify.com/track/abc" &&
            x.Context.Message.SourceService == ServiceType.Spotify, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task ItWillSetCreatedAtTimestamp()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();
        var beforePublish = DateTime.UtcNow;

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);

        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.ResolvingMetadata);
        instance.Should().NotBeNull();
        instance!.CreatedAt.Should().BeOnOrAfter(beforePublish);
        instance.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region SourceMetadataResolved (with pending services)

    [Fact]
    public async Task ItWillTransitionToAwaitingServiceLinksWhenPendingServicesExist()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.AwaitingServiceLinks);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillSetSongIdOnMetadataResolved()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-42",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.AwaitingServiceLinks);
        instance.Should().NotBeNull();
        instance!.SongId.Should().Be("song-42");
    }

    [Fact]
    public async Task ItWillSetMetadataOnMetadataResolved()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();
        var metadata = CreateTestMetadata();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = metadata
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.AwaitingServiceLinks);
        instance.Should().NotBeNull();
        instance!.Metadata.Should().NotBeNull();
        instance.Metadata!.Title.Should().Be("Test Song");
        instance.Metadata.Artists.Should().Contain("Test Artist");
    }

    [Fact]
    public async Task ItWillPublishResolveServiceLinkForEachPendingService()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        (await ctx.Harness.Published.Any<ResolveServiceLink>(x =>
            x.Context.Message.CorrelationId == correlationId &&
            x.Context.Message.TargetService == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        (await ctx.Harness.Published.Any<ResolveServiceLink>(x =>
            x.Context.Message.CorrelationId == correlationId &&
            x.Context.Message.TargetService == ServiceType.YouTubeMusic, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    #endregion

    #region SourceMetadataResolved (no pending services)

    [Fact]
    public async Task ItWillTransitionToCompletedWhenNoPendingServices()
    {
        await using var ctx = await CreateHarness(CreateSingleServiceResolver(ServiceType.Spotify));
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillNotPublishResolveServiceLinkWhenNoPendingServices()
    {
        await using var ctx = await CreateHarness(CreateSingleServiceResolver(ServiceType.Spotify));
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        (await ctx.Harness.Published.Any<ResolveServiceLink>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    #endregion

    #region SourceMetadataFailed

    [Fact]
    public async Task ItWillTransitionToFailedOnMetadataFailed()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataFailed
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            ErrorMessage = "Could not resolve metadata"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataFailed>(TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillNotPublishResolveServiceLinkOnMetadataFailed()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataFailed
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            ErrorMessage = "Could not resolve metadata"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataFailed>(TestContext.Current.CancellationToken)).Should().BeTrue();

        (await ctx.Harness.Published.Any<ResolveServiceLink>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    #endregion

    #region All ServiceLinks Resolved

    [Fact]
    public async Task ItWillTransitionToCompletedWhenAllServiceLinksResolved()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.AppleMusic,
            ResolvedUrl = "https://music.apple.com/track/123"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkResolved>(x =>
            x.Context.Message.ServiceType == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.YouTubeMusic,
            ResolvedUrl = "https://music.youtube.com/watch?v=xyz"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkResolved>(x =>
            x.Context.Message.ServiceType == ServiceType.YouTubeMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillTrackResolvedServicesCorrectly()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.AppleMusic,
            ResolvedUrl = "https://music.apple.com/track/123"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkResolved>(x =>
            x.Context.Message.ServiceType == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.YouTubeMusic,
            ResolvedUrl = "https://music.youtube.com/watch?v=xyz"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkResolved>(x =>
            x.Context.Message.ServiceType == ServiceType.YouTubeMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
        instance!.ResolvedServices.Should().BeEquivalentTo(
            [ServiceType.AppleMusic, ServiceType.YouTubeMusic]);
    }

    [Fact]
    public async Task ItWillRemoveServiceFromPendingOnLinkResolved()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        // Resolve only one service - should stay in AwaitingServiceLinks
        await ctx.Harness.Bus.Publish(new ServiceLinkResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.AppleMusic,
            ResolvedUrl = "https://music.apple.com/track/123"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkResolved>(x =>
            x.Context.Message.ServiceType == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.AwaitingServiceLinks);
        instance.Should().NotBeNull();
        instance!.PendingServices.Should().BeEquivalentTo([ServiceType.YouTubeMusic]);
        instance.ResolvedServices.Should().BeEquivalentTo([ServiceType.AppleMusic]);
    }

    #endregion

    #region All ServiceLinks Failed

    [Fact]
    public async Task ItWillTransitionToCompletedWhenAllServiceLinksFailed()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkFailed
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.AppleMusic,
            ErrorMessage = "Not found"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkFailed>(x =>
            x.Context.Message.ServiceType == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkFailed
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.YouTubeMusic,
            ErrorMessage = "Not found"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkFailed>(x =>
            x.Context.Message.ServiceType == ServiceType.YouTubeMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillTrackFailedServicesCorrectly()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkFailed
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.AppleMusic,
            ErrorMessage = "Not found"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkFailed>(x =>
            x.Context.Message.ServiceType == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkFailed
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.YouTubeMusic,
            ErrorMessage = "Not found"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkFailed>(x =>
            x.Context.Message.ServiceType == ServiceType.YouTubeMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
        instance!.FailedServices.Should().BeEquivalentTo(
            [ServiceType.AppleMusic, ServiceType.YouTubeMusic]);
    }

    #endregion

    #region Mixed Resolved/Failed

    [Fact]
    public async Task ItWillTransitionToCompletedWithMixOfResolvedAndFailed()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.AppleMusic,
            ResolvedUrl = "https://music.apple.com/track/123"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkResolved>(x =>
            x.Context.Message.ServiceType == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkFailed
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.YouTubeMusic,
            ErrorMessage = "Not found"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkFailed>(x =>
            x.Context.Message.ServiceType == ServiceType.YouTubeMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillTrackBothResolvedAndFailedServices()
    {
        await using var ctx = await CreateHarness();
        var correlationId = Guid.NewGuid();

        await ctx.Harness.Bus.Publish(new SongShareSubmitted
        {
            CorrelationId = correlationId,
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SongShareSubmitted>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new SourceMetadataResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ShareId = "share-123",
            SourceService = ServiceType.Spotify,
            Metadata = CreateTestMetadata()
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<SourceMetadataResolved>(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkResolved
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.AppleMusic,
            ResolvedUrl = "https://music.apple.com/track/123"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkResolved>(x =>
            x.Context.Message.ServiceType == ServiceType.AppleMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        await ctx.Harness.Bus.Publish(new ServiceLinkFailed
        {
            CorrelationId = correlationId,
            SongId = "song-1",
            ServiceType = ServiceType.YouTubeMusic,
            ErrorMessage = "Not found"
        }, TestContext.Current.CancellationToken);
        (await ctx.SagaHarness.Consumed.Any<ServiceLinkFailed>(x =>
            x.Context.Message.ServiceType == ServiceType.YouTubeMusic, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = ctx.SagaHarness.Sagas.ContainsInState(
            correlationId, ctx.SagaHarness.StateMachine, ctx.SagaHarness.StateMachine.Final);
        instance.Should().NotBeNull();
        instance!.ResolvedServices.Should().BeEquivalentTo([ServiceType.AppleMusic]);
        instance.FailedServices.Should().BeEquivalentTo([ServiceType.YouTubeMusic]);
        instance.PendingServices.Should().BeEmpty();
    }

    #endregion
}
