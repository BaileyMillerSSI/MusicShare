using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.MusicAdapters.Services;

namespace MusicShare.Worker.Sagas;

/// <summary>
/// Saga state machine that orchestrates the share request workflow.
/// Coordinates source metadata resolution and parallel service link resolution.
/// </summary>
public class ShareRequestSaga : MassTransitStateMachine<ShareRequestSagaState>
{
    private readonly ILogger<ShareRequestSaga> _logger;

    // States
    public State ResolvingMetadata { get; private set; } = null!;
    public State AwaitingServiceLinks { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    // Events
    public Event<SongShareSubmitted> ShareSubmitted { get; private set; } = null!;
    public Event<SourceMetadataResolved> MetadataResolved { get; private set; } = null!;
    public Event<SourceMetadataFailed> MetadataFailed { get; private set; } = null!;
    public Event<ServiceLinkResolved> LinkResolved { get; private set; } = null!;
    public Event<ServiceLinkFailed> LinkFailed { get; private set; } = null!;

    public ShareRequestSaga(
        IMusicServiceResolver serviceResolver,
        ILogger<ShareRequestSaga> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        // Configure event correlation
        Event(() => ShareSubmitted, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => MetadataResolved, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => MetadataFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => LinkResolved, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => LinkFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        // Initial state - receive SongShareSubmitted
        Initially(
            When(ShareSubmitted)
                .Then(ctx =>
                {
                    _logger.LogInformation(
                        "Saga started for ShareId={ShareId}, CorrelationId={CorrelationId}",
                        ctx.Message.ShareId, ctx.Message.CorrelationId);

                    ctx.Saga.ShareId = ctx.Message.ShareId;
                    ctx.Saga.SourceService = ctx.Message.SourceService;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;

                    // Determine which services we'll need to resolve (exclude source)
                    ctx.Saga.PendingServices = serviceResolver.GetAllAdapters()
                        .Select(a => a.ServiceType)
                        .Where(s => s != ctx.Message.SourceService)
                        .ToList();
                })
                .Publish(ctx => new ResolveSourceMetadata
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ShareId = ctx.Message.ShareId,
                    SourceUrl = ctx.Message.SourceUrl,
                    SourceService = ctx.Message.SourceService
                })
                .TransitionTo(ResolvingMetadata)
        );

        // Waiting for source metadata resolution
        During(ResolvingMetadata,
            When(MetadataResolved)
                .Then(ctx =>
                {
                    _logger.LogInformation(
                        "Source metadata resolved for ShareId={ShareId}, SongId={SongId}",
                        ctx.Message.ShareId, ctx.Message.SongId);

                    ctx.Saga.SongId = ctx.Message.SongId;
                    ctx.Saga.Metadata = ctx.Message.Metadata;
                })
                .If(ctx => ctx.Saga.PendingServices.Count == 0,
                    // No other services to resolve, complete immediately
                    noOtherServices => noOtherServices
                        .Then(ctx => _logger.LogInformation("No other services to resolve, completing saga"))
                        .Activity(x => x.OfType<CompleteSagaActivity>())
                        .TransitionTo(Completed)
                        .Finalize())
                .If(ctx => ctx.Saga.PendingServices.Count > 0,
                    // Fanout to all other services
                    hasOtherServices => hasOtherServices
                        .ThenAsync(async ctx =>
                        {
                            _logger.LogInformation(
                                "Fanning out to {Count} services for SongId={SongId}",
                                ctx.Saga.PendingServices.Count, ctx.Saga.SongId);

                            foreach (var service in ctx.Saga.PendingServices)
                            {
                                await ctx.Publish(new ResolveServiceLink
                                {
                                    CorrelationId = ctx.Saga.CorrelationId,
                                    SongId = ctx.Saga.SongId!,
                                    ShareId = ctx.Saga.ShareId,
                                    TargetService = service,
                                    Metadata = ctx.Saga.Metadata!
                                });
                            }
                        })
                        .TransitionTo(AwaitingServiceLinks)),

            When(MetadataFailed)
                .Then(ctx =>
                {
                    _logger.LogWarning(
                        "Source metadata resolution failed for ShareId={ShareId}: {Error}",
                        ctx.Message.ShareId, ctx.Message.ErrorMessage);
                })
                .Activity(x => x.OfType<FailSagaActivity>())
                .TransitionTo(Failed)
                .Finalize()
        );

        // Waiting for all service link resolutions
        During(AwaitingServiceLinks,
            When(LinkResolved)
                .Then(ctx =>
                {
                    _logger.LogInformation(
                        "Service link resolved: SongId={SongId}, Service={Service}",
                        ctx.Message.SongId, ctx.Message.ServiceType);

                    ctx.Saga.PendingServices.Remove(ctx.Message.ServiceType);
                    ctx.Saga.ResolvedServices.Add(ctx.Message.ServiceType);
                })
                .If(ctx => ctx.Saga.PendingServices.Count == 0,
                    allComplete => allComplete
                        .Activity(x => x.OfType<CompleteSagaActivity>())
                        .TransitionTo(Completed)
                        .Finalize()),

            When(LinkFailed)
                .Then(ctx =>
                {
                    _logger.LogWarning(
                        "Service link failed: SongId={SongId}, Service={Service}, Error={Error}",
                        ctx.Message.SongId, ctx.Message.ServiceType, ctx.Message.ErrorMessage);

                    ctx.Saga.PendingServices.Remove(ctx.Message.ServiceType);
                    ctx.Saga.FailedServices.Add(ctx.Message.ServiceType);
                })
                .If(ctx => ctx.Saga.PendingServices.Count == 0,
                    allComplete => allComplete
                        .Activity(x => x.OfType<CompleteSagaActivity>())
                        .TransitionTo(Completed)
                        .Finalize())
        );

        // Cleanup completed sagas
        //SetCompletedWhenFinalized();
    }
}

/// <summary>
/// Activity that completes the saga by updating Song and ShareRequest statuses.
/// </summary>
public class CompleteSagaActivity :
    IStateMachineActivity<ShareRequestSagaState, SourceMetadataResolved>,
    IStateMachineActivity<ShareRequestSagaState, ServiceLinkResolved>,
    IStateMachineActivity<ShareRequestSagaState, ServiceLinkFailed>
{
    private readonly ISongRepository _songRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ILogger<CompleteSagaActivity> _logger;

    public CompleteSagaActivity(
        ISongRepository songRepository,
        IShareRequestRepository shareRequestRepository,
        ILogger<CompleteSagaActivity> logger)
    {
        _songRepository = songRepository;
        _shareRequestRepository = shareRequestRepository;
        _logger = logger;
    }

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

        // Update Song status
        if (!string.IsNullOrEmpty(saga.SongId))
        {
            var song = await _songRepository.GetByIdAsync(saga.SongId);
            if (song != null)
            {
                song.Status = songStatus;
                song.UpdatedAt = DateTime.UtcNow;
                await _songRepository.UpdateAsync(song);
            }
        }

        // Update ShareRequest status
        var shareRequest = await _shareRequestRepository.GetByShareIdAsync(saga.ShareId);
        if (shareRequest != null)
        {
            shareRequest.Status = ShareStatus.Completed;
            await _shareRequestRepository.UpdateAsync(shareRequest);
        }
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

/// <summary>
/// Activity that handles saga failure by updating ShareRequest status.
/// </summary>
public class FailSagaActivity : IStateMachineActivity<ShareRequestSagaState, SourceMetadataFailed>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ILogger<FailSagaActivity> _logger;

    public FailSagaActivity(
        IShareRequestRepository shareRequestRepository,
        ILogger<FailSagaActivity> logger)
    {
        _shareRequestRepository = shareRequestRepository;
        _logger = logger;
    }

    public async Task Execute(
        BehaviorContext<ShareRequestSagaState, SourceMetadataFailed> context,
        IBehavior<ShareRequestSagaState, SourceMetadataFailed> next)
    {
        _logger.LogWarning(
            "Marking saga as failed for ShareId={ShareId}",
            context.Saga.ShareId);

        context.Saga.CompletedAt = DateTime.UtcNow;

        var shareRequest = await _shareRequestRepository.GetByShareIdAsync(context.Saga.ShareId);
        if (shareRequest != null)
        {
            shareRequest.Status = ShareStatus.Failed;
            await _shareRequestRepository.UpdateAsync(shareRequest);
        }

        await next.Execute(context);
    }

    public Task Faulted<TException>(
        BehaviorExceptionContext<ShareRequestSagaState, SourceMetadataFailed, TException> context,
        IBehavior<ShareRequestSagaState, SourceMetadataFailed> next)
        where TException : Exception => next.Faulted(context);

    public void Probe(ProbeContext context) => context.CreateScope("fail-saga");
    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);
}
