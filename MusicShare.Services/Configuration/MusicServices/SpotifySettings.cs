namespace MusicShare.Services.Configuration.MusicServices
{
    public class SpotifyConfiguration
    {
        public const string SectionName = "Spotify";

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public double ConfidenceThreshold { get; set; } = 0.80;
    }
}
