using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using Microsoft.Extensions.Logging;

namespace MusicShare.Services.Services.Music;

/// <summary>
/// Decorator that wraps any IMusicServiceAdapter to filter and rank results by confidence score.
/// Uses the ConfidenceScoreService to calculate match quality and only returns high-confidence matches.
/// </summary>
public class ConfidenceAdapter(
    IMusicServiceAdapter innerAdapter,
    IConfidenceScoreService confidenceScoreService,
    ILogger<ConfidenceAdapter> logger,
    double confidenceThreshold = 0.65) : IMusicServiceAdapter
{
    public ServiceType ServiceType => innerAdapter.ServiceType;

    public Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken cancellationToken = default)
        => innerAdapter.ResolveMetadataAsync(url, cancellationToken);

    public async IAsyncEnumerable<SongSearchResult> FindSongsAsync(
        SongMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<(SongSearchResult Result, ConfidenceScore Score)>();

        await foreach (var candidate in innerAdapter.FindSongsAsync(metadata, cancellationToken))
        {
            var score = confidenceScoreService.CalculateScore(metadata, candidate.FoundMetadata);
            candidates.Add((candidate, score));

            logger.LogDebug(
                "Evaluated candidate for {Title} on {Service}: URL={Url}, Score={Score:P0}",
                metadata.Title,
                ServiceType,
                candidate.Url,
                score.Total);
        }

        if (candidates.Count == 0)
        {
            logger.LogWarning(
                "No candidates found for {Title} on {Service}",
                metadata.Title,
                ServiceType);
            yield break;
        }

        var filteredCandidates = candidates
            .Where(c => c.Score.Total >= confidenceThreshold)
            .OrderByDescending(c => c.Score.Total)
            .ToList();

        if (filteredCandidates.Count == 0)
        {
            logger.LogWarning(
                "All {Count} candidates for {Title} on {Service} filtered out (threshold={Threshold:P0}). Best score: {BestScore:P0}",
                candidates.Count,
                metadata.Title,
                ServiceType,
                confidenceThreshold,
                candidates.Max(c => c.Score.Total));
            yield break;
        }

        logger.LogInformation(
            "Evaluated {Total} candidates for {Title} on {Service}, {Filtered} meet threshold. Best match: {BestScore:P0}",
            candidates.Count,
            metadata.Title,
            ServiceType,
            filteredCandidates.Count,
            filteredCandidates[0].Score.Total);

        foreach (var (result, _) in filteredCandidates)
        {
            yield return result;
        }
    }

    [Obsolete("Use FindSongsAsync() instead. This method will be removed in a future version.")]
    public async Task<string?> FindSongAsync(SongMetadata metadata, CancellationToken cancellationToken = default)
    {
        await foreach (var result in FindSongsAsync(metadata, cancellationToken))
        {
            logger.LogDebug(
                "Returning best match for {Title} on {Service}: {Url}",
                metadata.Title,
                ServiceType,
                result.Url);
            return result.Url;
        }

        logger.LogInformation(
            "No matches above threshold for {Title} on {Service}",
            metadata.Title,
            ServiceType);
        return null;
    }

    public string NormalizeUrl(string url)
        => innerAdapter.NormalizeUrl(url);

    public string? ExtractSongId(string url)
        => innerAdapter.ExtractSongId(url);
}
