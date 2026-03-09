using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;

namespace MusicShare.Api.Sagas.ShareRequest.Activities;

/// <summary>
/// Activity that completes the saga by updating Song and ShareRequest statuses.
/// </summary>
public class CompleteSagaActivity(
    ISongService songService,
    IShareStatusService shareStatusService,
    IFrontendRevalidateService revalidateService,
    ILogger<CompleteSagaActivity> logger) :
    IStateMachineActivity<ShareRequestSagaState, SourceMetadataResolved>,
    IStateMachineActivity<ShareRequestSagaState, ServiceLinkResolved>,
    IStateMachineActivity<ShareRequestSagaState, ServiceLinkFailed>
{
    private readonly ISongService _songService = songService;
    private readonly IShareStatusService _shareStatusService = shareStatusService;
    private readonly IFrontendRevalidateService _frontendRevalidateService = revalidateService;
    private readonly ILogger<CompleteSagaActivity> _logger = logger;

    public async Task Execute(
        BehaviorContext<ShareRequestSagaState, SourceMetadataResolved> context,
        IBehavior<ShareRequestSagaState, SourceMetadataResolved> next)
    {
        await CompleteAsync(context.Saga);
        await next.Execute(context);
    }

    public async Task Execute(
        BehaviorContext<ShareRequestSagaState, ServiceLinkResolved> context,
        IBehavior<ShareRequestSagaState, ServiceLinkResolved> next)
    {
        await CompleteAsync(context.Saga);
        await next.Execute(context);
    }

    public async Task Execute(
        BehaviorContext<ShareRequestSagaState, ServiceLinkFailed> context,
        IBehavior<ShareRequestSagaState, ServiceLinkFailed> next)
    {
        await CompleteAsync(context.Saga);
        await next.Execute(context);
    }

    private async Task CompleteAsync(ShareRequestSagaState saga)
    {
        saga.CompletedAt = DateTime.UtcNow;

        // Determine final song status
        var totalServices = saga.ResolvedServices.Count + saga.FailedServices.Count;
        var songStatus = saga.ResolvedServices.Count == totalServices
            ? SongStatus.Resolved
            : saga.ResolvedServices.Count > 0
                ? SongStatus.PartiallyResolved
                : SongStatus.Failed;

        _logger.LogInformation(
            "Completing saga for ShareId={ShareId}, SongStatus={SongStatus}, " +
            "Resolved={Resolved}, Failed={Failed}",
            saga.ShareId, songStatus, saga.ResolvedServices.Count, saga.FailedServices.Count);

        await _songService.UpdateStatusAsync(saga.SongId, songStatus);
        await _shareStatusService.UpdateStatusAsync(saga.ShareId, ShareStatus.Completed);

        // Trigger Next.js on-demand ISR revalidation for this share page
        await _frontendRevalidateService
            .RevalidateAsync(new RevalidateFrontendRequest(saga.ShareId));
    }

    public Task Faulted<TException>(
        BehaviorExceptionContext<ShareRequestSagaState, SourceMetadataResolved, TException> context,
        IBehavior<ShareRequestSagaState, SourceMetadataResolved> next)
        where TException : Exception => next.Faulted(context);

    public Task Faulted<TException>(
        BehaviorExceptionContext<ShareRequestSagaState, ServiceLinkResolved, TException> context,
        IBehavior<ShareRequestSagaState, ServiceLinkResolved> next)
        where TException : Exception => next.Faulted(context);

    public Task Faulted<TException>(
        BehaviorExceptionContext<ShareRequestSagaState, ServiceLinkFailed, TException> context,
        IBehavior<ShareRequestSagaState, ServiceLinkFailed> next)
        where TException : Exception => next.Faulted(context);

    public void Probe(ProbeContext context) => context.CreateScope("complete-saga");
    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);
}
