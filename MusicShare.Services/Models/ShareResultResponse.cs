namespace MusicShare.Services.Models;

public record ShareResultResponse
{
    /// <summary>The terminal canonical share ID; it can differ from the requested alias ID.</summary>
    public required string ShareId { get; init; }
    public required string Status { get; init; }
    public SongDetails? Song { get; init; }
}
