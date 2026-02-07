namespace MusicShare.Persistence.Configuration;

public class MongoDbSettings
{
    public const string SectionName = "MongoDB";

    public string DatabaseName { get; set; } = "musicshare";
}
