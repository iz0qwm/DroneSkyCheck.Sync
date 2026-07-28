using Microsoft.Win32;

namespace DroneSkyCheck.Sync.Core.Cloud;

internal static class WindowsProtocolRegistration
{
    public static void Register(
        string protocolScheme)
    {
        var executablePath =
            Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        using var protocolKey =
            Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{protocolScheme}");

        protocolKey.SetValue(string.Empty, $"URL:{protocolScheme}");
        protocolKey.SetValue("URL Protocol", string.Empty);

        using var commandKey =
            protocolKey.CreateSubKey(@"shell\open\command");

        commandKey.SetValue(
            string.Empty,
            $"\"{executablePath}\" \"%1\"");
    }
}

