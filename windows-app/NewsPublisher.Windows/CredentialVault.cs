using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace KaragoPublisher.Windows;

public static class CredentialVault
{
    private const uint Generic = 1, PersistLocalMachine = 2;
    private static string Target(string site) => $"KARAGO.NewsPublisher.Connector.{site}";

    public static void Save(string site, string apiKey)
    {
        ValidateSite(site);
        apiKey = (apiKey ?? "").Trim();
        if (!Regex.IsMatch(apiKey, "^kdia_[a-f0-9]{16}_[A-Za-z0-9]{48}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Το KARAGO Connector API key δεν έχει έγκυρη μορφή.");
        var bytes = Encoding.Unicode.GetBytes(apiKey);
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential {
                Type = Generic, TargetName = Target(site), CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob, Persist = PersistLocalMachine, UserName = "KARAGO Diafaneia Connector"
            };
            if (!CredWrite(ref credential, 0)) throw new InvalidOperationException("Αποτυχία αποθήκευσης στο Windows Credential Manager.");
        }
        finally {
            if (bytes.Length > 0) Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeCoTaskMem(blob);
        }
    }

    public static ConnectorCredentials Load(string site)
    {
        ValidateSite(site);
        if (!CredRead(Target(site), Generic, 0, out var pointer)) return new ConnectorCredentials("");
        try {
            var value = Marshal.PtrToStructure<NativeCredential>(pointer);
            var apiKey = value.CredentialBlob == IntPtr.Zero ? "" : Marshal.PtrToStringUni(value.CredentialBlob, (int)value.CredentialBlobSize / 2) ?? "";
            return new ConnectorCredentials(apiKey);
        }
        finally { CredFree(pointer); }
    }

    private static void ValidateSite(string site)
    {
        if (site != "diafaneia") throw new InvalidOperationException("Άγνωστο site credentials.");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential {
        public uint Flags, Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }
    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredWrite(ref NativeCredential credential, uint flags);
    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("Advapi32.dll", SetLastError = true)] private static extern void CredFree(IntPtr buffer);
}
