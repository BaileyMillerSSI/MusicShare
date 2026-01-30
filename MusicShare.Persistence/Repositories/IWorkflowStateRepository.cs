using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public interface IWorkflowStateRepository
{
    Task<WorkflowState?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<WorkflowState> UpsertAsync(WorkflowState state, CancellationToken cancellationToken = default);
}
