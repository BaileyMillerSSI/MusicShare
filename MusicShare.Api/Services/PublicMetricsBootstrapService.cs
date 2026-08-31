using MassTransit;
using MongoDB.Driver;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;

namespace MusicShare.Api.Services;

public class PublicMetricsBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicMetricsBootstrapService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IMusicShareDbContext>();
            await context.ShareRequests.Indexes.CreateManyAsync([
                new CreateIndexModel<ShareRequest>(Builders<ShareRequest>.IndexKeys
                    .Ascending(x => x.Status).Ascending(x => x.SongId).Ascending(x => x.SourceService)),
                new CreateIndexModel<ShareRequest>(Builders<ShareRequest>.IndexKeys
                    .Ascending(x => x.Status).Descending(x => x.CreatedAt).Ascending(x => x.SongId))
            ], cancellationToken: stoppingToken);
            await context.SongServiceLinks.Indexes.CreateOneAsync(
                new CreateIndexModel<SongServiceLink>(Builders<SongServiceLink>.IndexKeys
                    .Ascending(x => x.ServiceType).Ascending(x => x.SongId)), cancellationToken: stoppingToken);
            await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()
                .Publish(new RefreshPublicMetrics(), stoppingToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Public metrics bootstrap failed; it will be retried by a future refresh");
        }
    }
}
