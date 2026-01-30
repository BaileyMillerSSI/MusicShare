namespace MusicShare.Worker.Configuration.MusicServices
{
    public class SpotifyConfiguration
    {
        public const string SectionName = "Spotify";

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;
    }
}
