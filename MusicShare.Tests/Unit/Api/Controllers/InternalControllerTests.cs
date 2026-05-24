using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicShare.Api.Configuration;
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

        var sut = CreateAuthorizedController(mock);

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

        var sut = CreateAuthorizedController(mock);

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

        var sut = CreateAuthorizedController(mock);

        // Act
        await sut.GetAllShareIds(cts.Token);

        // Assert
        mock.Mock<IMediator>().Verify(
            x => x.Send(It.IsAny<GetAllShareIds.Query>(), cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task ItWillReturnUnauthorizedWhenInternalApiKeyIsMissing()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = CreateController(mock);

        // Act
        var actionResult = await sut.GetAllShareIds(CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<UnauthorizedResult>();
        mock.Mock<IMediator>().Verify(
            x => x.Send(It.IsAny<GetAllShareIds.Query>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ItWillReturnUnauthorizedWhenInternalApiKeyDoesNotMatch()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = CreateController(mock, "wrong-key");

        // Act
        var actionResult = await sut.GetAllShareIds(CancellationToken.None);

        // Assert
        actionResult.Result.Should().BeOfType<UnauthorizedResult>();
        mock.Mock<IMediator>().Verify(
            x => x.Send(It.IsAny<GetAllShareIds.Query>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static InternalController CreateAuthorizedController(AutoMock mock)
    {
        return CreateController(mock, "test-internal-key");
    }

    private static InternalController CreateController(AutoMock mock, string? providedApiKey = null)
    {
        var settings = new InternalApiSettings { ApiKey = "test-internal-key" };
        mock.Mock<IOptions<InternalApiSettings>>()
            .SetupGet(x => x.Value)
            .Returns(settings);

        var sut = mock.Create<InternalController>();
        var context = new DefaultHttpContext();

        if (providedApiKey is not null)
        {
            context.Request.Headers[settings.HeaderName] = providedApiKey;
        }

        sut.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        return sut;
    }
}
