namespace MusicShare.Contracts.Messages;

/// <summary>Requests an idempotent rebuild of the public metrics snapshot.</summary>
/// <param name="AllowReconciliationDecrease">
/// Permits a lower total only for a successful duplicate-share reconciliation.
/// Ordinary refreshes must leave this false.
/// </param>
public record RefreshPublicMetrics(bool AllowReconciliationDecrease = false);
