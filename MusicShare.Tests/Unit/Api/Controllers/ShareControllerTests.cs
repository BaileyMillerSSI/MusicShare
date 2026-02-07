using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Commands;
using MusicShare.Api.Controllers;
using MusicShare.Api.Queries;
using MusicShare.Contracts;
using MusicShare.Services.Models;

namespace MusicShare.Tests.Unit.Api.Controllers;

public class ShareControllerTests
{
    #region SubmitShare Tests

    [Fact]
    public async Task ItWillReturnOkWithShareIdForValidRequest()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://open.spotify.com/track/abc123");
        var response = SubmitShare.Response.AsSuccess("share-123abc", ShareStatus.Pending);
        mock.Mock<IMediator>()
            .Setup(x => x.Send(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.SubmitShare(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var submitResponse = okResult.Value.Should().BeOfType<SubmitShareResponse>().Subject;
        submitResponse.ShareId.Should().Be("share-123abc");
        submitResponse.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ItWillReturnBadRequestForUnsupportedUrl()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://unknown.com/track");
        var response = SubmitShare.Response.AsFailure("Unsupported music service URL");
        mock.Mock<IMediator>()
            .Setup(x => x.Send(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.SubmitShare(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ItWillReturnBadRequestWithErrorForFailedResponse()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://bad-url.com");
        var response = SubmitShare.Response.AsFailure("Something went wrong");
        mock.Mock<IMediator>()
            .Setup(x => x.Send(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.SubmitShare(request, CancellationToken.None);

        // Assert
        var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillSendRequestToMediator()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://open.spotify.com/track/abc123");
        var cts = new CancellationTokenSource();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(request, cts.Token))
            .ReturnsAsync(SubmitShare.Response.AsSuccess("id", ShareStatus.Pending));

        var sut = mock.Create<ShareController>();

        // Act
        await sut.SubmitShare(request, cts.Token);

        // Assert
        mock.Mock<IMediator>().Verify(x => x.Send(request, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ItWillReturnBadRequestForInvalidModelState()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("https://open.spotify.com/track/abc123");
        var sut = mock.Create<ShareController>();
        sut.ModelState.AddModelError("Url", "The Url field is required.");

        // Act
        var actionResult = await sut.SubmitShare(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task ItWillNotCallMediatorWhenModelStateIsInvalid()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var request = new SubmitShare.Request("invalid");
        var sut = mock.Create<ShareController>();
        sut.ModelState.AddModelError("Url", "Invalid URL format.");

        // Act
        await sut.SubmitShare(request, CancellationToken.None);

        // Assert
        mock.Mock<IMediator>().Verify(
            x => x.Send(It.IsAny<SubmitShare.Request>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetShareResult Tests

    [Fact]
    public async Task ItWillReturnOkWithResponseForExistingShare()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareId = "share-123";
        var shareResponse = new ShareResultResponse
        {
            ShareId = shareId,
            Status = "Completed",
            Song = null
        };
        var queryResult = GetShareResult.Result.Success(shareResponse);
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.Is<GetShareResult.Query>(q => q.ShareId == shareId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.GetShareResult(shareId, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ShareResultResponse>().Subject;
        response.ShareId.Should().Be(shareId);
        response.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task ItWillReturnNotFoundForNonExistentShare()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareId = "nonexistent";
        var queryResult = GetShareResult.Result.NotFound();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.Is<GetShareResult.Query>(q => q.ShareId == shareId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.GetShareResult(shareId, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ItWillReturnErrorMessageWhenNotFound()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetShareResult.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetShareResult.Result.NotFound());

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.GetShareResult("missing", CancellationToken.None);

        // Assert
        var notFoundResult = actionResult.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillSendCorrectQueryToMediator()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareId = "test-share-id";
        var cts = new CancellationTokenSource();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetShareResult.Query>(), cts.Token))
            .ReturnsAsync(GetShareResult.Result.NotFound());

        var sut = mock.Create<ShareController>();

        // Act
        await sut.GetShareResult(shareId, cts.Token);

        // Assert
        mock.Mock<IMediator>().Verify(
            x => x.Send(
                It.Is<GetShareResult.Query>(q => q.ShareId == shareId),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task ItWillReturnBadRequestForInvalidModelStateOnGetShareResult()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ShareController>();
        sut.ModelState.AddModelError("shareId", "Invalid share ID.");

        // Act
        var actionResult = await sut.GetShareResult("any-id", CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task ItWillReturnFullResponseWithSongDetails()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareId = "share-song";
        var shareResponse = new ShareResultResponse
        {
            ShareId = shareId,
            Status = "Completed",
            Song = new SongDetails
            {
                Id = "song-1",
                Title = "Great Song",
                Artists = ["Band Name"],
                Album = "Album Title",
                Status = "Resolved",
                Links = [
                    new ServiceLink
                    {
                        ServiceType = ServiceType.Spotify,
                        Url = "https://open.spotify.com/track/xyz"
                    },
                    new ServiceLink
                    {
                        ServiceType = ServiceType.AppleMusic,
                        Url = "https://music.apple.com/song/xyz"
                    }
                ]
            }
        };
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetShareResult.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetShareResult.Result.Success(shareResponse));

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.GetShareResult(shareId, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ShareResultResponse>().Subject;
        response.Song.Should().NotBeNull();
        response.Song!.Title.Should().Be("Great Song");
        response.Song.Links.Should().HaveCount(2);
    }

    #endregion

    #region GetAllShareIds Tests

    [Fact]
    public async Task ItWillReturnOkWithShareIdsForCompletedRequests()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var shareIds = new List<string> { "abc123def456", "789ghi012jkl" };
        var queryResult = GetAllShareIds.Result.Success(shareIds);
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetAllShareIds.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.GetAllShareIds(CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var result = okResult.Value.Should().BeOfType<GetAllShareIds.Result>().Subject;
        result.ShareIds.Should().HaveCount(2);
        result.ShareIds.Should().ContainInOrder("abc123def456", "789ghi012jkl");
    }

    [Fact]
    public async Task ItWillReturnOkWithEmptyListWhenNoCompletedRequests()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var queryResult = GetAllShareIds.Result.Success(new List<string>());
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetAllShareIds.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var sut = mock.Create<ShareController>();

        // Act
        var actionResult = await sut.GetAllShareIds(CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var result = okResult.Value.Should().BeOfType<GetAllShareIds.Result>().Subject;
        result.ShareIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillSendGetAllShareIdsQueryToMediator()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var cts = new CancellationTokenSource();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetAllShareIds.Query>(), cts.Token))
            .ReturnsAsync(GetAllShareIds.Result.Success(new List<string>()));

        var sut = mock.Create<ShareController>();

        // Act
        await sut.GetAllShareIds(cts.Token);

        // Assert
        mock.Mock<IMediator>().Verify(
            x => x.Send(It.IsAny<GetAllShareIds.Query>(), cts.Token),
            Times.Once);
    }

    #endregion
}
