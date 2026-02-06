using MusicShare.Contracts;

namespace MusicShare.Tests.Unit.Contracts;

public class ServiceTypeExtensionsTests
{
    [Fact]
    public void ItWillReturnSpotifyForSpotify()
    {
        // Act
        var result = ServiceType.Spotify.ToRoutingKey();

        // Assert
        result.Should().Be("spotify");
    }

    [Fact]
    public void ItWillReturnAppleMusicForAppleMusic()
    {
        // Act
        var result = ServiceType.AppleMusic.ToRoutingKey();

        // Assert
        result.Should().Be("apple-music");
    }

    [Fact]
    public void ItWillReturnYouTubeMusicForYouTubeMusic()
    {
        // Act
        var result = ServiceType.YouTubeMusic.ToRoutingKey();

        // Assert
        result.Should().Be("youtube-music");
    }

    [Fact]
    public void ItWillThrowArgumentOutOfRangeExceptionForUnknown()
    {
        // Act
        var act = () => ServiceType.Unknown.ToRoutingKey();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("serviceType");
    }

    [Fact]
    public void ItWillThrowArgumentOutOfRangeExceptionForInvalidValue()
    {
        // Act
        var act = () => ((ServiceType)99).ToRoutingKey();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
