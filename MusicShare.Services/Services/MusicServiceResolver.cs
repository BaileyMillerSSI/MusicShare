using MusicShare.Contracts;
using MusicShare.Services.Services.Music;

namespace MusicShare.Services.Services;

/// <summary>
/// Resolves the appropriate music service adapter based on service type.
/// </summary>
public class MusicServiceResolver(IEnumerable<IMusicServiceAdapter> adapters) : IMusicServiceResolver
{
    private readonly Dictionary<ServiceType, IMusicServiceAdapter> _adapters = adapters.ToDictionary(a => a.ServiceType);

    public IMusicServiceAdapter? GetAdapter(ServiceType serviceType)
    {
        return _adapters.TryGetValue(serviceType, out var adapter) ? adapter : null;
    }

    public IEnumerable<IMusicServiceAdapter> GetAllAdapters() => _adapters.Values;

    public IEnumerable<IMusicServiceAdapter> GetOtherAdapters(ServiceType excludeServiceType)
    {
        return _adapters.Values.Where(a => a.ServiceType != excludeServiceType);
    }

    public ServiceType? DetectServiceType(string url)
    {
        // Try to detect service type from URL
        if (url.Contains("spotify.com") || url.StartsWith("spotify:"))
            return ServiceType.Spotify;

        if (url.Contains("music.apple.com"))
            return ServiceType.AppleMusic;

        if (url.Contains("music.youtube.com") || url.Contains("youtube.com") || url.Contains("youtu.be"))
            return ServiceType.YouTubeMusic;

        return null;
    }
}
