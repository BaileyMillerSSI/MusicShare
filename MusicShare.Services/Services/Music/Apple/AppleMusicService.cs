using Microsoft.Extensions.Logging;

namespace MusicShare.Services.Services.Music.Apple;

/// <summary>
/// Mock implementation of Apple Music API integration service.
/// Returns deterministic fake data for testing and development.
/// TODO: Replace with real Apple Music API integration for production.
/// </summary>
public class AppleMusicService(ILogger<AppleMusicService> logger) : IAppleMusicService
{
    private readonly ILogger<AppleMusicService> _logger = logger;

    public Task<IReadOnlyList<AppleMusicTrack>> SearchTracksAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Mock searching Apple Music for: {Query} (limit: {Limit})", query, limit);

        // Generate deterministic mock results based on query
        var results = GenerateMockSearchResults(query, limit);

        _logger.LogDebug("Returning {Count} mock results for query: {Query}", results.Count, query);
        return Task.FromResult<IReadOnlyList<AppleMusicTrack>>(results);
    }

    public Task<AppleMusicTrack?> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            _logger.LogWarning("Invalid track ID provided");
            return Task.FromResult<AppleMusicTrack?>(null);
        }

        _logger.LogDebug("Mock fetching Apple Music track: {TrackId}", trackId);

        // Return deterministic fake data based on track ID
        var track = new AppleMusicTrack(
            Id: trackId,
            Name: $"Song {trackId}",
            Artists: new List<string> { "Artist A", "Artist B" },
            AlbumName: $"Album {trackId}",
            ArtworkUrl: $"https://is1-ssl.mzstatic.com/image/{trackId}",
            Duration: TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(30)),
            IsExplicit: null);

        _logger.LogDebug("Returning mock track: {TrackId} ({Name})", trackId, track.Name);
        return Task.FromResult<AppleMusicTrack?>(track);
    }

    private static List<AppleMusicTrack> GenerateMockSearchResults(string query, int limit)
    {
        var results = new List<AppleMusicTrack>();

        // Generate deterministic mock track IDs based on query hash
        var baseHash = Math.Abs(query.GetHashCode());

        for (int i = 0; i < limit; i++)
        {
            var trackId = (baseHash + i).ToString();
            results.Add(new AppleMusicTrack(
                Id: trackId,
                Name: $"Mock Song {i + 1}",
                Artists: new List<string> { $"Mock Artist {i + 1}" },
                AlbumName: $"Mock Album {i + 1}",
                ArtworkUrl: $"https://is1-ssl.mzstatic.com/image/mock-{trackId}",
                Duration: TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(15 * i)),
                IsExplicit: i % 3 == 0 ? true : null));
        }

        return results;
    }
}
