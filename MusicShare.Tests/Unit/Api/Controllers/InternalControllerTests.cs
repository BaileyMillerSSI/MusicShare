using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MusicShare.Api.Commands;
using MusicShare.Api.Controllers;
using MusicShare.Api.Queries;

namespace MusicShare.Tests.Unit.Api.Controllers;

public class InternalControllerTests
{
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

        var sut = mock.Create<InternalController>();

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

        var sut = mock.Create<InternalController>();

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

        var sut = mock.Create<InternalController>();

        // Act
        await sut.GetAllShareIds(cts.Token);

        // Assert
        mock.Mock<IMediator>().Verify(
            x => x.Send(It.IsAny<GetAllShareIds.Query>(), cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task ItWillReturnServerErrorWhenReindexSecretIsMissing()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = CreateSut(mock, null, "secret");

        // Act
        var actionResult = await sut.ReindexAll(CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task ItWillRejectUnauthorizedReindexRequests()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = CreateSut(mock, "secret", "wrong");

        // Act
        var actionResult = await sut.ReindexAll(CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ItWillSendReindexAllCommandWhenAuthorized()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<ReindexAllShares.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReindexAllShares.Response.AsSuccess(2));
        var sut = CreateSut(mock, "secret", "secret");

        // Act
        var actionResult = await sut.ReindexAll(CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(ReindexAllShares.Response.AsSuccess(2));
    }

    [Fact]
    public async Task ItWillRejectMalformedReindexSongIds()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = CreateSut(mock, "secret", "secret");

        // Act
        var actionResult = await sut.ReindexSong("../bad", CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<BadRequestObjectResult>();
        mock.Mock<IMediator>().Verify(
            x => x.Send(It.IsAny<ReindexSong.Request>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillReturnNotFoundWhenReindexSongDoesNotFindShareRequest()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.IsAny<ReindexSong.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReindexSong.Response.NotFound());
        var sut = CreateSut(mock, "secret", "secret");

        // Act
        var actionResult = await sut.ReindexSong("507f1f77bcf86cd799439011", CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ItWillSendReindexSongCommandWhenAuthorized()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IMediator>()
            .Setup(x => x.Send(It.Is<ReindexSong.Request>(request => request.SongId == "507f1f77bcf86cd799439011"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReindexSong.Response.AsSuccess());
        var sut = CreateSut(mock, "secret", "secret");

        // Act
        var actionResult = await sut.ReindexSong("507f1f77bcf86cd799439011", CancellationToken.None);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(ReindexSong.Response.AsSuccess());
    }

    private static InternalController CreateSut(
        AutoMock mock,
        string? configuredSecret,
        string requestApiKey)
    {
        var configurationValues = new Dictionary<string, string?>();
        if (configuredSecret != null)
        {
            configurationValues["REINDEX_API_KEY"] = configuredSecret;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var sut = new InternalController(mock.Mock<IMediator>().Object, configuration);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-API-KEY"] = requestApiKey;
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return sut;
    }
}
