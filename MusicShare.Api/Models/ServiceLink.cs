using MusicShare.Contracts;

namespace MusicShare.Api.Models;

public record ServiceLink
{
    public required ServiceType ServiceType { get; init; }
    public required string Url { get; init; }
}
