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

    private static ShareRequest Completed(string shareId, string songId, DateTime createdAt) => new() { ShareId = shareId, SongId = songId, Status = ShareStatus.Completed, CreatedAt = createdAt, SourceService = ServiceType.Spotify };
    private static SongServiceLink Link(string songId, ServiceType service, string identity) => new() { SongId = songId, ServiceType = service, ServiceSongId = identity, OriginalUrl = "https://example.test", NormalizedUrl = "https://example.test" };
}
