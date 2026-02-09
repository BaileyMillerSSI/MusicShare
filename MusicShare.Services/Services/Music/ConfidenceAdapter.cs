using System.Runtime.CompilerServices;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicShare.Services.Configuration.MusicServices;

namespace MusicShare.Services.Services.Music;

/// <summary>
/// Decorator that wraps any IMusicServiceAdapter to filter and rank results by confidence score.
/// Uses the ConfidenceScoreService to calculate match quality and only returns high-confidence matches.
/// </summary>
public class ConfidenceAdapter(
    IMusicServiceAdapter innerAdapter,
    IConfidenceScoreService confidenceScoreService,
    ILogger<ConfidenceAdapter> logger) : IMusicServiceAdapter
{
    public ServiceType ServiceType => innerAdapter.ServiceType;

    public Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken cancellationToken = default)
        => innerAdapter.ResolveMetadataAsync(url, cancellationToken);

    public async IAsyncEnumerable<SongSearchResult> FindSongsAsync(
        SongMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var (Result, Score) in GetCandidatesWithScoresAsync(metadata, cancellationToken)
            .Where(c => confidenceScoreService.MeetsThreshold(c.Score))
            .OrderByDescending(c => c.Score.TotalScore)
            .WithCancellation(cancellationToken))
        {
            yield return Result;
        }
    }

    public string NormalizeUrl(string url)
        => innerAdapter.NormalizeUrl(url);

    public string? ExtractSongId(string url)
        => innerAdapter.ExtractSongId(url);

    private async IAsyncEnumerable<(SongSearchResult Result, ConfidenceScore Score)> GetCandidatesWithScoresAsync(
        SongMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var candidate in innerAdapter.FindSongsAsync(metadata, cancellationToken))
        {
            var score = confidenceScoreService.CalculateScore(metadata, candidate.FoundMetadata);
            yield return (candidate, score);
        }
    }
}
