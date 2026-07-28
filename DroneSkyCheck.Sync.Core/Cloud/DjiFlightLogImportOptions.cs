namespace DroneSkyCheck.Sync.Core.Cloud;

public sealed class DjiFlightLogImportOptions
{
    public string CheckDjiFlightLogImportUrl { get; init; } =
        "https://europe-west8-droneskycheck-d0136.cloudfunctions.net/checkDjiFlightLogImport";

    public string CheckDjiFlightLogImportsUrl { get; init; } =
        "https://europe-west8-droneskycheck-d0136.cloudfunctions.net/checkDjiFlightLogImports";

    public string ImportDjiFlightLogUrl { get; init; } =
        "https://europe-west8-droneskycheck-d0136.cloudfunctions.net/importDjiFlightLog";

    public TimeSpan MinimumDelayBetweenCloudRequests { get; init; } =
        TimeSpan.FromSeconds(8);

    public int MaxRateLimitRetries { get; init; } = 5;

    public TimeSpan RateLimitRetryBaseDelay { get; init; } =
        TimeSpan.FromMinutes(1);

    public TimeSpan RateLimitRetryMaxDelay { get; init; } =
        TimeSpan.FromMinutes(5);
}
