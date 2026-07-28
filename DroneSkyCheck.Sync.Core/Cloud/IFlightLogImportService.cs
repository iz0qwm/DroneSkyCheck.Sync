namespace DroneSkyCheck.Sync.Core.Cloud;

public interface IFlightLogImportService
{
    Task<DjiFlightLogImportCheckResult> CheckDjiFlightLogImportAsync(
        string idToken,
        string operatorId,
        string ownerUid,
        string sha256,
        long sizeBytes,
        string filename,
        CancellationToken cancellationToken = default);

    Task<DjiFlightLogImportResult> ImportDjiFlightLogAsync(
        string idToken,
        string operatorId,
        string ownerUid,
        string sha256,
        byte[] content,
        string filename,
        CancellationToken cancellationToken = default);
}

public sealed record DjiFlightLogImportCheckResult(
    bool AlreadyImported);

public sealed record DjiFlightLogImportResult(
    bool IsDuplicateFlight,
    bool PersistedFleetRegistry,
    bool RequiresReview,
    string ReconciliationStatus,
    string ReconciliationAction,
    string ReconciliationIssue);
