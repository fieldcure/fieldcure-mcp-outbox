using System.Runtime.InteropServices;
using System.Text;

namespace FieldCure.Mcp.Outbox.Configuration;

/// <summary>
/// Manages secrets using the Windows Credential Manager (advapi32).
/// </summary>
public class CredentialManager
{
    const int CredTypeGeneric = 1;
    const int CredPersistLocalMachine = 2;

    /// <summary>
    /// Stores a secret in Windows Credential Manager.
    /// </summary>
    /// <param name="credentialName">The credential target name.</param>
    /// <param name="secret">The secret value to store.</param>
    public void Store(string credentialName, string secret)
    {
        var secretBytes = Encoding.Unicode.GetBytes(secret);

        var credential = new CREDENTIAL
        {
            Type = CredTypeGeneric,
            TargetName = credentialName,
            CredentialBlobSize = (uint)secretBytes.Length,
            CredentialBlob = Marshal.AllocHGlobal(secretBytes.Length),
            Persist = CredPersistLocalMachine,
        };

        try
        {
            Marshal.Copy(secretBytes, 0, credential.CredentialBlob, secretBytes.Length);

            if (!CredWrite(ref credential, 0))
                throw new InvalidOperationException(
                    $"Failed to write credential '{credentialName}'. Error: {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            Marshal.FreeHGlobal(credential.CredentialBlob);
        }
    }

    /// <summary>
    /// Retrieves a secret from Windows Credential Manager.
    /// </summary>
    /// <param name="credentialName">The credential target name.</param>
    public string? Retrieve(string credentialName)
    {
        if (!CredRead(credentialName, CredTypeGeneric, 0, out var credentialPtr))
            return null;

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                return null;

            var secretBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, secretBytes, 0, (int)credential.CredentialBlobSize);
            return Encoding.Unicode.GetString(secretBytes);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    /// <summary>
    /// Deletes a credential from Windows Credential Manager.
    /// </summary>
    /// <param name="credentialName">The credential target name.</param>
    public void Delete(string credentialName)
    {
        CredDelete(credentialName, CredTypeGeneric, 0);
    }

    #pragma warning disable SYSLIB1054 // Use LibraryImportAttribute — requires partial class and manual marshalling
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern void CredFree(IntPtr buffer);
    #pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENTIAL
    {
        public uint Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }
}
