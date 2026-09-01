using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using MusicShare.Persistence;
using MusicShare.Persistence.Entities;

namespace MusicShare.Api.Services;

/// <summary>Establishes the write-time duplicate prevention invariant before the API accepts traffic.</summary>
public sealed class ShareIdentityIndexInitializer(IMusicShareDbContext context) : IHostedService
{
    public const string IndexName = "ux_share_requests_source_identity";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<ShareRequest>.IndexKeys.Ascending(x => x.SourceIdentityKey);
        var options = new CreateIndexOptions { Name = IndexName, Unique = true, Sparse = true };
        await context.ShareRequests.Indexes.CreateOneAsync(new CreateIndexModel<ShareRequest>(keys, options), cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
