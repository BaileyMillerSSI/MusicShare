namespace MusicShare.Services.Services
{
public interface IFrontendRevalidateService
{
    Task RevalidateShareAsync(string shareId);
    Task<bool> RevalidateMetricsAsync();
}
}
