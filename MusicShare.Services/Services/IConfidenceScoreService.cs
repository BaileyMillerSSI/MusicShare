using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

/// <summary>
/// Service for calculating confidence scores when matching songs across different music platforms.
/// </summary>
public interface IConfidenceScoreService
{
    /// <summary>
    /// Calculates a confidence score comparing source metadata with candidate metadata.
    /// </summary>
    /// <param name="source">The original song metadata.</param>
    /// <param name="candidate">The candidate song metadata to compare against.</param>
    /// <returns>A confidence score with total and component scores.</returns>
    ConfidenceScore CalculateScore(SongMetadata source, SongMetadata candidate);

    /// <summary>
    /// Determines if the calculated score meets the given threshold.
    /// </summary>
    /// <param name="score">The confidence score to check.</param>
    /// <param name="threshold">The threshold value (0.0 to 1.0).</param>
    /// <returns>True if the score meets the threshold, false otherwise.</returns>
    bool MeetsThreshold(ConfidenceScore score, double threshold);
}
