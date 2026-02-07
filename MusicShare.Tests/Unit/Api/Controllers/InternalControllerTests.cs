using MediatR;
using Microsoft.AspNetCore.Mvc;
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
}
