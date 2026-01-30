namespace MusicShare.Contracts.Messages;

/// <summary>
/// Event published when source metadata resolution has failed.
/// </summary>
public record SourceMetadataFailed
{
    public required Guid CorrelationId { get; init; }
    public required string ShareId { get; init; }
    public required string ErrorMessage { get; init; }
}
