namespace MusicShare.Services.Models;

public record ShareResultResponse
{
    public required string ShareId { get; init; }
    public required string Status { get; init; }
    public SongDetails? Song { get; init; }
}
