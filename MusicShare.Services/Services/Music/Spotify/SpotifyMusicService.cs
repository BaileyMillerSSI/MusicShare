using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services.Music;
using System.Net.Http.Json;
using System.Web;

namespace MusicShare.Services.Services.Music.Spotify
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

        public async Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default)
        {
            var query = HttpUtility.UrlEncode(metadata.Title);

            var searchResult = await _httpClient.GetFromJsonAsync<SpotifySearchResponse>(
                $"search?q={query}&type=track&limit=5",
                cancellationToken);

            if (searchResult?.tracks?.items == null || searchResult.tracks.items.Length == 0)
                return null;

            foreach (var track in searchResult.tracks.items)
            {
                if (IsMatch(track, metadata))
                {
                    return track.external_urls?.spotify;
                }
            }

            return null;
        }

        private static bool IsMatch(SpotifyResponse track, SongMetadata metadata)
        {
            if (!string.Equals(track.name, metadata.Title, StringComparison.OrdinalIgnoreCase))
                return false;

            var trackArtists = track.artists?.Select(a => a.name) ?? [];
            var hasArtistMatch = metadata.Artists.Any(inputArtist =>
                trackArtists.Any(trackArtist =>
                    string.Equals(trackArtist, inputArtist, StringComparison.OrdinalIgnoreCase)));

            return hasArtistMatch;
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
                Duration = TimeSpan.FromMilliseconds(apiResponse?.Duration ?? 0),
                IsExplicit = apiResponse?.Explicit
            };
        }
    }
}
