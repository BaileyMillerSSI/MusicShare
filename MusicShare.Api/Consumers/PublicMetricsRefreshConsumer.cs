using MassTransit;
using MusicShare.Api.Services;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;

namespace MusicShare.Api.Consumers;

public class PublicMetricsRefreshConsumer(
    IPublicMetricsService metrics,
    IFrontendRevalidateService revalidateService,
    IPublicMetricsInvalidationRetryService invalidationRetries,
    ILogger<PublicMetricsRefreshConsumer> logger) : IConsumer<RefreshPublicMetrics>
{
    public async Task Consume(ConsumeContext<RefreshPublicMetrics> context)
    {
        var result = await metrics.RefreshAsync(context.CancellationToken);
        if (!result.Accepted)
        {
            logger.LogInformation("Public metrics refresh was superseded by a newer snapshot");
            return;
        }

        if (!await revalidateService.RevalidateMetricsAsync(context.CancellationToken))
        {
            invalidationRetries.ScheduleRetry();
        }
    }
}
