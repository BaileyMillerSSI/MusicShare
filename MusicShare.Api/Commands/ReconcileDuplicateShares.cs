using MassTransit;
using MediatR;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;

namespace MusicShare.Api.Commands;

public static class ReconcileDuplicateShares
{
    public record Request(string FirstShareId, string SecondShareId, string? CanonicalShareId, string Mode, string? Fingerprint) : IRequest<Response>;

    public sealed class Handler(
        IDuplicateShareReconciliationService reconciliation,
        IFrontendRevalidateService revalidation,
        IPublishEndpoint publishEndpoint) : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            var mode = request.Mode switch
            {
                "dry-run" => DuplicateShareReconciliationMode.DryRun,
                "apply" => DuplicateShareReconciliationMode.Apply,
                _ => (DuplicateShareReconciliationMode?)null
            };
            if (mode is null)
                return Response.Failure("Mode must be dry-run or apply.");
            var result = await reconciliation.ReconcileAsync(new(request.FirstShareId, request.SecondShareId, request.CanonicalShareId, mode.Value, request.Fingerprint), cancellationToken);
            if (!result.Success) return Response.From(result);
            // These operations are idempotent. Run them for a successful retry even when the
            // durable alias was already written before a previous follow-up failed.
            if (mode == DuplicateShareReconciliationMode.Apply)
            {
                var canonicalRevalidated = await revalidation.RevalidateShareAsync(result.CanonicalShareId!, cancellationToken);
                var aliasRevalidated = await revalidation.RevalidateShareAsync(result.AliasShareId!, cancellationToken);
                if (!canonicalRevalidated || !aliasRevalidated)
                    return Response.From(result with { Success = false, Error = "The reconciliation was saved but one or more share pages could not be revalidated. Retry apply with the same fingerprint." });
                await publishEndpoint.Publish(new RefreshPublicMetrics(AllowReconciliationDecrease: true), cancellationToken);
            }
            return Response.From(result);
        }
    }

    public record Response(bool Success, bool Changed, string? Error, string? OperationId, string? Fingerprint, string? CanonicalShareId, string? AliasShareId, IReadOnlyList<DuplicateShareIdentity> SharedIdentities, int AffectedShareCount = 0)
    {
        public static Response From(DuplicateShareReconciliationResult result) => new(result.Success, result.Changed, result.Error, result.OperationId, result.Fingerprint, result.CanonicalShareId, result.AliasShareId, result.SharedIdentities, (result.Success || result.Changed) ? 2 : 0);
        public static Response Failure(string error) => new(false, false, error, null, null, null, null, [], 0);
    }
}
