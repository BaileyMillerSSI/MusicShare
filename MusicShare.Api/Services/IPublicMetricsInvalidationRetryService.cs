namespace MusicShare.Api.Services;

public interface IPublicMetricsInvalidationRetryService
{
    void ScheduleRetry();
}
