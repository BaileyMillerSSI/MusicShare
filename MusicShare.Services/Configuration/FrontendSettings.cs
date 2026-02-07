namespace MusicShare.Services.Configuration
{
    public class FrontendSettings
    {
        public const string SectionName = "Frontend";
        public string RevalidationSecret { get; set; } = string.Empty;
        public string UrlHttp { get; set; } = string.Empty;
        public string UrlHttps { get; set; } = string.Empty;

        public Uri Uri => new(!string.IsNullOrEmpty(UrlHttps) ? UrlHttps : UrlHttp);
    }
}
