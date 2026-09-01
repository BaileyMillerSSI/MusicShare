using System.Security.Cryptography;
using System.Text;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

// The dry-run and the fenced apply path deliberately share this representation.  In
// particular, the claim version in a dry-run is the *pre-claim* version; an apply
// validates that it acquired exactly the next version before rebuilding this snapshot.
public sealed record ReconciliationSnapshot(
    string Fingerprint,
    long CanonicalPreClaimVersion,
    long AliasPreClaimVersion,
    IReadOnlyList<ReconciliationIdentity> SharedIdentities);

public record ReconciliationIdentity(int ServiceType, string ServiceSongId);

public static class ReconciliationSnapshots
{
    public static ReconciliationSnapshot? TryCreate(
        ShareRequest canonical, ShareRequest alias, IEnumerable<Song> songs,
        IEnumerable<SongServiceLink> links, IEnumerable<ShareRequest> owners,
        long canonicalPreClaimVersion, long aliasPreClaimVersion)
    {
        if (canonical.Status != ShareStatus.Completed || alias.Status != ShareStatus.Completed ||
            string.IsNullOrWhiteSpace(canonical.SongId) || string.IsNullOrWhiteSpace(alias.SongId) ||
            !Defined(canonical.SourceService) || !Defined(alias.SourceService) ||
            !string.IsNullOrWhiteSpace(canonical.CanonicalShareId) || canonical.SongId == alias.SongId)
            return null;
        var songArray = songs.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        if (songArray.Length != 2 || songArray.Any(x => x.Status != SongStatus.Resolved) ||
            !songArray.Select(x => x.Id).ToHashSet(StringComparer.Ordinal).SetEquals([canonical.SongId, alias.SongId])) return null;
        var evidence = links.OrderBy(x => x.SongId, StringComparer.Ordinal).ThenBy(x => (int)x.ServiceType).ThenBy(x => x.ServiceSongId, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        if (evidence.Any(x => !Defined(x.ServiceType) || !ProviderId(x.ServiceSongId))) return null;
        var perSong = new[] { canonical.SongId, alias.SongId }.Select(songId => evidence.Where(x => x.SongId == songId)
            .GroupBy(x => x.ServiceType).ToDictionary(x => x.Key, x => x.Select(y => y.ServiceSongId).Distinct(StringComparer.Ordinal).ToArray())).ToArray();
        if (perSong.Any(x => x.Any(y => y.Value.Length != 1)) || perSong[0].Keys.Intersect(perSong[1].Keys).Any(x => !perSong[0][x].SequenceEqual(perSong[1][x], StringComparer.Ordinal))) return null;
        var sets = perSong.Select(x => x.SelectMany(y => y.Value.Select(id => new ReconciliationIdentity((int)y.Key, id))).ToHashSet()).ToArray();
        var shared = sets[0].Intersect(sets[1]).OrderBy(x => x.ServiceType).ThenBy(x => x.ServiceSongId, StringComparer.Ordinal).ToArray();
        if (shared.Length == 0 || owners.Any(x => x.ShareId != canonical.ShareId && x.ShareId != alias.ShareId && string.IsNullOrWhiteSpace(x.CanonicalShareId))) return null;
        // Roles are deliberately explicit: choosing A->B is not the same plan as B->A.
        var requestLines = new[] {
            Pack("canonical-request", canonical.ShareId, canonical.Id, canonical.SongId, ((int)canonical.Status).ToString(), canonical.CreatedAt.Ticks.ToString(), canonical.SourceIdentityKey, canonical.CanonicalShareId, canonicalPreClaimVersion.ToString()),
            Pack("alias-request", alias.ShareId, alias.Id, alias.SongId, ((int)alias.Status).ToString(), alias.CreatedAt.Ticks.ToString(), alias.SourceIdentityKey, alias.CanonicalShareId, aliasPreClaimVersion.ToString()) };
        var songLines = songArray.Select(x => Pack("song", x.Id, ((int)x.Status).ToString(), x.CreatedAt.Ticks.ToString(), x.UpdatedAt.Ticks.ToString()));
        var linkLines = evidence.Select(x => Pack("evidence", x.Id, x.SongId, ((int)x.ServiceType).ToString(), x.ServiceSongId, x.CreatedAt.Ticks.ToString(), x.OriginalUrl, x.NormalizedUrl));
        var ownerLines = owners.OrderBy(x => x.ShareId, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).Select(x => Pack("owner", x.ShareId, x.Id, x.SongId, ((int)x.Status).ToString(), x.CanonicalShareId, x.CreatedAt.Ticks.ToString()));
        var sharedLines = shared.Select(x => Pack("shared", x.ServiceType.ToString(), x.ServiceSongId));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Pack(requestLines.Concat(songLines).Concat(linkLines).Concat(ownerLines).Concat(sharedLines).ToArray())))).ToLowerInvariant();
        return new(fingerprint, canonicalPreClaimVersion, aliasPreClaimVersion, shared);
    }

    public static bool Defined(ServiceType value) => Enum.IsDefined(value) && value != ServiceType.Unknown;
    public static bool ProviderId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && value.All(x => !char.IsControl(x));

    // Length-prefixing keeps values such as "a|b" and separate fields unambiguous.
    private static string Pack(params string?[] values) => string.Concat(values.Select(value =>
    {
        return value is null ? "-1:" : $"{value.Length}:{value}";
    }));
}
