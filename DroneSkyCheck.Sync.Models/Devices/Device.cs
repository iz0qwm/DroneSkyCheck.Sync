namespace DroneSkyCheck.Sync.Models.Devices
{
    public class Device
    {
        public string Name { get; set; } = string.Empty;

        public string DriveLetter { get; set; } = string.Empty;

        public string RootPath { get; set; } = string.Empty;

        public bool IsRemovable { get; set; }

        public string DeviceId { get; set; } = string.Empty;

        public bool IsMtp { get; set; }


    }
}