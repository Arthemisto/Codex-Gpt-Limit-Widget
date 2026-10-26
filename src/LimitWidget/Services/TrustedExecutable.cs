using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LimitWidget.Services;

/// <summary>Holds the checked file against writes/deletion until Process.Start has opened it.</summary>
internal sealed class TrustedExecutable : IDisposable
{
    readonly FileStream file;
    public string Path { get; }
    TrustedExecutable(string path, FileStream file) { Path = path; this.file = file; }

    public static TrustedExecutable Open(string path)
    {
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            Verify(path, file.SafeFileHandle.DangerousGetHandle());
            return new TrustedExecutable(path, file);
        }
        catch (CryptographicException error)
        {
            file.Dispose();
            throw new IOException("Codex publisher could not be verified. Repair or update the official Codex installation.", error);
        }
        catch { file.Dispose(); throw; }
    }

    static void Verify(string path, IntPtr handle)
    {
        IntPtr name = Marshal.StringToCoTaskMemUni(path);
        IntPtr infoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
        var info = new FileInfo { Size = (uint)Marshal.SizeOf<FileInfo>(), Name = name, Handle = handle };
        Marshal.StructureToPtr(info, infoPointer, false);
        var data = new TrustData
        {
            Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Revocation = 1, Choice = 1,
            File = infoPointer, StateAction = 1, Flags = 0x80 // Check revocation, excluding the root.
        };
        Guid action = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            int status = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            if (status != 0)
                throw new IOException("Codex signature is missing, invalid, or not trusted by Windows. Repair or update the official Codex installation.");
            IntPtr provider = WTHelperProvDataFromStateData(data.State);
            IntPtr signerPointer = provider == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            if (signerPointer == IntPtr.Zero) throw new CryptographicException("Verified signer is missing.");
            var signer = Marshal.PtrToStructure<ProviderSigner>(signerPointer);
            if (signer.Count == 0 || signer.Chain == IntPtr.Zero) throw new CryptographicException("Verified certificate is missing.");
            var certificate = Marshal.PtrToStructure<ProviderCertificate>(signer.Chain);
            var context = Marshal.PtrToStructure<CertificateContext>(certificate.Certificate);
            var bytes = new byte[checked((int)context.Length)]; Marshal.Copy(context.Encoded, bytes, 0, bytes.Length);
            using var signingCertificate = X509CertificateLoader.LoadCertificate(bytes);
            if (signingCertificate.GetNameInfo(X509NameType.SimpleName, false) != "OpenAI OpCo, LLC")
                throw new IOException("Codex is not signed by the approved OpenAI publisher. Install the official Codex desktop app.");
        }
        finally
        {
            data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.FreeHGlobal(infoPointer); Marshal.FreeCoTaskMem(name);
        }
    }

    public void Dispose() => file.Dispose();

    [StructLayout(LayoutKind.Sequential)] struct FileInfo { public uint Size; public IntPtr Name, Handle, Subject; }
    [StructLayout(LayoutKind.Sequential)] struct TrustData
    {
        public uint Size;
        public IntPtr Policy, Sip;
        public uint Ui, Revocation, Choice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr State, Url;
        public uint Flags, Context;
        public IntPtr SignatureSettings;
    }
    // Prefixes of CRYPT_PROVIDER_SGNR and CRYPT_PROVIDER_CERT; read only the leading fields.
    [StructLayout(LayoutKind.Sequential)] struct ProviderSigner
    {
        public uint Size;
        public System.Runtime.InteropServices.ComTypes.FILETIME VerifiedAt;
        public uint Count;
        public IntPtr Chain;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProviderCertificate { public uint Size; public IntPtr Certificate; }
    [StructLayout(LayoutKind.Sequential)] struct CertificateContext
    {
        public uint Encoding;
        public IntPtr Encoded;
        public uint Length;
        public IntPtr Info, Store;
    }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint index, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterIndex);
}
