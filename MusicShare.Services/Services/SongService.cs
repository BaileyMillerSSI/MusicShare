using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Services.Services;

public class SongService(ISongRepository songRepository, ILogger<SongService> logger) : ISongService
{
    private readonly ISongRepository _songRepository = songRepository;
    private readonly ILogger<SongService> _logger = logger;

    public async Task UpdateStatusAsync(string? songId, SongStatus status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(songId))
        {
            _logger.LogDebug("Skipping song status update — songId is null or empty");
            return;
        }

        var song = await _songRepository.GetByIdAsync(songId, cancellationToken);
        if (song is null)
        {
            _logger.LogWarning("Song not found for SongId={SongId}, skipping status update", songId);
            return;
        }

        song.Status = status;
        song.UpdatedAt = DateTime.UtcNow;
        await _songRepository.UpdateAsync(song, cancellationToken);

        _logger.LogInformation("Updated song {SongId} status to {Status}", songId, status);
    }
}
