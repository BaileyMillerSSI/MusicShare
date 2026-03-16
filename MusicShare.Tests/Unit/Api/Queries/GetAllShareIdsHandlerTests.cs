using MusicShare.Api.Queries;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Queries;

public class GetAllShareIdsHandlerTests
{
    [Fact]
    public async Task ItWillReturnShareIdsForCompletedRequests()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var expectedIds = new List<string> { "abc123def456", "789ghi012jkl" };
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIds);

        var sut = mock.Create<GetAllShareIds.Handler>();

        // Act
        var result = await sut.Handle(new GetAllShareIds.Query(), CancellationToken.None);

        // Assert
        result.ShareIds.Should().HaveCount(2);
        result.ShareIds.Should().ContainInOrder("abc123def456", "789ghi012jkl");
    }

    [Fact]
    public async Task ItWillReturnEmptyListWhenNoCompletedRequests()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = mock.Create<GetAllShareIds.Handler>();

        // Act
        var result = await sut.Handle(new GetAllShareIds.Query(), CancellationToken.None);

        // Assert
        result.ShareIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillReturnSingleShareIdWhenOnlyOneCompleted()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var expectedIds = new List<string> { "only1shareid" };
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIds);

        var sut = mock.Create<GetAllShareIds.Handler>();

        // Act
        var result = await sut.Handle(new GetAllShareIds.Query(), CancellationToken.None);

        // Assert
        result.ShareIds.Should().ContainSingle().Which.Should().Be("only1shareid");
    }

    [Fact]
    public async Task ItWillPassCancellationTokenToService()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var cts = new CancellationTokenSource();
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(cts.Token))
            .ReturnsAsync([]);

        var sut = mock.Create<GetAllShareIds.Handler>();

        // Act
        await sut.Handle(new GetAllShareIds.Query(), cts.Token);

        // Assert
        mock.Mock<IShareRequestService>().Verify(
            x => x.GetAllCompletedShareIdsAsync(cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task ItWillCallServiceExactlyOnce()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        mock.Mock<IShareRequestService>()
            .Setup(x => x.GetAllCompletedShareIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["id1", "id2"]);

        var sut = mock.Create<GetAllShareIds.Handler>();

        // Act
        await sut.Handle(new GetAllShareIds.Query(), CancellationToken.None);

        // Assert
        mock.Mock<IShareRequestService>().Verify(
            x => x.GetAllCompletedShareIdsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
