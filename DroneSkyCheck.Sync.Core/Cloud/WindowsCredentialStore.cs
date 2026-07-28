using System.Runtime.InteropServices;
using System.Text;

namespace DroneSkyCheck.Sync.Core.Cloud;

internal static class WindowsCredentialStore
{
    private const int CredentialTypeGeneric = 1;
    private const int CredentialPersistLocalMachine = 2;

    public static void Write(
        string targetName,
        string value)
    {
        var credentialBytes = Encoding.Unicode.GetBytes(value);

        var credential = new NativeCredential
        {
            Type = CredentialTypeGeneric,
            TargetName = targetName,
            CredentialBlobSize = credentialBytes.Length,
            Persist = CredentialPersistLocalMachine,
            UserName = Environment.UserName
        };

        var credentialBlob =
            Marshal.AllocCoTaskMem(credentialBytes.Length);

        try
        {
            Marshal.Copy(
                credentialBytes,
                0,
                credentialBlob,
                credentialBytes.Length);

            credential.CredentialBlob = credentialBlob;

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"Windows Credential Manager write failed: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(credentialBlob);
        }
    }

    public static string? Read(string targetName)
    {
        if (!CredRead(
            targetName,
            CredentialTypeGeneric,
            0,
            out var credentialPointer))
        {
            return null;
        }

        try
        {
            var credential =
                Marshal.PtrToStructure<NativeCredential>(
                    credentialPointer);

            if (credential.CredentialBlob == IntPtr.Zero ||
                credential.CredentialBlobSize == 0)
            {
                return string.Empty;
            }

            var credentialBytes =
                new byte[credential.CredentialBlobSize];

            Marshal.Copy(
                credential.CredentialBlob,
                credentialBytes,
                0,
                credentialBytes.Length);

            return Encoding.Unicode.GetString(credentialBytes);
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public static void Delete(string targetName)
    {
        CredDelete(targetName, CredentialTypeGeneric, 0);
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(
        ref NativeCredential userCredential,
        uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(
        string target,
        int type,
        int reservedFlag,
        out IntPtr credentialPointer);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(
        string target,
        int type,
        int flags);

    [DllImport("Advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;

        public int Type;

        public string TargetName;

        public string? Comment;

        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;

        public int CredentialBlobSize;

        public IntPtr CredentialBlob;

        public int Persist;

        public int AttributeCount;

        public IntPtr Attributes;

        public string? TargetAlias;

        public string UserName;
    }
}

