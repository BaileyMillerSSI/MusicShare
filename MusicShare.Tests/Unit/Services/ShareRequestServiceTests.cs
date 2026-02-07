using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;

namespace MusicShare.Tests.Unit.Services;

public class ShareRequestServiceTests
{
    #region Create Tests

    [Fact]
    public async Task ItWillReturnGeneratedShareIdForNewSong()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, "abc123");
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(12);
    }

    [Fact]
    public async Task ItWillInsertShareRequestToRepositoryForNewSong()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, "abc123");
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.InsertAsync(
                It.Is<ShareRequest>(sr =>
                    sr.SourceUrl == "https://open.spotify.com/track/abc123" &&
                    sr.SourceService == ServiceType.Spotify &&
                    sr.ServiceTrackId == "abc123" &&
                    sr.Status == ShareStatus.Pending &&
                    sr.ShareId.Length == 12),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillPublishSongShareSubmittedEventForNewSong()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, "abc123");
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        mock.Mock<IPublishEndpoint>().Verify(
            x => x.Publish(
                It.Is<SongShareSubmitted>(msg =>
                    msg.SourceUrl == "https://open.spotify.com/track/abc123" &&
                    msg.SourceService == ServiceType.Spotify &&
                    msg.ShareId.Length == 12 &&
                    msg.CorrelationId != Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillReturnExistingShareIdForDuplicateSong()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;
        var existingShareId = "existing12id";

        SetupAdapterForCreate(mock, url, serviceType, "abc123");

        var existingLink = new SongServiceLink
        {
            Id = "link-1",
            SongId = "song-1",
            ServiceType = ServiceType.Spotify,
            ServiceSongId = "abc123",
            OriginalUrl = url,
            NormalizedUrl = "https://open.spotify.com/track/abc123"
        };
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        var existingRequest = new ShareRequest
        {
            ShareId = existingShareId,
            SourceUrl = url,
            SourceService = serviceType,
            Status = ShareStatus.Completed
        };
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetBySongIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRequest);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        result.Should().Be(existingShareId);
    }

    [Fact]
    public async Task ItWillNotInsertNewShareRequestForDuplicateSong()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, "abc123");

        var existingLink = new SongServiceLink
        {
            Id = "link-1",
            SongId = "song-1",
            ServiceType = ServiceType.Spotify,
            ServiceSongId = "abc123",
            OriginalUrl = url,
            NormalizedUrl = url
        };
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetBySongIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareRequest { ShareId = "existing12id" });

        var sut = mock.Create<ShareRequestService>();

        // Act
        await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mock.Mock<IPublishEndpoint>().Verify(
            x => x.Publish(It.IsAny<SongShareSubmitted>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillCreateNewShareRequestWhenLinkExistsButNoShareRequest()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, "abc123");

        var existingLink = new SongServiceLink
        {
            Id = "link-1",
            SongId = "song-1",
            ServiceType = ServiceType.Spotify,
            ServiceSongId = "abc123",
            OriginalUrl = url,
            NormalizedUrl = url
        };
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        // No existing share request for this song
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetBySongIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        result.Should().HaveLength(12);
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillSkipDuplicateCheckForNullServiceTrackId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, null);

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        result.Should().HaveLength(12);
        mock.Mock<ISongServiceLinkRepository>().Verify(
            x => x.GetByServiceAndSongIdAsync(It.IsAny<ServiceType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillSkipDuplicateCheckForEmptyServiceTrackId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, "");

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        mock.Mock<ISongServiceLinkRepository>().Verify(
            x => x.GetByServiceAndSongIdAsync(It.IsAny<ServiceType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillUseNormalizedUrlInShareRequest()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123?si=extra_params";
        var normalizedUrl = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(serviceType))
            .Returns(adapterMock.Object);
        adapterMock.Setup(x => x.ExtractSongId(url)).Returns("abc123");
        adapterMock.Setup(x => x.NormalizeUrl(url)).Returns(normalizedUrl);

        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.InsertAsync(
                It.Is<ShareRequest>(sr => sr.SourceUrl == normalizedUrl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillEnsureShareRequestHasValidCorrelationId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var url = "https://open.spotify.com/track/abc123";
        var serviceType = ServiceType.Spotify;

        SetupAdapterForCreate(mock, url, serviceType, "abc123");
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetByServiceAndSongIdAsync(serviceType, "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongServiceLink?)null);
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.InsertAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest sr, CancellationToken _) => sr);

        var sut = mock.Create<ShareRequestService>();

        // Act
        await sut.Create(url, serviceType, CancellationToken.None);

        // Assert
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.InsertAsync(
                It.Is<ShareRequest>(sr => sr.CorrelationId != Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetByShareIdAsync Tests

    [Fact]
    public async Task ItWillReturnNullForNonExistentShareId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnResponseWithoutSongForExistingShareWithNoSong()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareRequest = new ShareRequest
        {
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Pending,
            SongId = null
        };
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("share-123", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ShareId.Should().Be("share-123");
        result.Status.Should().Be("Pending");
        result.Song.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnResponseWithoutSongForExistingShareWithEmptySongId()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareRequest = new ShareRequest
        {
            ShareId = "share-123",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Processing,
            SongId = ""
        };
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("share-123", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Song.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnFullResponseForShareWithSongAndLinks()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareRequest = new ShareRequest
        {
            ShareId = "share-full",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Completed,
            SongId = "song-obj-id"
        };
        var song = new Song
        {
            Id = "song-obj-id",
            Title = "Test Song",
            Artists = ["Artist 1", "Artist 2"],
            Album = "Test Album",
            ArtworkUrl = "https://img.example.com/art.jpg",
            Duration = TimeSpan.FromSeconds(210),
            IsExplicit = true,
            Status = SongStatus.Resolved
        };
        var links = new List<SongServiceLink>
        {
            new()
            {
                Id = "link-1",
                SongId = "song-obj-id",
                ServiceType = ServiceType.Spotify,
                ServiceSongId = "spotify-123",
                OriginalUrl = "https://open.spotify.com/track/abc",
                NormalizedUrl = "https://open.spotify.com/track/abc"
            },
            new()
            {
                Id = "link-2",
                SongId = "song-obj-id",
                ServiceType = ServiceType.AppleMusic,
                ServiceSongId = "apple-456",
                OriginalUrl = "https://music.apple.com/song/456",
                NormalizedUrl = "https://music.apple.com/song/456"
            }
        };

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-obj-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAsync("song-obj-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("share-full", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ShareId.Should().Be("share-full");
        result.Status.Should().Be("Completed");
        result.Song.Should().NotBeNull();
        result.Song!.Id.Should().Be("song-obj-id");
        result.Song.Title.Should().Be("Test Song");
        result.Song.Artists.Should().HaveCount(2);
        result.Song.Artists.Should().Contain("Artist 1");
        result.Song.Artists.Should().Contain("Artist 2");
        result.Song.Album.Should().Be("Test Album");
        result.Song.ArtworkUrl.Should().Be("https://img.example.com/art.jpg");
        result.Song.Duration.Should().Be(TimeSpan.FromSeconds(210));
        result.Song.IsExplicit.Should().BeTrue();
        result.Song.Status.Should().Be("Resolved");
        result.Song.Links.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItWillMapLinksCorrectlyForShareWithSong()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareRequest = new ShareRequest
        {
            ShareId = "share-links",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Completed,
            SongId = "song-1"
        };
        var song = new Song
        {
            Id = "song-1",
            Title = "Link Test",
            Artists = ["Artist"],
            Status = SongStatus.Resolved
        };
        var links = new List<SongServiceLink>
        {
            new()
            {
                Id = "link-1",
                SongId = "song-1",
                ServiceType = ServiceType.YouTubeMusic,
                ServiceSongId = "yt-123",
                OriginalUrl = "https://music.youtube.com/watch?v=yt-123",
                NormalizedUrl = "https://music.youtube.com/watch?v=yt-123"
            }
        };

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-links", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAsync("song-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("share-links", CancellationToken.None);

        // Assert
        var link = result!.Song!.Links.Should().ContainSingle().Subject;
        link.ServiceType.Should().Be(ServiceType.YouTubeMusic);
        link.Url.Should().Be("https://music.youtube.com/watch?v=yt-123");
    }

    [Fact]
    public async Task ItWillReturnResponseWithoutSongWhenSongIdPresentButSongNotFound()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareRequest = new ShareRequest
        {
            ShareId = "share-orphan",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Processing,
            SongId = "missing-song-id"
        };

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-orphan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("missing-song-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Song?)null);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("share-orphan", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ShareId.Should().Be("share-orphan");
        result.Status.Should().Be("Processing");
        result.Song.Should().BeNull();
    }

    [Fact]
    public async Task ItWillReturnEmptyLinksListForShareWithSongButNoLinks()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareRequest = new ShareRequest
        {
            ShareId = "share-nolinks",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Completed,
            SongId = "song-nolinks"
        };
        var song = new Song
        {
            Id = "song-nolinks",
            Title = "No Links Song",
            Artists = ["Solo"],
            Status = SongStatus.Pending
        };

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-nolinks", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-nolinks", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAsync("song-nolinks", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("share-nolinks", CancellationToken.None);

        // Assert
        result!.Song.Should().NotBeNull();
        result.Song!.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillMapAllShareStatuses()
    {
        // Arrange & Act & Assert for each status
        foreach (var status in Enum.GetValues<ShareStatus>())
        {
            using var mock = AutoMock.GetLoose();
            var shareRequest = new ShareRequest
            {
                ShareId = $"share-{status}",
                SourceUrl = "https://open.spotify.com/track/abc",
                SourceService = ServiceType.Spotify,
                Status = status,
                SongId = null
            };
            mock.Mock<IShareRequestRepository>()
                .Setup(x => x.GetByShareIdAsync($"share-{status}", It.IsAny<CancellationToken>()))
                .ReturnsAsync(shareRequest);

            var sut = mock.Create<ShareRequestService>();
            var result = await sut.GetByShareIdAsync($"share-{status}", CancellationToken.None);

            result!.Status.Should().Be(status.ToString());
        }
    }

    [Fact]
    public async Task ItWillMapCorrectlyForSongWithNullOptionalFields()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareRequest = new ShareRequest
        {
            ShareId = "share-minimal",
            SourceUrl = "https://open.spotify.com/track/abc",
            SourceService = ServiceType.Spotify,
            Status = ShareStatus.Completed,
            SongId = "song-minimal"
        };
        var song = new Song
        {
            Id = "song-minimal",
            Title = "Minimal Song",
            Artists = ["Artist"],
            Album = null,
            ArtworkUrl = null,
            Duration = null,
            IsExplicit = null,
            Status = SongStatus.Resolved
        };

        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetByShareIdAsync("share-minimal", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        mock.Mock<ISongRepository>()
            .Setup(x => x.GetByIdAsync("song-minimal", It.IsAny<CancellationToken>()))
            .ReturnsAsync(song);
        mock.Mock<ISongServiceLinkRepository>()
            .Setup(x => x.GetBySongIdAsync("song-minimal", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetByShareIdAsync("share-minimal", CancellationToken.None);

        // Assert
        result!.Song!.Album.Should().BeNull();
        result.Song.ArtworkUrl.Should().BeNull();
        result.Song.Duration.Should().BeNull();
        result.Song.IsExplicit.Should().BeNull();
    }

    #endregion

    #region GetAllCompletedShareIdsAsync Tests

    [Fact]
    public async Task ItWillReturnCompletedShareIdsFromRepository()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var expectedIds = new List<string> { "abc123def456", "789ghi012jkl" };
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIds);

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetAllCompletedShareIdsAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainInOrder("abc123def456", "789ghi012jkl");
    }

    [Fact]
    public async Task ItWillReturnEmptyListWhenNoCompletedShareRequests()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var sut = mock.Create<ShareRequestService>();

        // Act
        var result = await sut.GetAllCompletedShareIdsAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToRepositoryForGetAllCompletedShareIds()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var cts = new CancellationTokenSource();
        mock.Mock<IShareRequestRepository>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(cts.Token))
            .ReturnsAsync(new List<string>());

        var sut = mock.Create<ShareRequestService>();

        // Act
        await sut.GetAllCompletedShareIdsAsync(cts.Token);

        // Assert
        mock.Mock<IShareRequestRepository>().Verify(
            x => x.GetAllCompletedShareIdsAsync(cts.Token),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static void SetupAdapterForCreate(AutoMock mock, string url, ServiceType serviceType, string? songId)
    {
        var adapterMock = new Mock<IMusicServiceAdapter>();
        mock.Mock<IMusicServiceResolver>()
            .Setup(x => x.GetAdapter(serviceType))
            .Returns(adapterMock.Object);
        adapterMock.Setup(x => x.ExtractSongId(url)).Returns(songId);
        adapterMock.Setup(x => x.NormalizeUrl(url)).Returns(url);
    }

    #endregion
}
