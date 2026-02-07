namespace MusicShare.Services.Configuration
{
    public class FrontendSettings
    {
        public const string SectionName = "Frontend";
        public string RevalidationSecret { get; set; } = string.Empty;

        public static Uri Uri => new("https+http://frontend");
    }
}
