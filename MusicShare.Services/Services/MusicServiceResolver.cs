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
        if (MusicUrlParser.IsSpotifyUrl(url))
            return ServiceType.Spotify;

        if (MusicUrlParser.IsAppleMusicUrl(url))
            return ServiceType.AppleMusic;

        if (MusicUrlParser.IsYouTubeMusicUrl(url))
            return ServiceType.YouTubeMusic;

        return null;
    }
}
