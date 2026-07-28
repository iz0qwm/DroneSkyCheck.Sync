using DroneSkyCheck.Sync.Core.Native.Windows;
using DroneSkyCheck.Sync.Models.Devices;

namespace DroneSkyCheck.Sync.Core.Devices;

public class MtpDeviceProvider : IDeviceProvider
{
    public Task<IReadOnlyList<Device>> GetDevicesAsync()
    {
        var devices = new List<Device>();

        var mtpDevices = PortableDeviceHelper.GetConnectedDevices();

        foreach (var mtp in mtpDevices)
        {
            devices.Add(new Device
            {
                Name = mtp.FriendlyName,
                DeviceId = mtp.DeviceId,
                IsMtp = true
            });
        }

        return Task.FromResult<IReadOnlyList<Device>>(devices);
    }
}