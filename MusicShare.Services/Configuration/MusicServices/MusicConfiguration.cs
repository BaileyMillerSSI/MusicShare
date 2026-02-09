namespace MusicShare.Services.Configuration.MusicServices
{
    public class MusicConfiguration
    {
        public const string SectionName = "Music";

        public double ConfidenceThreshold { get; set; } = 0.80;
    }
}
