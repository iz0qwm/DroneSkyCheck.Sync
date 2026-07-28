namespace DroneSkyCheck.Sync.Core.Native.Windows;

public sealed class PortableDeviceObject
{
    public string Id { get; init; } = string.Empty;

    public string ParentId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsFolder { get; init; }

    public bool IsFile { get; init; }

    public ulong Size { get; init; }
}