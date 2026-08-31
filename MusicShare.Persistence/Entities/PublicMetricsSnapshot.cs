using MongoDB.Bson.Serialization.Attributes;
using MusicShare.Contracts;

namespace MusicShare.Persistence.Entities;

public class PublicMetricsSnapshot
{
    public const string SingletonId = "public-metrics";

    [BsonId]
    public string Id { get; set; } = SingletonId;
    public long TotalCompletedSongs { get; set; }
    /// <summary>Monotonically ordered candidate watermark used to reject stale equal-total refreshes.</summary>
    public long SnapshotVersion { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<PublicMetricsServiceCount> ServiceCounts { get; set; } = [];
    public List<PublicMetricsRecentSong> RecentSongs { get; set; } = [];
}

public class PublicMetricsServiceCount
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public ServiceType Service { get; set; }
    public long Count { get; set; }
}

public class PublicMetricsRecentSong
{
    public string SongId { get; set; } = string.Empty;
    public string ShareId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = [];
    public string? Album { get; set; }
    public string? ArtworkUrl { get; set; }
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public ServiceType SourceService { get; set; }
    public DateTime CreatedAt { get; set; }
}
