namespace MusicShare.Services.Services
{
public interface IFrontendRevalidateService
{
    Task<bool> RevalidateShareAsync(string shareId, CancellationToken cancellationToken = default);
    Task<bool> RevalidateMetricsAsync(CancellationToken cancellationToken = default);
}
}
