namespace MusicShare.Tests.Unit.Services.Configuration;

using MusicShare.Services.Configuration;

public class FrontendSettingsTests
{
    [Fact]
    public void ItWillReturnHttpsUriWhenBothUrlsAreSet()
    {
        // Arrange
        var settings = new FrontendSettings
        {
            UrlHttp = "http://localhost:3000",
            UrlHttps = "https://localhost:3001"
        };

        // Act
        var uri = settings.Uri;

        // Assert
        uri.Should().NotBeNull();
        uri.ToString().Should().Be("https://localhost:3001/");
    }

    [Fact]
    public void ItWillReturnHttpsUriWhenOnlyHttpsIsSet()
    {
        // Arrange
        var settings = new FrontendSettings
        {
            UrlHttps = "https://example.com"
        };

        // Act
        var uri = settings.Uri;

        // Assert
        uri.Should().NotBeNull();
        uri.ToString().Should().Be("https://example.com/");
    }

    [Fact]
    public void ItWillReturnHttpUriWhenOnlyHttpIsSet()
    {
        // Arrange
        var settings = new FrontendSettings
        {
            UrlHttp = "http://example.com",
            UrlHttps = ""
        };

        // Act
        var uri = settings.Uri;

        // Assert
        uri.Should().NotBeNull();
        uri.ToString().Should().Be("http://example.com/");
    }

    [Fact]
    public void ItWillReturnHttpUriWhenHttpsIsNull()
    {
        // Arrange
        var settings = new FrontendSettings
        {
            UrlHttp = "http://example.com",
            UrlHttps = null!
        };

        // Act
        var uri = settings.Uri;

        // Assert
        uri.Should().NotBeNull();
        uri.ToString().Should().Be("http://example.com/");
    }

    [Fact]
    public void ItWillThrowWhenBothUrlsAreEmpty()
    {
        // Arrange
        var settings = new FrontendSettings
        {
            UrlHttp = "",
            UrlHttps = ""
        };

        // Act & Assert
        var act = () => _ = settings.Uri;
        act.Should().Throw<UriFormatException>();
    }
}
