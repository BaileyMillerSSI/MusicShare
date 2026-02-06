using MusicShare.Api.Commands;
using MusicShare.Contracts;

namespace MusicShare.Tests.Unit.Api.Commands;

public class SubmitShareResponseTests
{
    [Fact]
    public void ItWillReturnResponseWithSuccessFalseWhenCallingAsFailure()
    {
        // Act
        var result = SubmitShare.Response.AsFailure("Something went wrong");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Something went wrong");
        result.ShareId.Should().BeNull();
        result.Status.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnResponseWithSuccessTrueWhenCallingAsSuccess()
    {
        // Act
        var result = SubmitShare.Response.AsSuccess("share-123", ShareStatus.Pending);

        // Assert
        result.Success.Should().BeTrue();
        result.ShareId.Should().Be("share-123");
        result.Status.Should().Be("Pending");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ItWillReturnProcessingStatusStringForProcessingStatus()
    {
        // Act
        var result = SubmitShare.Response.AsSuccess("share-456", ShareStatus.Processing);

        // Assert
        result.Status.Should().Be("Processing");
    }

    [Fact]
    public void ItWillReturnCompletedStatusStringForCompletedStatus()
    {
        // Act
        var result = SubmitShare.Response.AsSuccess("share-789", ShareStatus.Completed);

        // Assert
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public void ItWillReturnEmptyErrorStringWhenAsFailureCalledWithEmptyError()
    {
        // Act
        var result = SubmitShare.Response.AsFailure("");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEmpty();
    }
}
