namespace MusicShare.Tests.Unit.Services.Configuration;

using MusicShare.Services.Configuration;

public class FrontendSettingsTests
{
    [Fact]
    public void ItWillUseServiceDiscovery()
    {
        // Arrange
        _ = new FrontendSettings();

        // Act
        var uri = FrontendSettings.Uri;

        // Assert
        uri.Should().NotBeNull();
        uri.ToString().Should().Be("https+http://frontend/");
    }
}
