namespace MusicShare.Contracts.Messages;

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
    public bool? IsExplicit { get; init; }
}
