using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.MusicAdapters.Services;

/// <summary>
/// Interface for music service adapters that resolve song metadata and find songs across platforms.
/// TODO: Replace mock implementations with real API calls for production.
/// </summary>
public interface IMusicServiceAdapter
{
    ServiceType ServiceType { get; }

    /// <summary>
    /// Resolves song metadata from a service URL.
    /// </summary>
    Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for a song on this service using metadata from another service.
    /// Returns the URL if found.
    /// </summary>
    Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalizes a service URL to a canonical format.
    /// </summary>
    string NormalizeUrl(string url);

    /// <summary>
    /// Extracts the service-specific song ID from a URL.
    /// </summary>
    string? ExtractSongId(string url);
}
