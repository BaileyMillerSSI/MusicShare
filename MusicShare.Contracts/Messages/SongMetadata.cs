namespace MusicShare.Contracts.Messages;

/// <summary>
/// Metadata for a song, used by music service adapters.
/// </summary>
public record SongMetadata
{
    public required string Title { get; init; }
    public required IEnumerable<string> Artists { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
    public TimeSpan? Duration { get; init; }
}
