using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MusicShare.Contracts;

namespace MusicShare.Persistence.Entities;

public class Song
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("title")]
    [BsonRequired]
    public string Title { get; set; } = string.Empty;

    [BsonElement("artists")]
    [BsonRequired]
    public List<string> Artists { get; set; } = [];

    [BsonElement("album")]
    public string? Album { get; set; }

    [BsonElement("artworkUrl")]
    public string? ArtworkUrl { get; set; }

    [BsonElement("duration")]
    public TimeSpan? Duration { get; set; }

    [BsonElement("isExplicit")]
    public bool? IsExplicit { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public SongStatus Status { get; set; } = SongStatus.Pending;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
