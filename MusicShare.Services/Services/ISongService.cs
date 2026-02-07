using MusicShare.Contracts;

namespace MusicShare.Services.Services;

public interface ISongService
{
    Task UpdateStatusAsync(string? songId, SongStatus status, CancellationToken cancellationToken = default);
}
