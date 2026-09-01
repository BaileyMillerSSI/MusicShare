using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

public interface IPublicMetricsService
{
    Task<PublicMetricsResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<PublicMetricsRefreshResult> RefreshAsync(
        CancellationToken cancellationToken = default,
        bool allowReconciliationDecrease = false);
}
