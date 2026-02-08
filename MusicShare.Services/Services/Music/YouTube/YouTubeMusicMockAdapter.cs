using YouTubeMusicAPI.Models.Info;
using YouTubeMusicAPI.Models.Search;

namespace MusicShare.Services.Services.Music.YouTube;

/// <summary>
/// Mock YouTube Music service that returns deterministic fake data.
/// TODO: Replace with real YouTube Music API integration for production.
/// </summary>
public class YouTubeMusicMockAdapter : IYouTubeMusicService
{
    public Task<IReadOnlyList<SongSearchResult>> SearchSongsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        // Return empty list for mock
        var results = new List<SongSearchResult>();
        return Task.FromResult<IReadOnlyList<SongSearchResult>>(results);
    }

    public Task<SongVideoInfo?> GetSongVideoInfoAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        // Return null for mock
        return Task.FromResult<SongVideoInfo?>(null);
    }
}
