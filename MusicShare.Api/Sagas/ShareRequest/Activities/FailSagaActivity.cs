using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;

namespace MusicShare.Api.Sagas.ShareRequest.Activities;

/// <summary>
/// Activity that handles saga failure by updating ShareRequest status.
/// </summary>
public class FailSagaActivity(
    IShareStatusService shareStatusService,
    ILogger<FailSagaActivity> logger) : IStateMachineActivity<ShareRequestSagaState, SourceMetadataFailed>
{
    private readonly IShareStatusService _shareStatusService = shareStatusService;
    private readonly ILogger<FailSagaActivity> _logger = logger;

    public async Task Execute(
        BehaviorContext<ShareRequestSagaState, SourceMetadataFailed> context,
        IBehavior<ShareRequestSagaState, SourceMetadataFailed> next)
    {
        _logger.LogWarning(
            "Marking saga as failed for ShareId={ShareId}",
            context.Saga.ShareId);

        context.Saga.CompletedAt = DateTime.UtcNow;

        await _shareStatusService.UpdateStatusAsync(context.Saga.ShareId, ShareStatus.Failed);

        await next.Execute(context);
    }

    public Task Faulted<TException>(
        BehaviorExceptionContext<ShareRequestSagaState, SourceMetadataFailed, TException> context,
        IBehavior<ShareRequestSagaState, SourceMetadataFailed> next)
        where TException : Exception => next.Faulted(context);

    public void Probe(ProbeContext context) => context.CreateScope("fail-saga");
    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);
}
