using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NaxiBootstrap;

internal sealed class PcScanResult
{
    public string Cpu { get; init; } = "Unknown";
    public int Threads { get; init; }
    public string Gpu { get; init; } = "Unknown";
    public double RamGb { get; init; }
    public int FpsFrom { get; init; }
    public int FpsTo { get; init; }
}

internal static class PcScan
{
    public static PcScanResult Run()
    {
        var cpu = Clean(ReadCpuName());
        var threads = Environment.ProcessorCount;
        var gpu = ReadGpuName();
        var ram = ReadRamGb();

        var score = 0;
        if (threads >= 12) score += 2;
        else if (threads >= 6) score += 1;

        if (ram >= 16) score += 1;
        else if (ram > 0 && ram < 8) score -= 1;

        var g = gpu.ToLowerInvariant();
        var integrated =
            (g.Contains("intel") && (g.Contains("uhd") || g.Contains("hd graphics") || g.Contains("iris"))) ||
            g.Contains("radeon(tm) graphics") || g.Contains("radeon (tm) graphics") ||
            g.Contains("basic display") || g.Contains("basic render");
        var strong = g.Contains("rtx") || g.Contains("rx 6") || g.Contains("rx 7") || g.Contains("rx 9") || g.Contains("arc a");
        if (strong) score += 2;
        else if (!integrated) score += 1;

        var tier = score <= 0 ? 0 : score <= 2 ? 1 : score <= 3 ? 2 : 3;
        // With the aggressive performance kit (quality 1 + shadows/postFx off +
        // LOD reduction + voxelizer pause + GPU light culling) the realistic gain
        // is noticeably larger than conservative estimates.
        var (from, to) = tier switch { 0 => (40, 70), 1 => (70, 120), 2 => (120, 200), _ => (200, 320) };

        return new PcScanResult { Cpu = cpu, Threads = threads, Gpu = gpu, RamGb = ram, FpsFrom = from, FpsTo = to };
    }

    private static string Clean(string s)
        => string.Join(" ", s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string ReadCpuName()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (k?.GetValue("ProcessorNameString") is string s && s.Trim().Length > 0) return s;
        }
        catch { }
        return "Unknown";
    }

    private static (string Name, double VramGb) ReadGpuEntry(string subKey)
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(subKey);
            if (k?.GetValue("DriverDesc") is not string desc || desc.Trim().Length == 0) return ("", 0);
            double vram = 0;
            try
            {
                var raw = k.GetValue("HardwareInformation.qwMemorySize");
                if (raw is long l) vram = l;
                else if (raw is int i) vram = i;
                else if (raw is byte[] b && b.Length >= 8) vram = BitConverter.ToInt64(b, 0);
            }
            catch { }
            return (desc.Trim(), vram / 1073741824.0);
        }
        catch { return ("", 0); }
    }

    private static string ReadGpuName()
    {
        const string cls = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        string best = "";
        var bestScore = -1.0;
        for (var i = 0; i < 10; i++)
        {
            var (name, vram) = ReadGpuEntry($"{cls}\\{i:D4}");
            if (name.Length == 0) continue;
            var low = name.ToLowerInvariant();
            if (low.Contains("basic") || low.Contains("virtual") || low.Contains("hyper-v")) continue;
            var score = vram;
            if (!low.Contains("intel")) score += 0.5; // prefer discrete GPU over integrated
            if (score > bestScore) { bestScore = score; best = name; }
        }
        return best.Length > 0 ? best : "Unknown";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx b);

    private static double ReadRamGb()
    {
        try
        {
            var b = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            return GlobalMemoryStatusEx(ref b) ? Math.Round(b.ullTotalPhys / 1073741824.0, 1) : 0;
        }
        catch { return 0; }
    }
}
