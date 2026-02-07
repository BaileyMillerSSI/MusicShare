using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.Worker.Sagas.ShareRequest;

/// <summary>
/// State for the ShareRequest saga, persisted to MongoDB.
/// </summary>
public class ShareRequestSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int Version { get; set; }
    public string CurrentState { get; set; } = null!;

    // Request info
    public string ShareId { get; set; } = null!;
    public string? SongId { get; set; }
    public ServiceType SourceService { get; set; }

    // Metadata (cached for fanout)
    public SongMetadataPayload? Metadata { get; set; }

    // Service tracking
    public List<ServiceType> PendingServices { get; set; } = [];
    public List<ServiceType> ResolvedServices { get; set; } = [];
    public List<ServiceType> FailedServices { get; set; } = [];

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
