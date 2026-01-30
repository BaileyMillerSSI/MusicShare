namespace MusicShare.Contracts.Messages;

public record SongMetadataResolved
{
    public required string SongId { get; init; }
    public required SongMetadata Metadata { get; init; }
}

public record SongMetadata
{
    public required string Title { get; init; }
    public required IEnumerable<string> Artists { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
    public TimeSpan? Duration { get; init; }
}
