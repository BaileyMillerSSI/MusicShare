using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters.Services;
using System.Net.Http.Json;

namespace MusicShare.MusicAdapters.Services.Music.Spotify
{
    public class SpotifyMusicService(HttpClient spotifyClient): IMusicServiceAdapter
    {
        private readonly HttpClient _httpClient = spotifyClient;

        public ServiceType ServiceType => ServiceType.Spotify;

        public string? ExtractSongId(string url)
        {
            // Extract from URLs like:
            // https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6
            // spotify:track:6rqhFgbbKwnb9MLmUQDhG6

            if (url.Contains("spotify.com/track/"))
            {
                var parts = url.Split('/');
                var trackPart = parts.LastOrDefault(p => !string.IsNullOrEmpty(p));
                return trackPart?.Split('?')[0]; // Remove query params
            }

            if (url.StartsWith("spotify:track:"))
            {
                return url.Split(':').LastOrDefault();
            }

            return null;
        }

        public Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public string NormalizeUrl(string url)
        {
            var songId = ExtractSongId(url);
            return string.IsNullOrEmpty(songId)
                ? url
                : $"https://open.spotify.com/track/{songId}";
        }

        public async Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken cancellationToken = default)
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<SpotifyResponse>($"tracks/{ExtractSongId(url)}", cancellationToken);

            return new SongMetadata
            { 
                Title = apiResponse?.name ?? string.Empty,
                Artists = apiResponse?.artists?.Select(artist => artist.name) ?? [],
                Album = apiResponse?.album?.name,
                ArtworkUrl = apiResponse?.album?.images?.OrderByDescending(x => x.height)?.FirstOrDefault()?.url,
                Duration = TimeSpan.Zero
            };
        }
    }
}
