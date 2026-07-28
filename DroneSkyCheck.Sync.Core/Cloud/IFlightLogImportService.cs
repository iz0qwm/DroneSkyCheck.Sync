namespace DroneSkyCheck.Sync.Core.Cloud;

public sealed class DjiFlightLogRateLimitException : HttpRequestException
{
    public DjiFlightLogRateLimitException(
        string message,
        TimeSpan retryAfter)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}

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

    Task<DjiFlightLogImportBatchCheckResult> CheckDjiFlightLogImportsAsync(
        string idToken,
        string operatorId,
        string ownerUid,
        IReadOnlyList<DjiFlightLogImportBatchCheckItem> items,
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

public sealed record DjiFlightLogImportBatchCheckItem(
    string Sha256,
    long SizeBytes,
    string Filename);

public sealed record DjiFlightLogImportBatchCheckResult(
    IReadOnlyList<DjiFlightLogImportBatchCheckResultItem> Results);

public sealed record DjiFlightLogImportBatchCheckResultItem(
    string Sha256,
    string Filename,
    bool AlreadyImported);

public sealed record DjiFlightLogImportResult(
    bool IsDuplicateFlight,
    bool PersistedFleetRegistry,
    bool RequiresReview,
    string ReconciliationStatus,
    string ReconciliationAction,
    string ReconciliationIssue);
