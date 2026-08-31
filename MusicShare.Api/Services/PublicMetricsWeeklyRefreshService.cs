using MassTransit;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;

namespace MusicShare.Api.Services;

/// <summary>Publishes a metrics rebuild at each Sunday 00:00 UTC boundary.</summary>
public class PublicMetricsWeeklyRefreshService(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicMetricsWeeklyRefreshService> logger,
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
                await _delayAsync(GetDelayUntilNextSundayUtc(_utcNow()), stoppingToken);
                await PublishRefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Weekly public metrics refresh failed; retrying");
                await _delayAsync(_retryDelay, stoppingToken);
            }
        }
    }

    public static TimeSpan GetDelayUntilNextSundayUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The current time must be UTC.", nameof(utcNow));

        return PublicMetricsService.GetSundayStartUtc(utcNow).AddDays(7) - utcNow;
    }

    private async Task PublishRefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()
            .Publish(new RefreshPublicMetrics(), cancellationToken);
    }
}
