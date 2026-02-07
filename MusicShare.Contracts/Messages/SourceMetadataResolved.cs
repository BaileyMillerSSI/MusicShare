namespace MusicShare.Contracts.Messages;

/// <summary>
/// Event published when source metadata has been successfully resolved.
/// </summary>
public record SourceMetadataResolved
{
    public required Guid CorrelationId { get; init; }
    public required string SongId { get; init; }
    public required string ShareId { get; init; }
    public required ServiceType SourceService { get; init; }
    public required SongMetadataPayload Metadata { get; init; }
}
