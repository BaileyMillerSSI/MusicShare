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
    ILogger<DuplicateShareReconciliationService> logger) : IDuplicateShareReconciliationService
{
    public async Task<DuplicateShareReconciliationResult> ReconcileAsync(DuplicateShareReconciliationRequest request, CancellationToken cancellationToken)
    {
        if (!IsShareId(request.FirstShareId) || !IsShareId(request.SecondShareId) || request.FirstShareId == request.SecondShareId)
            return DuplicateShareReconciliationResult.Failure("Exactly two distinct lowercase 12-character share IDs are required.");
        if (request.CanonicalShareId is not null && request.CanonicalShareId != request.FirstShareId && request.CanonicalShareId != request.SecondShareId)
            return DuplicateShareReconciliationResult.Failure("The canonical share must be one of the requested shares.");

        var pair = await requests.GetByShareIdsAsync([request.FirstShareId, request.SecondShareId], cancellationToken);
        if (pair.Count != 2 || pair.Any(x => x.Status != ShareStatus.Completed || string.IsNullOrWhiteSpace(x.SongId) || !string.IsNullOrWhiteSpace(x.CanonicalShareId)))
            return DuplicateShareReconciliationResult.Failure("Both shares must be distinct, completed canonical shares with songs.");
        var songLinks = await links.GetBySongIdsAsync(pair.Select(x => x.SongId!).ToArray(), cancellationToken);
        var identities = pair.Select(x => songLinks.Where(l => l.SongId == x.SongId && l.ServiceType != ServiceType.Unknown && !string.IsNullOrWhiteSpace(l.ServiceSongId))
            .Select(l => new DuplicateShareIdentity((int)l.ServiceType, l.ServiceSongId)).Distinct().ToHashSet()).ToArray();
        var shared = identities[0].Intersect(identities[1]).OrderBy(x => x.ServiceType).ThenBy(x => x.ServiceSongId, StringComparer.Ordinal).ToArray();
        if (shared.Length == 0) return DuplicateShareReconciliationResult.Failure("The shares do not have an exact provider identity in common.");

        var canonical = request.CanonicalShareId is null
            ? pair.OrderBy(x => x.CreatedAt).ThenBy(x => x.ShareId, StringComparer.Ordinal).First()
            : pair.Single(x => x.ShareId == request.CanonicalShareId);
        var alias = pair.Single(x => x.ShareId != canonical.ShareId);
        var fingerprint = Fingerprint(canonical, alias, shared);
        var operationId = $"reconcile-{fingerprint[..16]}";
        if (request.Mode == DuplicateShareReconciliationMode.DryRun)
            return new(true, false, null, operationId, fingerprint, canonical.ShareId, alias.ShareId, shared);
        if (request.Fingerprint != fingerprint) return DuplicateShareReconciliationResult.Failure("The apply fingerprint does not match the current reconciliation plan.");

        var sourceIdentity = canonical.SourceIdentityKey ?? ShareRequestService.BuildSourceIdentityKey(canonical.SourceService, canonical.ServiceTrackId);
        var write = await requests.TryReconcileAsync(new(canonical.ShareId, alias.ShareId, operationId, fingerprint, sourceIdentity, canonical.CreatedAt, alias.CreatedAt), cancellationToken);
        logger.LogInformation("Duplicate share reconciliation {OperationId} apply for {CanonicalShareId} and {AliasShareId}: {Succeeded}, changed={Changed}", operationId, canonical.ShareId, alias.ShareId, write.Succeeded, write.Changed);
        return new(write.Succeeded, write.Changed, write.Error, operationId, fingerprint, canonical.ShareId, alias.ShareId, shared);
    }

    private static string Fingerprint(ShareRequest canonical, ShareRequest alias, IEnumerable<DuplicateShareIdentity> shared)
    {
        var value = string.Join('|', canonical.ShareId, canonical.CreatedAt.Ticks, alias.ShareId, alias.CreatedAt.Ticks,
            string.Join(',', shared.Select(x => $"{x.ServiceType}:{x.ServiceSongId}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static bool IsShareId(string value) => ShareIdPattern().IsMatch(value);
    [GeneratedRegex("^[a-f0-9]{12}$", RegexOptions.CultureInvariant)] private static partial Regex ShareIdPattern();
}
