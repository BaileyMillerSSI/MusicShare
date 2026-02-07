using MusicShare.Api.Queries;
using MusicShare.Services.Models;

namespace MusicShare.Tests.Unit.Api.Queries;

public class GetShareResultResultTests
{
    [Fact]
    public void ItWillReturnResultWithFoundFalseWhenCallingNotFound()
    {
        // Act
        var result = GetShareResult.Result.NotFound();

        // Assert
        result.Found.Should().BeFalse();
        result.Response.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnResultWithFoundTrueWhenCallingSuccess()
    {
        // Arrange
        var response = new ShareResultResponse
        {
            ShareId = "test-123",
            Status = "Completed",
            Song = null
        };

        // Act
        var result = GetShareResult.Result.Success(response);

        // Assert
        result.Found.Should().BeTrue();
        result.Response.Should().BeSameAs(response);
    }

    [Fact]
    public void ItWillPreserveResponseDataWhenCallingSuccess()
    {
        // Arrange
        var response = new ShareResultResponse
        {
            ShareId = "share-abc",
            Status = "Processing",
            Song = new SongDetails
            {
                Id = "song-1",
                Title = "My Song",
                Artists = ["Artist A", "Artist B"],
                Status = "Resolved",
                Links = []
            }
        };

        // Act
        var result = GetShareResult.Result.Success(response);

        // Assert
        result.Response!.ShareId.Should().Be("share-abc");
        result.Response.Status.Should().Be("Processing");
        result.Response.Song.Should().NotBeNull();
        result.Response.Song!.Title.Should().Be("My Song");
        result.Response.Song.Artists.Should().HaveCount(2);
    }
}
