using DroneSkyCheck.Sync.Models.Devices;

namespace DroneSkyCheck.Sync.Core.Devices
{
    public interface IDeviceProvider
    {
        Task<IReadOnlyList<Device>> GetDevicesAsync();
    }
}