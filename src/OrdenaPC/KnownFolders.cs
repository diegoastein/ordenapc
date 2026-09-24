using System.Runtime.InteropServices;

namespace OrdenaPC;

static class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    /// <summary>Descargas no está en SpecialFolder; se pide a Windows por si el usuario la movió.</summary>
    public static string Downloads
    {
        get
        {
            try
            {
                if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out var ptr) == 0)
                {
                    try { return Marshal.PtrToStringUni(ptr)!; }
                    finally { Marshal.FreeCoTaskMem(ptr); }
                }
            }
            catch (Exception) { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
