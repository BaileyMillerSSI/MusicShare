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

/// <summary>
/// Payload containing song metadata for saga state and message passing.
/// Uses concrete List for better serialization support.
/// </summary>
public record SongMetadataPayload
{
    public required string Title { get; init; }
    public required List<string> Artists { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
    public TimeSpan? Duration { get; init; }
}
