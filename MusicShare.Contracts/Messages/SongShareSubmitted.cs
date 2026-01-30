namespace MusicShare.Contracts.Messages;

public record SongShareSubmitted
{
    public required string ShareId { get; init; }
    public required string SourceUrl { get; init; }
    public required ServiceType SourceService { get; init; }
    public required Guid CorrelationId { get; init; }
}
