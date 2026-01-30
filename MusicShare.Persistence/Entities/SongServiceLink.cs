using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MusicShare.Contracts;

namespace MusicShare.Persistence.Entities;

public class SongServiceLink
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("songId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonRequired]
    public string SongId { get; set; } = string.Empty;

    [BsonElement("serviceType")]
    [BsonRepresentation(BsonType.String)]
    [BsonRequired]
    public ServiceType ServiceType { get; set; }

    [BsonElement("serviceSongId")]
    [BsonRequired]
    public string ServiceSongId { get; set; } = string.Empty;

    [BsonElement("originalUrl")]
    [BsonRequired]
    public string OriginalUrl { get; set; } = string.Empty;

    [BsonElement("normalizedUrl")]
    [BsonRequired]
    public string NormalizedUrl { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
