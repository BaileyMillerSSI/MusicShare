using Microsoft.Extensions.Options;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Configuration.MusicServices;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

/// <summary>
/// Service for calculating confidence scores when matching songs across different music platforms.
/// Uses weighted scoring: Title (40%), Artist (25%), Album (25%), Duration (10%).
/// </summary>
public class ConfidenceScoreService(IOptionsMonitor<MusicConfiguration> musicConfigurationMonitor) : IConfidenceScoreService
{
    private const double TitleWeight = 0.40;
    private const double ArtistWeight = 0.25;
    private const double AlbumWeight = 0.25;
    private const double DurationWeight = 0.10;
    private readonly double DefaultThreshold = musicConfigurationMonitor.CurrentValue.ConfidenceThreshold;

    /// <inheritdoc />
    public ConfidenceScore CalculateScore(SongMetadata source, SongMetadata candidate)
    {
        var titleScore = CalculateTitleScore(source.Title, candidate.Title);
        var artistScore = CalculateArtistScore(source.Artists, candidate.Artists);
        var albumScore = CalculateAlbumScore(source.Album, candidate.Album);
        var durationScore = CalculateDurationScore(source.Duration, candidate.Duration);

        var totalScore = (titleScore * TitleWeight) +
                        (artistScore * ArtistWeight) +
                        (albumScore * AlbumWeight) +
                        (durationScore * DurationWeight);

        return new ConfidenceScore
        {
            TotalScore = totalScore,
            TitleScore = titleScore,
            ArtistScore = artistScore,
            AlbumScore = albumScore,
            DurationScore = durationScore
        };
    }

    /// <inheritdoc />
    public bool MeetsThreshold(ConfidenceScore score) => MeetsThreshold(score, DefaultThreshold);

    /// <inheritdoc />
    public bool MeetsThreshold(ConfidenceScore score, double threshold) => score.MeetsThreshold(threshold);

    /// <summary>
    /// Calculates title similarity score using Levenshtein distance with exact match bonus.
    /// </summary>
    private static double CalculateTitleScore(string sourceTitle, string candidateTitle)
    {
        var normalizedSource = NormalizeString(sourceTitle);
        var normalizedCandidate = NormalizeString(candidateTitle);

        // Exact match gets perfect score
        if (normalizedSource == normalizedCandidate)
        {
            return 1.0;
        }

        // Calculate Levenshtein distance
        var distance = LevenshteinDistance(normalizedSource, normalizedCandidate);
        var maxLength = Math.Max(normalizedSource.Length, normalizedCandidate.Length);

        if (maxLength == 0)
        {
            return 0.0;
        }

        // Convert distance to similarity score (0.0 to 1.0)
        var similarity = 1.0 - ((double)distance / maxLength);

        return Math.Max(0.0, similarity);
    }

    /// <summary>
    /// Calculates artist similarity score by comparing artist arrays.
    /// </summary>
    private static double CalculateArtistScore(IEnumerable<string> sourceArtists, IEnumerable<string> candidateArtists)
    {
        var sourceList = sourceArtists.Select(NormalizeString).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        var candidateList = candidateArtists.Select(NormalizeString).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();

        if (sourceList.Count == 0 && candidateList.Count == 0)
        {
            return 1.0; // Both empty means perfect match
        }

        if (sourceList.Count == 0 || candidateList.Count == 0)
        {
            return 0.0; // One empty, one not means no match
        }

        // Check for exact matches
        var exactMatches = sourceList.Intersect(candidateList, StringComparer.OrdinalIgnoreCase).Count();
        if (exactMatches > 0)
        {
            // Score based on proportion of matched artists
            var matchRatio = (double)exactMatches / Math.Max(sourceList.Count, candidateList.Count);
            return matchRatio;
        }

        // No exact matches - try fuzzy matching for primary artist
        var primarySource = sourceList.First();
        var primaryCandidate = candidateList.First();

        var distance = LevenshteinDistance(primarySource, primaryCandidate);
        var maxLength = Math.Max(primarySource.Length, primaryCandidate.Length);

        if (maxLength == 0)
        {
            return 0.0;
        }

        var similarity = 1.0 - ((double)distance / maxLength);
        return Math.Max(0.0, similarity);
    }

    /// <summary>
    /// Calculates album similarity score with fuzzy matching.
    /// Returns 0.5 (neutral) if either album is missing.
    /// </summary>
    private static double CalculateAlbumScore(string? sourceAlbum, string? candidateAlbum)
    {
        // If either album is missing, return neutral score
        if (string.IsNullOrWhiteSpace(sourceAlbum) || string.IsNullOrWhiteSpace(candidateAlbum))
        {
            return 0.5;
        }

        var normalizedSource = NormalizeString(sourceAlbum);
        var normalizedCandidate = NormalizeString(candidateAlbum);

        // Exact match
        if (normalizedSource == normalizedCandidate)
        {
            return 1.0;
        }

        // Fuzzy match using Levenshtein distance
        var distance = LevenshteinDistance(normalizedSource, normalizedCandidate);
        var maxLength = Math.Max(normalizedSource.Length, normalizedCandidate.Length);

        if (maxLength == 0)
        {
            return 0.5; // Both empty after normalization
        }

        var similarity = 1.0 - ((double)distance / maxLength);
        return Math.Max(0.0, similarity);
    }

    /// <summary>
    /// Calculates duration similarity score with tolerance-based scoring.
    /// 0s difference = 1.0, 2s = 0.95, 10s = 0.85, 30s = 0.70.
    /// </summary>
    private static double CalculateDurationScore(TimeSpan? sourceDuration, TimeSpan? candidateDuration)
    {
        // If either duration is missing, return neutral score
        if (!sourceDuration.HasValue || !candidateDuration.HasValue)
        {
            return 0.5;
        }

        var differenceSeconds = Math.Abs((sourceDuration.Value - candidateDuration.Value).TotalSeconds);

        // Perfect match
        if (differenceSeconds == 0)
        {
            return 1.0;
        }

        // Tolerance-based scoring
        return differenceSeconds switch
        {
            <= 2 => 0.95,
            <= 5 => 0.90,
            <= 10 => 0.85,
            <= 15 => 0.80,
            <= 20 => 0.75,
            <= 30 => 0.70,
            <= 45 => 0.60,
            <= 60 => 0.50,
            _ => Math.Max(0.0, 1.0 - (differenceSeconds / 300.0)) // Linear decay beyond 60s
        };
    }

    /// <summary>
    /// Normalizes a string for comparison by converting to lowercase and removing special characters.
    /// </summary>
    private static string NormalizeString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Convert to lowercase
        var normalized = input.ToLowerInvariant();

        // Remove common noise words and characters
        normalized = normalized
            .Replace("(", " ")
            .Replace(")", " ")
            .Replace("[", " ")
            .Replace("]", " ")
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace(".", " ")
            .Replace(",", " ")
            .Replace("&", "and");

        // Remove extra whitespace
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.IsNullOrEmpty(target) ? 0 : target.Length;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        var sourceLength = source.Length;
        var targetLength = target.Length;

        // Create distance matrix
        var distance = new int[sourceLength + 1, targetLength + 1];

        // Initialize first row and column
        for (var i = 0; i <= sourceLength; i++)
        {
            distance[i, 0] = i;
        }

        for (var j = 0; j <= targetLength; j++)
        {
            distance[0, j] = j;
        }

        // Calculate distances
        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,      // Deletion
                        distance[i, j - 1] + 1),     // Insertion
                    distance[i - 1, j - 1] + cost    // Substitution
                );
            }
        }

        return distance[sourceLength, targetLength];
    }
}
