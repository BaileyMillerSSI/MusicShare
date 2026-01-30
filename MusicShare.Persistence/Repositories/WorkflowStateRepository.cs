using MongoDB.Driver;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class WorkflowStateRepository : IWorkflowStateRepository
{
    private readonly IMongoCollection<WorkflowState> _states;

    public WorkflowStateRepository(MusicShareDbContext context)
    {
        _states = context.WorkflowStates;
    }

    public async Task<WorkflowState?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<WorkflowState>.Filter.Eq(s => s.CorrelationId, correlationId);
        return await _states.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowState> UpsertAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        state.LastUpdated = DateTime.UtcNow;

        var filter = Builders<WorkflowState>.Filter.Eq(s => s.CorrelationId, state.CorrelationId);
        var options = new ReplaceOptions { IsUpsert = true };

        await _states.ReplaceOneAsync(filter, state, options, cancellationToken);
        return state;
    }
}
