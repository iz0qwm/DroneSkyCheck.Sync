using DroneSkyCheck.Sync.Models.Devices;
using DroneSkyCheck.Sync.Models.FlightLogs;

public interface IFlightLogProvider
{
    Task<IReadOnlyList<FlightLogFile>> FindLogsAsync(
        CancellationToken cancellationToken = default);
}