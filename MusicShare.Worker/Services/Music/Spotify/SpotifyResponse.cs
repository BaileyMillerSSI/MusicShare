namespace MusicShare.Worker.Services.Music.Spotify
{
    public class SpotifyResponse
    {
        public SpotifyArtist[] artists { get; set; }

        public SpotifyAlbum album { get; set; }
        public bool _explicit { get; set; }
        public string id { get; set; }
        public bool is_playable { get; set; }
        public string name { get; set; }

        public SpotifyExternalUrls external_urls { get; set; }
    }
}
