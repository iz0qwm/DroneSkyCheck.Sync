using DroneSkyCheck.Sync.Core.Native.Windows;
using DroneSkyCheck.Sync.Models.FlightLogs;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
namespace DroneSkyCheck.Sync.Core.Devices;

public sealed class MtpFlightLogProvider
{
    public IReadOnlyList<FlightLogFile> GetFlightLogs(string deviceId)
    {
        try
        {
            using var explorer = new PortableDeviceExplorer();

            explorer.Open(deviceId);

            var folder = explorer.FindFolder(
                "Android",
                "data",
                "dji.go.v5",
                "files",
                "FlightRecord");

            if (folder == null)
                return [];

            return explorer
                .GetChildren(folder.Id)
                .Where(x =>
                    x.IsFile &&
                    x.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                    (x.Name.StartsWith("DJIFlightRecord_", StringComparison.OrdinalIgnoreCase) ||
                     x.Name.StartsWith("FlightRecord_", StringComparison.OrdinalIgnoreCase)))
                .Select(x => new FlightLogFile
                {
                    FileName = x.Name,
                    FullPath = x.Name,
                    SourceObjectId = x.Id,
                    Size = (long)x.Size
                })
                .OrderByDescending(x => x.FileName)
                .ToList();
        }
        catch (COMException)
        {
            return [];
        }
    }

    public byte[] ReadFlightLogBytes(
        string deviceId,
        FlightLogFile flightLog)
    {
        if (string.IsNullOrWhiteSpace(flightLog.SourceObjectId))
        {
            throw new InvalidOperationException(
                "Il Flight Log MTP non contiene un identificativo leggibile.");
        }

        using var explorer = new PortableDeviceExplorer();

        explorer.Open(deviceId);

        return explorer.ReadObjectBytes(flightLog.SourceObjectId);
    }
}
