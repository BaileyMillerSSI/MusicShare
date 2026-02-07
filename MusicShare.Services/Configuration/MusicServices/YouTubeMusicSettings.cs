namespace MusicShare.Services.Configuration.MusicServices
{
    public class YouTubeMusicConfiguration
    {
        public const string SectionName = "YouTubeMusic";

        /// <summary>
        /// Geographic location for search results (e.g., "US", "GB", "DE").
        /// </summary>
        public string GeographicLocation { get; set; } = "US";
    }
}
