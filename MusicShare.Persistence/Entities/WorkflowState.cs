using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MusicShare.Persistence.Entities;

public class WorkflowState
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("correlationId")]
    [BsonRequired]
    public Guid CorrelationId { get; set; }

    [BsonElement("currentState")]
    [BsonRequired]
    public string CurrentState { get; set; } = string.Empty;

    [BsonElement("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
