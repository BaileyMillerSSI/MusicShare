namespace MusicShare.Api.Models;

public record SongDetails
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required List<string> Artists { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
    public TimeSpan? Duration { get; init; }
    public bool? IsExplicit { get; init; }
    public required string Status { get; init; }
    public required List<ServiceLink> Links { get; init; }
}
