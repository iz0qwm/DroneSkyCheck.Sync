using DroneSkyCheck.Sync.Models.Devices;

namespace DroneSkyCheck.Sync.Core.Devices
{
    public class DriveDeviceProvider : IDeviceProvider
    {
        public Task<IReadOnlyList<Device>> GetDevicesAsync()
        {
            var devices = new List<Device>();

            foreach (var drive in DriveInfo.GetDrives())
            {
                devices.Add(new Device
                {
                    Name = drive.Name,
                    DriveLetter = drive.Name,
                    RootPath = drive.RootDirectory.FullName,
                    IsRemovable = drive.DriveType == DriveType.Removable
                });
            }

            return Task.FromResult<IReadOnlyList<Device>>(devices);
        }
    }
}