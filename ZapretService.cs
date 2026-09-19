using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Principal;
using System.Threading;

namespace NaxiBootstrap;

// Bundled DPI bypass (Zapret / winws) so Roblox and Discord keep working on ISPs
// that throttle or block them. Binaries are embedded in the exe and extracted to
// %LOCALAPPDATA%\NaxiBootstrap\zapret on first use. Loading the WinDivert driver
// requires administrator rights, so the first start shows a UAC prompt.
internal static class ZapretService
{
    public const string BundleVersion = "1.10.2.1";
    private const string ResourceName = "NaxiBootstrap.zapret.zip";

    private static string RootDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaxiBootstrap", "zapret");
    private static string BinDir => Path.Combine(RootDir, "bin");
    private static string ListsDir => Path.Combine(RootDir, "lists");
    private static string WinwsPath => Path.Combine(BinDir, "winws.exe");

    public static bool IsRunning()
    {
        try { return Process.GetProcessesByName("winws").Length > 0; }
        catch { return false; }
    }

    public static string[] PresetNames() => ZapretPresetTable.All.Select(p => p.Name).ToArray();

    public static string ArgsFor(string preset)
    {
        string template = ZapretPresetTable.All[0].Args;
        foreach (var (name, args) in ZapretPresetTable.All)
        {
            if (string.Equals(name, preset, StringComparison.OrdinalIgnoreCase)) { template = args; break; }
        }
        return template
            .Replace("{BIN}", BinDir + "\\")
            .Replace("{LISTS}", ListsDir + "\\")
            .Replace("{ROOT}", RootDir + "\\");
    }

    public static bool Start(string preset)
    {
        try { EnsureFiles(); } catch { return false; }
        if (IsRunning()) return true;

        bool isAdmin = false;
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            isAdmin = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { }

        // cmd wrapper keeps winws attached to a hidden console. Without admin the
        // shell "runas" verb triggers the UAC prompt once; a decline throws -> false.
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c cd /d \"" + BinDir + "\" && winws.exe " + ArgsFor(preset),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };
        if (!isAdmin) psi.Verb = "runas";

        try { Process.Start(psi); }
        catch { return false; }

        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(250);
            if (IsRunning()) return true;
        }
        return false;
    }

    public static void Restart(string preset)
    {
        Stop();
        if (IsRunning()) return; // could not stop (UAC declined) — do not stack a second instance
        Start(preset);
    }

    public static void Stop()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("winws"))
            {
                try { p.Kill(true); } catch { }
                try { p.Dispose(); } catch { }
            }
        }
        catch { }

        // winws runs elevated (driver requirement), so a non-elevated kill cannot stop
        // it — fall back to an elevated taskkill (one UAC prompt).
        if (IsRunning())
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c taskkill /f /im winws.exe",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                })?.WaitForExit(8000);
            }
            catch { }
        }
    }

    private static void EnsureFiles()
    {
        Directory.CreateDirectory(RootDir);
        var marker = Path.Combine(RootDir, "version.txt");
        if (File.Exists(marker) && File.ReadAllText(marker).Trim() == BundleVersion && File.Exists(WinwsPath))
            return;

        if (IsRunning()) Stop();
        try { Directory.Delete(RootDir, true); } catch { }
        Directory.CreateDirectory(RootDir);

        using var s = typeof(ZapretService).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Zapret bundle resource is missing");
        using var zip = new ZipArchive(s, ZipArchiveMode.Read);
        zip.ExtractToDirectory(RootDir, overwriteFiles: true);
        File.WriteAllText(marker, BundleVersion);
    }
}
