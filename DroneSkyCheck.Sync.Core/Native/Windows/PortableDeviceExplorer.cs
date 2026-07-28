using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Vanara.PInvoke;
using static Vanara.PInvoke.PortableDeviceApi;
namespace DroneSkyCheck.Sync.Core.Native.Windows;

/// <summary>
/// Esplora il contenuto di un dispositivo MTP tramite
/// Windows Portable Device API.
/// </summary>
public sealed class PortableDeviceExplorer : IDisposable
{
    private IPortableDevice? _device;

    private IPortableDeviceContent? _content;

    private IPortableDeviceProperties? _properties;
    public bool IsOpen => _device != null;
    /// <summary>
    /// Apre una connessione con il dispositivo MTP.
    /// </summary>
    public void Open(string deviceId)
    {
        if (_device != null)
            throw new InvalidOperationException("Il dispositivo è già aperto.");

        _device = (IPortableDevice)new PortableDevice();

        var clientInfo =
            (IPortableDeviceValues)new PortableDeviceValues();

        _device.Open(deviceId, clientInfo);

        _content = _device.Content();

        _properties = _content.Properties();

       
    }

    /// <summary>
    /// Chiude il dispositivo.
    /// </summary>
    public void Close()
    {
        _device = null;
    }

    public void Dispose()
    {
        Close();
    }


    public IReadOnlyList<PortableDeviceObject> GetRootObjects()
    {
        return GetChildren(PortableDeviceApi.WPD_DEVICE_OBJECT_ID);
    }

    public IReadOnlyList<PortableDeviceObject> GetChildren(string parentObjectId)
    {
        if (_content == null)
            throw new InvalidOperationException("Il dispositivo non è aperto.");

        var result = new List<PortableDeviceObject>();
        Debug.WriteLine($"EnumObjects({parentObjectId})");

        var enumObjects = _content.EnumObjects(
            0,
            parentObjectId,
            null);

        while (true)
        {
            var objectIds = new string[1];

            var hr = enumObjects.Next(
                1,
                objectIds,
                out uint fetched);

            if (hr.Failed || fetched == 0)
                break;


            // Crea la lista
            result.Add(CreateObject(objectIds[0], parentObjectId));

            //
        }

        return result;
    }

    public byte[] ReadObjectBytes(string objectId)
    {
        if (_content == null)
            throw new InvalidOperationException("Il dispositivo non è aperto.");

        var resources = _content.Transfer();
        var key = PortableDeviceApi.WPD_RESOURCE_DEFAULT;
        uint optimalBufferSize = 0;
        IStream? stream = null;

        try
        {
            stream = resources.GetStream(
                objectId,
                in key,
                STGM.STGM_READ,
                out optimalBufferSize);

            var bufferSize = 64 * 1024;

            if (optimalBufferSize > 0 &&
                optimalBufferSize <= int.MaxValue)
            {
                bufferSize = (int)optimalBufferSize;
            }

            using var destination = new MemoryStream();
            var buffer = new byte[bufferSize];
            var bytesReadPointer = Marshal.AllocCoTaskMem(sizeof(int));

            try
            {
                while (true)
                {
                    stream.Read(
                        buffer,
                        buffer.Length,
                        bytesReadPointer);

                    var bytesRead =
                        Marshal.ReadInt32(bytesReadPointer);

                    if (bytesRead <= 0)
                        break;

                    destination.Write(buffer, 0, bytesRead);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(bytesReadPointer);
            }

            return destination.ToArray();
        }
        finally
        {
            if (stream != null)
                Marshal.FinalReleaseComObject(stream);

            Marshal.FinalReleaseComObject(resources);
        }
    }


    public PortableDeviceObject? FindChild(string parentObjectId, string name)
    {
        return GetChildren(parentObjectId)
            .FirstOrDefault(x =>
                string.Equals(
                    x.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
    }

    public PortableDeviceObject? FindFolder(params string[] path)
    {
        foreach (var root in GetRootObjects())
        {
            var folder = FindFolderFrom(root.Id, path);

            if (folder != null)
                return folder;
        }

        return null;
    }


    public PortableDeviceObject? FindFolderFrom(
        string parentObjectId,
        params string[] path)
    {
        if (path == null || path.Length == 0)
            return null;

        IReadOnlyList<PortableDeviceObject> currentLevel =
            GetChildren(parentObjectId);

        PortableDeviceObject? current = null;

        Debug.WriteLine($"Parent = {parentObjectId}");

        foreach (var folderName in path)
        {
            Debug.WriteLine($"Cerco: {folderName}");

            current = currentLevel.FirstOrDefault(x =>
                x.IsFolder &&
                string.Equals(
                    x.Name,
                    folderName,
                    StringComparison.OrdinalIgnoreCase));

            if (current == null)
            {
                Debug.WriteLine($"NON TROVATA: {folderName}");
                return null;
            }

            Debug.WriteLine($"TROVATA: {current.Name} [{current.Id}]");

            currentLevel = GetChildren(current.Id);
        }

        return current;
    }
    private string GetObjectName(string objectId)
    {
        if (_properties == null)
            throw new InvalidOperationException();

        var keys = (IPortableDeviceKeyCollection)new PortableDeviceKeyCollection();

        keys.Add(PortableDeviceApi.WPD_OBJECT_NAME);

        var values = _properties.GetValues(objectId, keys);

        return values.GetStringValue(PortableDeviceApi.WPD_OBJECT_NAME);
    }

    private Guid GetContentType(string objectId)
    {
        if (_properties == null)
            throw new InvalidOperationException();

        var keys = (IPortableDeviceKeyCollection)new PortableDeviceKeyCollection();

        keys.Add(PortableDeviceApi.WPD_OBJECT_CONTENT_TYPE);

        var values = _properties.GetValues(objectId, keys);

        return values.GetGuidValue(PortableDeviceApi.WPD_OBJECT_CONTENT_TYPE);
    }

    private ulong GetObjectSize(string objectId)
    {
        if (_properties == null)
            throw new InvalidOperationException();

        try
        {
            var keys = (IPortableDeviceKeyCollection)new PortableDeviceKeyCollection();

            keys.Add(PortableDeviceApi.WPD_OBJECT_SIZE);

            var values = _properties.GetValues(objectId, keys);

            return values.GetUnsignedLargeIntegerValue(PortableDeviceApi.WPD_OBJECT_SIZE);
        }
        catch (COMException ex)
        {
            Debug.WriteLine(
                $"GetObjectSize({objectId}) -> {ex.HResult:X8}");

            return 0;
        }
    }

    private PortableDeviceObject CreateObject(string objectId, string parentObjectId)
    {
        var contentType = GetContentType(objectId);

        bool isFolder =
            contentType == PortableDeviceApi.WPD_CONTENT_TYPE_FOLDER;

        ulong size = 0;

        if (!isFolder)
        {
            size = GetObjectSize(objectId);
        }

        return new PortableDeviceObject
        {
            Id = objectId,
            ParentId = parentObjectId,

            Name = GetObjectName(objectId),

            IsFolder = isFolder,
            IsFile = !isFolder,

            Size = size
        };
    }
}
