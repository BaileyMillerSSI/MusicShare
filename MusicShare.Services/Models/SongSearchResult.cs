using MusicShare.Contracts.Messages;

namespace MusicShare.Services.Models;

/// <summary>
/// Represents a song search result from a music service adapter.
/// Contains the URL and metadata for a candidate match.
/// </summary>
public record SongSearchResult(string Url, SongMetadata FoundMetadata);
