namespace MusicShare.Contracts.Messages;

/// <summary>
/// Command sent by the saga to resolve a song on a specific music service.
/// </summary>
public record ResolveServiceLink
{
    public required Guid CorrelationId { get; init; }
    public required string SongId { get; init; }
    public required string ShareId { get; init; }
    public required ServiceType TargetService { get; init; }
    public required SongMetadataPayload Metadata { get; init; }
}
