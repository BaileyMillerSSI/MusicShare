using Microsoft.Extensions.Logging.Abstractions;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class DuplicateShareReconciliationServiceTests
{
    [Fact]
    public async Task ItWillRequireAnExactSharedProviderIdentityAndKeepDryRunSideEffectFree()
    {
        using var mock = AutoMock.GetLoose();
        var first = Completed("aaaaaaaaaaaa", "song-a", DateTime.UnixEpoch);
        var second = Completed("bbbbbbbbbbbb", "song-b", DateTime.UnixEpoch.AddSeconds(1));
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetByShareIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetBySongIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            Link("song-a", ServiceType.Spotify, "track"), Link("song-b", ServiceType.Spotify, "track")]);
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([new Song { Id = "song-a", Status = SongStatus.Resolved }, new Song { Id = "song-b", Status = SongStatus.Resolved }]);
        var sut = new DuplicateShareReconciliationService(mock.Mock<IShareRequestRepository>().Object, mock.Mock<ISongServiceLinkRepository>().Object, mock.Mock<ISongRepository>().Object, NullLogger<DuplicateShareReconciliationService>.Instance);

        var result = await sut.ReconcileAsync(new(first.ShareId, second.ShareId, null, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Changed.Should().BeFalse();
        result.CanonicalShareId.Should().Be(first.ShareId);
        result.SharedIdentities.Should().ContainSingle().Which.ServiceSongId.Should().Be("track");
        mock.Mock<IShareRequestRepository>().Verify(x => x.TryReconcileAsync(It.IsAny<ReconciliationWrite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillRejectMetadataOnlyMatches()
    {
        using var mock = AutoMock.GetLoose();
        var first = Completed("aaaaaaaaaaaa", "song-a", DateTime.UnixEpoch);
        var second = Completed("bbbbbbbbbbbb", "song-b", DateTime.UnixEpoch.AddSeconds(1));
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetByShareIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetBySongIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            Link("song-a", ServiceType.Spotify, "one"), Link("song-b", ServiceType.Spotify, "two")]);
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([new Song { Id = "song-a", Status = SongStatus.Resolved }, new Song { Id = "song-b", Status = SongStatus.Resolved }]);
        var sut = new DuplicateShareReconciliationService(mock.Mock<IShareRequestRepository>().Object, mock.Mock<ISongServiceLinkRepository>().Object, mock.Mock<ISongRepository>().Object, NullLogger<DuplicateShareReconciliationService>.Instance);

        var result = await sut.ReconcileAsync(new(first.ShareId, second.ShareId, null, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ItWillRejectAThirdCanonicalOwnerOfSharedProviderEvidence()
    {
        using var mock = AutoMock.GetLoose();
        var first = Completed("aaaaaaaaaaaa", "song-a", DateTime.UnixEpoch);
        var second = Completed("bbbbbbbbbbbb", "song-b", DateTime.UnixEpoch.AddSeconds(1));
        var third = Completed("cccccccccccc", "song-c", DateTime.UnixEpoch.AddSeconds(2));
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetByShareIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetBySongIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second, third]);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetBySongIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([Link("song-a", ServiceType.Spotify, "track"), Link("song-b", ServiceType.Spotify, "track")]);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetByIdentitiesAsync(It.IsAny<IReadOnlyCollection<SongServiceIdentity>>(), It.IsAny<CancellationToken>())).ReturnsAsync([Link("song-a", ServiceType.Spotify, "track"), Link("song-b", ServiceType.Spotify, "track"), Link("song-c", ServiceType.Spotify, "track")]);
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([new Song { Id = "song-a", Status = SongStatus.Resolved }, new Song { Id = "song-b", Status = SongStatus.Resolved }]);
        var sut = new DuplicateShareReconciliationService(mock.Mock<IShareRequestRepository>().Object, mock.Mock<ISongServiceLinkRepository>().Object, mock.Mock<ISongRepository>().Object, NullLogger<DuplicateShareReconciliationService>.Instance);

        var result = await sut.ReconcileAsync(new(first.ShareId, second.ShareId, null, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("third canonical");
    }

    [Fact]
    public async Task ItWillRejectAnApplyWhenTheDryRunFingerprintNoLongerBindsCurrentEvidence()
    {
        using var mock = AutoMock.GetLoose();
        var first = Completed("aaaaaaaaaaaa", "song-a", DateTime.UnixEpoch);
        var second = Completed("bbbbbbbbbbbb", "song-b", DateTime.UnixEpoch.AddSeconds(1));
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetByShareIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([new Song { Id = "song-a", Status = SongStatus.Resolved }, new Song { Id = "song-b", Status = SongStatus.Resolved }]);
        var evidence = new List<SongServiceLink> { Link("song-a", ServiceType.Spotify, "track"), Link("song-b", ServiceType.Spotify, "track") };
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetBySongIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(evidence);
        var sut = new DuplicateShareReconciliationService(mock.Mock<IShareRequestRepository>().Object, mock.Mock<ISongServiceLinkRepository>().Object, mock.Mock<ISongRepository>().Object, NullLogger<DuplicateShareReconciliationService>.Instance);

        var dryRun = await sut.ReconcileAsync(new(first.ShareId, second.ShareId, null, DuplicateShareReconciliationMode.DryRun, null), CancellationToken.None);
        evidence[1].ServiceSongId = "changed-track";
        var apply = await sut.ReconcileAsync(new(first.ShareId, second.ShareId, null, DuplicateShareReconciliationMode.Apply, dryRun.Fingerprint), CancellationToken.None);

        apply.Success.Should().BeFalse();
        mock.Mock<IShareRequestRepository>().Verify(x => x.TryReconcileAsync(It.IsAny<ReconciliationWrite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillReturnStoredOperationAndEvidenceForASuccessfulIdempotentApplyRetry()
    {
        using var mock = AutoMock.GetLoose();
        var canonical = Completed("aaaaaaaaaaaa", "song-a", DateTime.UnixEpoch);
        canonical.SourceIdentityKey = "v1:1:track";
        var alias = Completed("bbbbbbbbbbbb", "song-b", DateTime.UnixEpoch.AddSeconds(1));
        alias.CanonicalShareId = canonical.ShareId;
        alias.ReconciliationId = $"reconcile-{new string('a', 64)}";
        alias.ReconciliationFingerprint = new string('a', 64);
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetByShareIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([canonical, alias]);
        mock.Mock<IShareRequestRepository>().Setup(x => x.GetBySongIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([canonical, alias]);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetBySongIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([Link("song-a", ServiceType.Spotify, "track"), Link("song-b", ServiceType.Spotify, "track")]);
        mock.Mock<ISongServiceLinkRepository>().Setup(x => x.GetByIdentitiesAsync(It.IsAny<IReadOnlyCollection<SongServiceIdentity>>(), It.IsAny<CancellationToken>())).ReturnsAsync([Link("song-a", ServiceType.Spotify, "track"), Link("song-b", ServiceType.Spotify, "track")]);
        mock.Mock<ISongRepository>().Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([new Song { Id = "song-a", Status = SongStatus.Resolved }, new Song { Id = "song-b", Status = SongStatus.Resolved }]);
        var sut = new DuplicateShareReconciliationService(mock.Mock<IShareRequestRepository>().Object, mock.Mock<ISongServiceLinkRepository>().Object, mock.Mock<ISongRepository>().Object, NullLogger<DuplicateShareReconciliationService>.Instance);

        var result = await sut.ReconcileAsync(new(canonical.ShareId, alias.ShareId, null, DuplicateShareReconciliationMode.Apply, new string('a', 64)), CancellationToken.None);

        result.Should().Match<DuplicateShareReconciliationResult>(x => x.Success && !x.Changed && x.OperationId == alias.ReconciliationId && x.Fingerprint == alias.ReconciliationFingerprint && x.SharedIdentities.Count == 1);
        mock.Mock<IShareRequestRepository>().Verify(x => x.TryReconcileAsync(It.IsAny<ReconciliationWrite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ItWillFingerprintEquivalentEvidenceDeterministicallyWithoutDelimiterAmbiguity()
    {
        var canonical = Completed("aaaaaaaaaaaa", "song-a", DateTime.UnixEpoch);
        var alias = Completed("bbbbbbbbbbbb", "song-b", DateTime.UnixEpoch.AddSeconds(1));
        var songs = new[] { new Song { Id = "song-a", Status = SongStatus.Resolved }, new Song { Id = "song-b", Status = SongStatus.Resolved } };
        var first = Link("song-a", ServiceType.Spotify, "track|one:two"); first.Id = "link-a";
        var second = Link("song-b", ServiceType.Spotify, "track|one:two"); second.Id = "link-b";
        var forward = ReconciliationSnapshots.TryCreate(canonical, alias, songs, [first, second], [canonical, alias], 0, 0);
        var reverse = ReconciliationSnapshots.TryCreate(canonical, alias, songs.Reverse(), [second, first], [alias, canonical], 0, 0);

        forward.Should().NotBeNull();
        reverse.Should().NotBeNull();
        forward!.Fingerprint.Should().Be(reverse!.Fingerprint);
        ReconciliationSnapshots.TryCreate(alias, canonical, songs, [first, second], [canonical, alias], 0, 0)!.Fingerprint.Should().NotBe(forward.Fingerprint);
    }

    private static ShareRequest Completed(string shareId, string songId, DateTime createdAt) => new() { ShareId = shareId, SongId = songId, Status = ShareStatus.Completed, CreatedAt = createdAt, SourceService = ServiceType.Spotify };
    private static SongServiceLink Link(string songId, ServiceType service, string identity) => new() { SongId = songId, ServiceType = service, ServiceSongId = identity, OriginalUrl = "https://example.test", NormalizedUrl = "https://example.test" };
}
