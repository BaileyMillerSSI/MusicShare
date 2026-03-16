namespace MusicShare.Services.Models;

/// <summary>
/// Represents the confidence level of a song match.
/// </summary>
public enum ConfidenceLevel
{
    Low = 0,      // < 0.65
    Medium = 1,   // >= 0.65 and < 0.85
    High = 2      // >= 0.85
}

/// <summary>
/// Represents a confidence score for a song match with component breakdown.
/// </summary>
public record ConfidenceScore
{
    /// <summary>
    /// Minimum artist score required for a match to be considered valid.
    /// Even if total score passes threshold, artist score must meet this minimum.
    /// </summary>
    public const double MinimumArtistScore = 0.5;

    /// <summary>
    /// Overall weighted confidence score (0.0 to 1.0).
    /// </summary>
    public required double TotalScore { get; init; }

    /// <summary>
    /// Component score for title matching (0.0 to 1.0).
    /// </summary>
    public required double TitleScore { get; init; }

    /// <summary>
    /// Component score for artist matching (0.0 to 1.0).
    /// </summary>
    public required double ArtistScore { get; init; }

    /// <summary>
    /// Component score for album matching (0.0 to 1.0).
    /// </summary>
    public required double AlbumScore { get; init; }

    /// <summary>
    /// Component score for duration matching (0.0 to 1.0).
    /// </summary>
    public required double DurationScore { get; init; }

    /// <summary>
    /// Confidence level derived from the total score.
    /// </summary>
    public ConfidenceLevel Level => TotalScore switch
    {
        >= 0.85 => ConfidenceLevel.High,
        >= 0.65 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low
    };

    /// <summary>
    /// Determines if the score meets the specified threshold.
    /// Requires both the total score to meet the threshold AND the artist score to meet MinimumArtistScore.
    /// </summary>
    public bool MeetsThreshold(double threshold) => TotalScore >= threshold && ArtistScore >= MinimumArtistScore;
}
