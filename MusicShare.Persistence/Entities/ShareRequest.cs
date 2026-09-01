using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MusicShare.Contracts;

namespace MusicShare.Persistence.Entities;

public class ShareRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("shareId")]
    [BsonRequired]
    public string ShareId { get; set; } = string.Empty;

    [BsonElement("sourceUrl")]
    [BsonRequired]
    public string SourceUrl { get; set; } = string.Empty;

    [BsonElement("sourceService")]
    [BsonRepresentation(BsonType.String)]
    [BsonRequired]
    public ServiceType SourceService { get; set; }

    [BsonElement("serviceTrackId")]
    public string? ServiceTrackId { get; set; }

    // A versioned, immutable identity used only for newly-created canonical requests.
    [BsonElement("sourceIdentityKey")]
    [BsonIgnoreIfNull]
    public string? SourceIdentityKey { get; set; }

    [BsonElement("canonicalShareId")]
    [BsonIgnoreIfNull]
    public string? CanonicalShareId { get; set; }

    [BsonElement("reconciledAt")]
    [BsonIgnoreIfNull]
    public DateTime? ReconciledAt { get; set; }

    [BsonElement("reconciliationId")]
    [BsonIgnoreIfNull]
    public string? ReconciliationId { get; set; }

    [BsonElement("songId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SongId { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public ShareStatus Status { get; set; } = ShareStatus.Pending;

    [BsonElement("correlationId")]
    [BsonRequired]
    public Guid CorrelationId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
