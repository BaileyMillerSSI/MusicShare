using MusicShare.Contracts;
using MusicShare.Services.Services.Music;

namespace MusicShare.Services.Services;

public interface IMusicServiceResolver
{
    ServiceType? DetectServiceType(string url);
    IMusicServiceAdapter? GetAdapter(ServiceType serviceType);
    IEnumerable<IMusicServiceAdapter> GetAllAdapters();
    IEnumerable<IMusicServiceAdapter> GetOtherAdapters(ServiceType excludeServiceType);
}
