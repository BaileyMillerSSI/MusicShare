using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MusicShare.Api.Services;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;

namespace MusicShare.Tests.Unit.Api.Services;

public class PublicMetricsBootstrapServiceTests
{
    [Fact]
    public async Task ItWillCreateMetricsIndexesAndPublishTheInitialRefresh()
    {
        var indexes = new Mock<IMongoIndexManager<ShareRequest>>();
        indexes.Setup(x => x.CreateManyAsync(It.IsAny<IEnumerable<CreateIndexModel<ShareRequest>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["status_source", "status_created_song"]);
        var context = new Mock<IMusicShareDbContext>();
        context.SetupGet(x => x.ShareRequests.Indexes).Returns(indexes.Object);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publish = new Mock<IPublishEndpoint>();
        publish.Setup(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()))
            .Callback(() => published.TrySetResult())
            .Returns(Task.CompletedTask);
        var service = CreateSut(context.Object, publish.Object);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        indexes.Verify(x => x.CreateManyAsync(It.Is<IEnumerable<CreateIndexModel<ShareRequest>>>(models => models.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillNotCrashApiStartupWhenMetricsBootstrapFails()
    {
        var context = new Mock<IMusicShareDbContext>();
        context.SetupGet(x => x.ShareRequests).Throws(new MongoException("unavailable"));
        var service = CreateSut(context.Object, Mock.Of<IPublishEndpoint>());

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    private static PublicMetricsBootstrapService CreateSut(IMusicShareDbContext context, IPublishEndpoint publish)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IMusicShareDbContext))).Returns(context);
        provider.Setup(x => x.GetService(typeof(IPublishEndpoint))).Returns(publish);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(x => x.CreateScope()).Returns(scope.Object);
        return new PublicMetricsBootstrapService(scopes.Object, Mock.Of<ILogger<PublicMetricsBootstrapService>>());
    }
}
