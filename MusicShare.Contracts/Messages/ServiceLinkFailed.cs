namespace MusicShare.Contracts.Messages;

/// <summary>
/// Event published when a song could not be resolved on a music service.
/// </summary>
public record ServiceLinkFailed
{
    public required Guid CorrelationId { get; init; }
    public required string SongId { get; init; }
    public required ServiceType ServiceType { get; init; }
    public required string ErrorMessage { get; init; }
}
