namespace DroneSkyCheck.Sync.Core.Cloud;

public sealed class DjiFlightLogImportOptions
{
    public string CheckDjiFlightLogImportUrl { get; init; } =
        "https://europe-west8-droneskycheck-d0136.cloudfunctions.net/checkDjiFlightLogImport";

    public string ImportDjiFlightLogUrl { get; init; } =
        "https://europe-west8-droneskycheck-d0136.cloudfunctions.net/importDjiFlightLog";

    public TimeSpan MinimumDelayBetweenCloudRequests { get; init; } =
        TimeSpan.FromMilliseconds(3250);

    public int MaxRateLimitRetries { get; init; } = 4;

    public TimeSpan RateLimitRetryBaseDelay { get; init; } =
        TimeSpan.FromSeconds(30);

    public TimeSpan RateLimitRetryMaxDelay { get; init; } =
        TimeSpan.FromMinutes(2);
}
