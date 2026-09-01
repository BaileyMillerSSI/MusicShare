using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Services.Services;

public sealed partial class DuplicateShareReconciliationService(
    IShareRequestRepository requests,
    ISongServiceLinkRepository links,
    ISongRepository songs,
    ILogger<DuplicateShareReconciliationService> logger) : IDuplicateShareReconciliationService
{
    public async Task<DuplicateShareReconciliationResult> ReconcileAsync(DuplicateShareReconciliationRequest request, CancellationToken cancellationToken)
    {
        if (!IsShareId(request.FirstShareId) || !IsShareId(request.SecondShareId) || request.FirstShareId == request.SecondShareId)
            return DuplicateShareReconciliationResult.Failure("Exactly two distinct lowercase 12-character share IDs are required.");
        if (request.CanonicalShareId is not null && request.CanonicalShareId != request.FirstShareId && request.CanonicalShareId != request.SecondShareId)
            return DuplicateShareReconciliationResult.Failure("The canonical share must be one of the requested shares.");

        var pair = await requests.GetByShareIdsAsync([request.FirstShareId, request.SecondShareId], cancellationToken);
        if (pair.Count != 2 || pair.Select(x => x.ShareId).Distinct(StringComparer.Ordinal).Count() != 2 ||
            !pair.Select(x => x.ShareId).ToHashSet(StringComparer.Ordinal).SetEquals([request.FirstShareId, request.SecondShareId]))
            return DuplicateShareReconciliationResult.Failure("Both shares must be distinct, completed canonical shares with songs.");
        var existingAlias = pair.SingleOrDefault(x => !string.IsNullOrWhiteSpace(x.CanonicalShareId));
        if (existingAlias is not null && (pair.Count(x => !string.IsNullOrWhiteSpace(x.CanonicalShareId)) != 1 || pair.All(x => x.ShareId != existingAlias.CanonicalShareId) || existingAlias.CanonicalShareId == existingAlias.ShareId))
            return DuplicateShareReconciliationResult.Failure("The shares are already reconciled in a conflicting state.");
        if (existingAlias is not null && request.Mode == DuplicateShareReconciliationMode.DryRun)
            return DuplicateShareReconciliationResult.Failure("The shares are already reconciled.");
        var songIds = pair.Where(x => x.SongId is not null).Select(x => x.SongId!).ToArray();
        var existingSongs = await songs.GetByIdsAsync(songIds, cancellationToken);
        var songLinks = await links.GetBySongIdsAsync(songIds, cancellationToken);

        var canonical = existingAlias is null && request.CanonicalShareId is null
            ? pair.OrderBy(x => x.CreatedAt).ThenBy(x => x.ShareId, StringComparer.Ordinal).First()
            : pair.Single(x => x.ShareId == (existingAlias?.CanonicalShareId ?? request.CanonicalShareId));
        var alias = pair.Single(x => x.ShareId != canonical.ShareId);
        if (request.CanonicalShareId is not null && canonical.ShareId != request.CanonicalShareId)
            return DuplicateShareReconciliationResult.Failure("The canonical share must be one of the requested shares.");
        var preliminary = ReconciliationSnapshots.TryCreate(canonical, alias, existingSongs, songLinks, [], canonical.ReconciliationClaimVersion, alias.ReconciliationClaimVersion);
        if (preliminary is null) return DuplicateShareReconciliationResult.Failure("The shares do not have unambiguous resolved provider evidence.");
        var evidenceOwners = await links.GetByIdentitiesAsync(preliminary.SharedIdentities.Select(x => new SongServiceIdentity(x.ServiceType, x.ServiceSongId)).ToArray(), cancellationToken) ?? [];
        var ownerRequests = await requests.GetBySongIdsAsync(evidenceOwners.Select(x => x.SongId).Distinct(StringComparer.Ordinal).ToArray(), cancellationToken) ?? [];
        var snapshot = ReconciliationSnapshots.TryCreate(canonical, alias, existingSongs, songLinks, ownerRequests, canonical.ReconciliationClaimVersion, alias.ReconciliationClaimVersion);
        if (snapshot is null) return DuplicateShareReconciliationResult.Failure("The provider identities are ambiguous or owned by a third canonical share.");
        if (existingAlias is not null)
        {
            // A successful retry must re-check the current pair and all owners, but it
            // reports the immutable operation/fingerprint persisted by the original CAS.
            // The canonical source key can legitimately have been backfilled by that CAS,
            // so recomputing a new fingerprint here would make a safe retry impossible.
            var expectedOperationId = request.Fingerprint is null ? null : $"reconcile-{request.Fingerprint}";
            if (request.Mode != DuplicateShareReconciliationMode.Apply || request.Fingerprint is null ||
                existingAlias.ReconciliationId != expectedOperationId || existingAlias.ReconciliationFingerprint != request.Fingerprint)
                return DuplicateShareReconciliationResult.Failure("The apply fingerprint does not match the existing reconciliation.");
            LogOutcome(request.Mode, existingAlias.ReconciliationId!, canonical.ShareId, alias.ShareId, snapshot.SharedIdentities, true, false);
            return new(true, false, null, existingAlias.ReconciliationId, existingAlias.ReconciliationFingerprint,
                canonical.ShareId, alias.ShareId, snapshot.SharedIdentities.Select(x => new DuplicateShareIdentity(x.ServiceType, x.ServiceSongId)).ToArray());
        }
        var fingerprint = snapshot.Fingerprint;
        var operationId = $"reconcile-{fingerprint}";
        if (request.Mode == DuplicateShareReconciliationMode.DryRun)
        {
            LogOutcome(request.Mode, operationId, canonical.ShareId, alias.ShareId, snapshot.SharedIdentities, true, false);
            return new(true, false, null, operationId, fingerprint, canonical.ShareId, alias.ShareId, snapshot.SharedIdentities.Select(x => new DuplicateShareIdentity(x.ServiceType, x.ServiceSongId)).ToArray());
        }
        if (request.Fingerprint != fingerprint) return DuplicateShareReconciliationResult.Failure("The apply fingerprint does not match the current reconciliation plan.");

        var write = await requests.TryReconcileAsync(new(canonical.ShareId, alias.ShareId, operationId, fingerprint,
            canonical.SongId!, alias.SongId!, canonical.Status, alias.Status, canonical.CreatedAt, alias.CreatedAt,
            snapshot.CanonicalSourceService, snapshot.CanonicalServiceTrackId, snapshot.CanonicalSourceIdentityKey,
            snapshot.CanonicalPreClaimVersion, snapshot.AliasPreClaimVersion), cancellationToken);
        LogOutcome(request.Mode, operationId, canonical.ShareId, alias.ShareId, snapshot.SharedIdentities, write.Succeeded, write.Changed);
        return new(write.Succeeded, write.Changed, write.Error, operationId, fingerprint, canonical.ShareId, alias.ShareId, snapshot.SharedIdentities.Select(x => new DuplicateShareIdentity(x.ServiceType, x.ServiceSongId)).ToArray());
    }

    private void LogOutcome(DuplicateShareReconciliationMode mode, string operationId, string canonicalShareId, string aliasShareId,
        IReadOnlyList<ReconciliationIdentity> identities, bool success, bool changed) =>
        logger.LogInformation("Duplicate share reconciliation mode={Mode} operation={OperationId} canonical={CanonicalShareId} alias={AliasShareId} serviceTypes={ServiceTypes} success={Success} affectedShareCount={AffectedShareCount} changed={Changed}",
            mode, operationId, canonicalShareId, aliasShareId, string.Join(',', identities.Select(x => x.ServiceType).Distinct().Order()), success, success || changed ? 2 : 0, changed);

    private static bool IsShareId(string value) => ShareIdPattern().IsMatch(value);
    [GeneratedRegex("^[a-f0-9]{12}$", RegexOptions.CultureInvariant)] private static partial Regex ShareIdPattern();
}
