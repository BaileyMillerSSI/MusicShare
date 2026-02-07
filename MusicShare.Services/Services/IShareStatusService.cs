using MusicShare.Contracts;

namespace MusicShare.Services.Services;

public interface IShareStatusService
{
    Task UpdateStatusAsync(string shareId, ShareStatus status, CancellationToken cancellationToken = default);
}
