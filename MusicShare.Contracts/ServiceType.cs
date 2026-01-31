namespace MusicShare.Contracts;

public enum ServiceType
{
    Unknown = 0,
    Spotify = 1,
    AppleMusic = 2,
    YouTubeMusic = 3
}

public static class ServiceTypeExtensions
{
    /// <summary>
    /// Converts a ServiceType to its corresponding RabbitMQ routing key.
    /// </summary>
    public static string ToRoutingKey(this ServiceType serviceType) => serviceType switch
    {
        ServiceType.Spotify => "spotify",
        ServiceType.AppleMusic => "apple-music",
        ServiceType.YouTubeMusic => "youtube-music",
        _ => throw new ArgumentOutOfRangeException(nameof(serviceType), $"No routing key defined for {serviceType}")
    };
}
