namespace MusicShare.Contracts.Messages;

public record ServiceLinkResolved
{
    public required string SongId { get; init; }
    public required ServiceType ServiceType { get; init; }
    public required string Url { get; init; }
}
