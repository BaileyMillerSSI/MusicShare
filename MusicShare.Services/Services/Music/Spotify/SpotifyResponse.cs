using System.Text.Json.Serialization;

namespace MusicShare.Services.Services.Music.Spotify
{
    public class SpotifySearchResponse
    {
        public SpotifyTracksResult? tracks { get; set; }
    }

    public class SpotifyTracksResult
    {
        public SpotifyResponse[]? items { get; set; }
        public int total { get; set; }
    }

    public class SpotifyResponse
    {
        public SpotifyArtist[]? artists { get; set; }

        public SpotifyAlbum? album { get; set; }

        [JsonPropertyName("explicit")]
        public bool Explicit { get; set; }

        /// <summary>
        /// Duration in ms
        /// </summary>
        [JsonPropertyName("duration_ms")]
        public long Duration { get; set; }

        public string? id { get; set; }

        public bool is_playable { get; set; }

        public string? name { get; set; }

        public SpotifyExternalUrls? external_urls { get; set; }
    }
}
