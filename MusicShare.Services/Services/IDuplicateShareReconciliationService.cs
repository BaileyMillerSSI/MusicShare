namespace MusicShare.Services.Services;

public interface IDuplicateShareReconciliationService
{
    Task<DuplicateShareReconciliationResult> ReconcileAsync(DuplicateShareReconciliationRequest request, CancellationToken cancellationToken);
}

public enum DuplicateShareReconciliationMode { DryRun, Apply }

public record DuplicateShareReconciliationRequest(
    string FirstShareId,
    string SecondShareId,
    string? CanonicalShareId,
    DuplicateShareReconciliationMode Mode,
    string? Fingerprint);

public record DuplicateShareIdentity(int ServiceType, string ServiceSongId);

public record DuplicateShareReconciliationResult(
    bool Success,
    bool Changed,
    string? Error,
    string? OperationId,
    string? Fingerprint,
    string? CanonicalShareId,
    string? AliasShareId,
    IReadOnlyList<DuplicateShareIdentity> SharedIdentities)
{
    public static DuplicateShareReconciliationResult Failure(string error) => new(false, false, error, null, null, null, null, []);
}
