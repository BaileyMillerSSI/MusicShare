namespace MusicShare.Contracts.Messages;

/// <summary>
/// Command sent by the saga to resolve metadata from the source service.
/// </summary>
public record ResolveSourceMetadata
{
    public required Guid CorrelationId { get; init; }
    public required string ShareId { get; init; }
    public required string SourceUrl { get; init; }
    public required ServiceType SourceService { get; init; }
}
