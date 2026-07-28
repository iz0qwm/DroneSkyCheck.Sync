namespace DroneSkyCheck.Sync;

public sealed class TransferLogEntry
{
    public int Number { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Error { get; set; } = string.Empty;
}
