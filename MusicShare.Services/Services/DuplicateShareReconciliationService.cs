using System.Security.Cryptography;
using System.Text;
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
            !pair.Select(x => x.ShareId).ToHashSet(StringComparer.Ordinal).SetEquals([request.FirstShareId, request.SecondShareId]) ||
            pair.Any(x => x.Status != ShareStatus.Completed || string.IsNullOrWhiteSpace(x.SongId)))
            return DuplicateShareReconciliationResult.Failure("Both shares must be distinct, completed canonical shares with songs.");
        var existingAlias = pair.SingleOrDefault(x => !string.IsNullOrWhiteSpace(x.CanonicalShareId));
        if (existingAlias is not null && (pair.Count(x => !string.IsNullOrWhiteSpace(x.CanonicalShareId)) != 1 || pair.All(x => x.ShareId != existingAlias.CanonicalShareId)))
            return DuplicateShareReconciliationResult.Failure("The shares are already reconciled in a conflicting state.");
        if (existingAlias is not null && request.Mode == DuplicateShareReconciliationMode.DryRun)
            return DuplicateShareReconciliationResult.Failure("The shares are already reconciled.");
        var songIds = pair.Select(x => x.SongId!).ToArray();
        var existingSongs = await songs.GetByIdsAsync(songIds, cancellationToken);
        if (existingSongs.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != 2)
            return DuplicateShareReconciliationResult.Failure("Both shares must reference existing, distinct songs.");
        var songLinks = await links.GetBySongIdsAsync(songIds, cancellationToken);
        var identities = pair.Select(x => songLinks.Where(l => l.SongId == x.SongId && Enum.IsDefined(l.ServiceType) && l.ServiceType != ServiceType.Unknown && !string.IsNullOrWhiteSpace(l.ServiceSongId))
            .GroupBy(l => l.ServiceType).ToDictionary(g => g.Key, g => g.Select(l => l.ServiceSongId).Distinct(StringComparer.Ordinal).ToArray())).ToArray();
        if (identities.Any(x => x.Any(y => y.Value.Length != 1)) || identities[0].Keys.Intersect(identities[1].Keys).Any(type => !identities[0][type].SequenceEqual(identities[1][type], StringComparer.Ordinal)))
            return DuplicateShareReconciliationResult.Failure("The provider identities are ambiguous or conflicting.");
        var identitySets = identities.Select(x => x.SelectMany(y => y.Value.Select(id => new DuplicateShareIdentity((int)y.Key, id))).ToHashSet()).ToArray();
        var shared = identitySets[0].Intersect(identitySets[1]).OrderBy(x => x.ServiceType).ThenBy(x => x.ServiceSongId, StringComparer.Ordinal).ToArray();
        if (shared.Length == 0) return DuplicateShareReconciliationResult.Failure("The shares do not have an exact provider identity in common.");

        var canonical = existingAlias is null && request.CanonicalShareId is null
            ? pair.OrderBy(x => x.CreatedAt).ThenBy(x => x.ShareId, StringComparer.Ordinal).First()
            : pair.Single(x => x.ShareId == (existingAlias?.CanonicalShareId ?? request.CanonicalShareId));
        var alias = pair.Single(x => x.ShareId != canonical.ShareId);
        if (request.CanonicalShareId is not null && canonical.ShareId != request.CanonicalShareId)
            return DuplicateShareReconciliationResult.Failure("The canonical share must be one of the requested shares.");
        if (existingAlias is not null)
        {
            var expectedOperationId = request.Fingerprint is null ? null : $"reconcile-{request.Fingerprint}";
            if (request.Fingerprint is null || existingAlias.ReconciliationId != expectedOperationId || existingAlias.ReconciliationFingerprint != request.Fingerprint)
                return DuplicateShareReconciliationResult.Failure("The apply fingerprint does not match the existing reconciliation.");
            return new(true, false, null, existingAlias.ReconciliationId, request.Fingerprint, canonical.ShareId, alias.ShareId, shared);
        }
        var fingerprint = Fingerprint(canonical, alias, shared, identities);
        var operationId = $"reconcile-{fingerprint}";
        if (request.Mode == DuplicateShareReconciliationMode.DryRun)
            return new(true, false, null, operationId, fingerprint, canonical.ShareId, alias.ShareId, shared);
        if (request.Fingerprint != fingerprint) return DuplicateShareReconciliationResult.Failure("The apply fingerprint does not match the current reconciliation plan.");

        var write = await requests.TryReconcileAsync(new(canonical.ShareId, alias.ShareId, operationId, fingerprint,
            canonical.SongId!, alias.SongId!, canonical.Status, alias.Status, canonical.CreatedAt, alias.CreatedAt), cancellationToken);
        logger.LogInformation("Duplicate share reconciliation {OperationId} apply canonical={CanonicalShareId} alias={AliasShareId} providerIdentities={ProviderIdentities} succeeded={Succeeded} changed={Changed}", operationId, canonical.ShareId, alias.ShareId, shared.Select(x => $"{x.ServiceType}:{x.ServiceSongId}").ToArray(), write.Succeeded, write.Changed);
        return new(write.Succeeded, write.Changed, write.Error, operationId, fingerprint, canonical.ShareId, alias.ShareId, shared);
    }

    private static string Fingerprint(ShareRequest canonical, ShareRequest alias, IEnumerable<DuplicateShareIdentity> shared, IEnumerable<Dictionary<ServiceType, string[]>> identities)
    {
        var value = string.Join('|', canonical.ShareId, canonical.Id, canonical.SongId, canonical.Status, canonical.CreatedAt.Ticks, canonical.SourceIdentityKey, canonical.CanonicalShareId,
            alias.ShareId, alias.Id, alias.SongId, alias.Status, alias.CreatedAt.Ticks, alias.SourceIdentityKey, alias.CanonicalShareId,
            string.Join(',', shared.Select(x => $"{x.ServiceType}:{x.ServiceSongId}")),
            string.Join(',', identities.SelectMany(x => x.OrderBy(y => y.Key).Select(y => $"{(int)y.Key}:{y.Value.Single()}"))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static bool IsShareId(string value) => ShareIdPattern().IsMatch(value);
    [GeneratedRegex("^[a-f0-9]{12}$", RegexOptions.CultureInvariant)] private static partial Regex ShareIdPattern();
}
