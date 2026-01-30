using MusicShare.Contracts;
using MusicShare.MusicAdapters;

namespace MusicShare.Api.Models;

public record ShareResultResponse
{
    public required string ShareId { get; init; }
    public required string Status { get; init; }
    public SongDetails? Song { get; init; }
}

public record SongDetails
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required List<string> Artists { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
    public TimeSpan? Duration { get; init; }
    public required string Status { get; init; }
    public required List<ServiceLink> Links { get; init; }
}

public record ServiceLink
{
    public required ServiceType ServiceType { get; init; }
    public required string Url { get; init; }
}
