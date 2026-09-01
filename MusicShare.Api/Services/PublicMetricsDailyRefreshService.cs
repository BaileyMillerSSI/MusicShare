using MassTransit;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;

namespace MusicShare.Api.Services;

/// <summary>Publishes a metrics rebuild at each UTC midnight boundary.</summary>
public class PublicMetricsDailyRefreshService(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicMetricsDailyRefreshService> logger,
    Func<DateTime>? utcNow = null,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
    TimeSpan? retryDelay = null) : BackgroundService
{
    private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync = delayAsync ?? Task.Delay;
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _delayAsync(GetDelayUntilNextMidnightUtc(_utcNow()), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Daily public metrics refresh failed; retrying");
                await _delayAsync(_retryDelay, stoppingToken);
                continue;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishRefreshAsync(stoppingToken);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Daily public metrics refresh failed; retrying");

                    try
                    {
                        await _delayAsync(_retryDelay, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }
    }

    public static TimeSpan GetDelayUntilNextMidnightUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The current time must be UTC.", nameof(utcNow));

        return utcNow.Date.AddDays(1) - utcNow;
    }

    private async Task PublishRefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()
            .Publish(new RefreshPublicMetrics(), cancellationToken);
    }
}
