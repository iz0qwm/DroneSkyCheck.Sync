using Vanara.PInvoke;
using static Vanara.PInvoke.PortableDeviceApi;
using System.Text;

namespace DroneSkyCheck.Sync.Core.Native.Windows;

public static class PortableDeviceHelper
{
    public static IReadOnlyList<PortableDeviceInfo> GetConnectedDevices()
    {
        var result = new List<PortableDeviceInfo>();

        IPortableDeviceManager manager =
            (IPortableDeviceManager)new PortableDeviceManager();

        manager.RefreshDeviceList();

        uint deviceCount = 0;

        manager.GetDevices(null, ref deviceCount);

        if (deviceCount == 0)
            return result;

        var deviceIds = new string[deviceCount];

        manager.GetDevices(deviceIds, ref deviceCount);

        foreach (var deviceId in deviceIds)
        {
            var friendlyName = GetFriendlyName(manager, deviceId);

            result.Add(new PortableDeviceInfo
            {
                DeviceId = deviceId,
                FriendlyName = GetFriendlyName(manager, deviceId),
                Manufacturer = GetManufacturer(manager, deviceId),
                Description = GetDescription(manager, deviceId)
            });
        }

        return result;
    }

    private static string GetFriendlyName(
        IPortableDeviceManager manager,
        string deviceId)
    {
        uint length = 0;

        // Prima chiamata: recupera la lunghezza del buffer
        manager.GetDeviceFriendlyName(deviceId, null, ref length);

        if (length == 0)
            return deviceId;

        var buffer = new StringBuilder((int)length);

        // Seconda chiamata: legge il nome
        var hr = manager.GetDeviceFriendlyName(deviceId, buffer, ref length);

        if (hr.Failed)
            return deviceId;

        return buffer.ToString();
    }

    private static string GetManufacturer(
    IPortableDeviceManager manager,
    string deviceId)
    {
        uint length = 0;

        manager.GetDeviceManufacturer(deviceId, null, ref length);

        if (length == 0)
            return string.Empty;

        var buffer = new StringBuilder((int)length);

        var hr = manager.GetDeviceManufacturer(deviceId, buffer, ref length);

        return hr.Succeeded
            ? buffer.ToString()
            : string.Empty;
    }

    private static string GetDescription(
    IPortableDeviceManager manager,
    string deviceId)
    {
        uint length = 0;

        manager.GetDeviceDescription(deviceId, null, ref length);

        if (length == 0)
            return string.Empty;

        var buffer = new StringBuilder((int)length);

        var hr = manager.GetDeviceDescription(deviceId, buffer, ref length);

        return hr.Succeeded
            ? buffer.ToString()
            : string.Empty;
    }

}