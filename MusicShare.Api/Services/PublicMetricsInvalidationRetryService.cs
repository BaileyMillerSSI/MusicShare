using System.Threading.Channels;
using MusicShare.Services.Services;

namespace MusicShare.Api.Services;

/// <summary>Retries only the cheap frontend invalidation after a snapshot has been stored.</summary>
public class PublicMetricsInvalidationRetryService(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicMetricsInvalidationRetryService> logger,
    TimeSpan? retryDelay = null) : BackgroundService, IPublicMetricsInvalidationRetryService
{
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromSeconds(15);
    private readonly Channel<bool> _scheduled = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void ScheduleRetry() => _scheduled.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _scheduled.Reader.WaitToReadAsync(stoppingToken))
        {
            while (_scheduled.Reader.TryRead(out _)) { }

            do
            {
                await Task.Delay(_retryDelay, stoppingToken);
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var revalidate = scope.ServiceProvider.GetRequiredService<IFrontendRevalidateService>();
                    if (await revalidate.RevalidateMetricsAsync(stoppingToken)) break;

                    logger.LogInformation("Metrics snapshot is stored, but frontend is not ready for invalidation; retrying");
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Metrics frontend invalidation retry failed; retrying");
                }
            }
            while (!stoppingToken.IsCancellationRequested);
        }
    }
}
