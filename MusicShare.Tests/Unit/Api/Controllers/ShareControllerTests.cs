using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Commands;
using MusicShare.Api.Controllers;
using MusicShare.Api.Models;
using MusicShare.Api.Queries;
using MusicShare.Contracts;

namespace MusicShare.Tests.Unit.Api.Controllers;

public class ShareControllerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly ShareController _sut;

    public ShareControllerTests()
    {
        _sut = _mocker.CreateInstance<ShareController>();
    }

    #region SubmitShare Tests

    [Fact]
    public async Task ItWillReturnOkWithShareIdForValidRequest()
    {
        // Arrange
        var request = new SubmitShare.Request("https://open.spotify.com/track/abc123");
        var response = SubmitShare.Response.AsSuccess("share-123abc", ShareStatus.Pending);
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var actionResult = await _sut.SubmitShare(request, CancellationToken.None);

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
        var request = new SubmitShare.Request("https://unknown.com/track");
        var response = SubmitShare.Response.AsFailure("Unsupported music service URL");
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var actionResult = await _sut.SubmitShare(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ItWillReturnBadRequestWithErrorForFailedResponse()
    {
        // Arrange
        var request = new SubmitShare.Request("https://bad-url.com");
        var response = SubmitShare.Response.AsFailure("Something went wrong");
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var actionResult = await _sut.SubmitShare(request, CancellationToken.None);

        // Assert
        var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillSendRequestToMediator()
    {
        // Arrange
        var request = new SubmitShare.Request("https://open.spotify.com/track/abc123");
        var cts = new CancellationTokenSource();
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(request, cts.Token))
            .ReturnsAsync(SubmitShare.Response.AsSuccess("id", ShareStatus.Pending));

        // Act
        await _sut.SubmitShare(request, cts.Token);

        // Assert
        _mocker.GetMock<IMediator>().Verify(x => x.Send(request, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ItWillReturnBadRequestForInvalidModelState()
    {
        // Arrange
        var request = new SubmitShare.Request("https://open.spotify.com/track/abc123");
        _sut.ModelState.AddModelError("Url", "The Url field is required.");

        // Act
        var actionResult = await _sut.SubmitShare(request, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task ItWillNotCallMediatorWhenModelStateIsInvalid()
    {
        // Arrange
        var request = new SubmitShare.Request("invalid");
        _sut.ModelState.AddModelError("Url", "Invalid URL format.");

        // Act
        await _sut.SubmitShare(request, CancellationToken.None);

        // Assert
        _mocker.GetMock<IMediator>().Verify(
            x => x.Send(It.IsAny<SubmitShare.Request>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetShareResult Tests

    [Fact]
    public async Task ItWillReturnOkWithResponseForExistingShare()
    {
        // Arrange
        var shareId = "share-123";
        var shareResponse = new ShareResultResponse
        {
            ShareId = shareId,
            Status = "Completed",
            Song = null
        };
        var queryResult = GetShareResult.Result.Success(shareResponse);
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(It.Is<GetShareResult.Query>(q => q.ShareId == shareId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var actionResult = await _sut.GetShareResult(shareId, CancellationToken.None);

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
        var shareId = "nonexistent";
        var queryResult = GetShareResult.Result.NotFound();
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(It.Is<GetShareResult.Query>(q => q.ShareId == shareId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var actionResult = await _sut.GetShareResult(shareId, CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ItWillReturnErrorMessageWhenNotFound()
    {
        // Arrange
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetShareResult.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetShareResult.Result.NotFound());

        // Act
        var actionResult = await _sut.GetShareResult("missing", CancellationToken.None);

        // Assert
        var notFoundResult = actionResult.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ItWillSendCorrectQueryToMediator()
    {
        // Arrange
        var shareId = "test-share-id";
        var cts = new CancellationTokenSource();
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetShareResult.Query>(), cts.Token))
            .ReturnsAsync(GetShareResult.Result.NotFound());

        // Act
        await _sut.GetShareResult(shareId, cts.Token);

        // Assert
        _mocker.GetMock<IMediator>().Verify(
            x => x.Send(
                It.Is<GetShareResult.Query>(q => q.ShareId == shareId),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task ItWillReturnBadRequestForInvalidModelStateOnGetShareResult()
    {
        // Arrange
        _sut.ModelState.AddModelError("shareId", "Invalid share ID.");

        // Act
        var actionResult = await _sut.GetShareResult("any-id", CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task ItWillReturnFullResponseWithSongDetails()
    {
        // Arrange
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
        _mocker.GetMock<IMediator>()
            .Setup(x => x.Send(It.IsAny<GetShareResult.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetShareResult.Result.Success(shareResponse));

        // Act
        var actionResult = await _sut.GetShareResult(shareId, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ShareResultResponse>().Subject;
        response.Song.Should().NotBeNull();
        response.Song!.Title.Should().Be("Great Song");
        response.Song.Links.Should().HaveCount(2);
    }

    #endregion
}
