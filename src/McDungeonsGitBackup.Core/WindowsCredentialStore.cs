using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace McDungeonsGitBackup.Core;

public sealed class WindowsCredentialStore : ICredentialStore
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const string TargetName = "McDungeonsGitBackup:GitHubToken";

    public string? ReadToken()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        if (!CredRead(TargetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            return Marshal.PtrToStringUni(
                credential.CredentialBlob,
                (int)credential.CredentialBlobSize / Encoding.Unicode.GetByteCount("a"));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void SaveToken(string token)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Credential Manager is only available on Windows.");
        }

        var tokenBytes = Encoding.Unicode.GetBytes(token);
        var blobPointer = Marshal.AllocCoTaskMem(tokenBytes.Length);

        try
        {
            Marshal.Copy(tokenBytes, 0, blobPointer, tokenBytes.Length);

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = TargetName,
                CredentialBlobSize = (uint)tokenBytes.Length,
                CredentialBlob = blobPointer,
                Persist = CredentialPersistLocalMachine,
                UserName = Environment.UserName
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to save token in Windows Credential Manager.");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(blobPointer);
        }
    }

    public void DeleteToken()
    {
        if (OperatingSystem.IsWindows())
        {
            CredDelete(TargetName, CredentialTypeGeneric, 0);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential userCredential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}
