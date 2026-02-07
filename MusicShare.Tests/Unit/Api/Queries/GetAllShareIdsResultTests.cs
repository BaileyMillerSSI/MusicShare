using MusicShare.Api.Queries;

namespace MusicShare.Tests.Unit.Api.Queries;

public class GetAllShareIdsResultTests
{
    [Fact]
    public void ItWillReturnResultWithShareIdsWhenCallingSuccess()
    {
        // Arrange
        var shareIds = new List<string> { "abc123", "def456" };

        // Act
        var result = GetAllShareIds.Result.Success(shareIds);

        // Assert
        result.ShareIds.Should().HaveCount(2);
        result.ShareIds.Should().ContainInOrder("abc123", "def456");
    }

    [Fact]
    public void ItWillReturnResultWithEmptyListWhenCallingSuccessWithNoIds()
    {
        // Act
        var result = GetAllShareIds.Result.Success(new List<string>());

        // Assert
        result.ShareIds.Should().BeEmpty();
    }

    [Fact]
    public void ItWillPreserveShareIdOrderWhenCallingSuccess()
    {
        // Arrange
        var shareIds = new List<string> { "zzz999", "aaa111", "mmm555" };

        // Act
        var result = GetAllShareIds.Result.Success(shareIds);

        // Assert
        result.ShareIds[0].Should().Be("zzz999");
        result.ShareIds[1].Should().Be("aaa111");
        result.ShareIds[2].Should().Be("mmm555");
    }

    [Fact]
    public void ItWillReturnReadOnlyListFromSuccess()
    {
        // Arrange
        var shareIds = new List<string> { "id1" };

        // Act
        var result = GetAllShareIds.Result.Success(shareIds);

        // Assert
        result.ShareIds.Should().BeAssignableTo<IReadOnlyList<string>>();
    }
}
