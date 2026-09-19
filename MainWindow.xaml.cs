using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;
using Image = System.Windows.Controls.Image;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using FontFamily = System.Windows.Media.FontFamily;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace NaxiBootstrap;

internal class AppConfig
{
    public bool FpsUnlock { get; set; } = true;
    public int FpsLimit { get; set; } = 240;
    public bool NoShadows { get; set; }
    public bool PerfMode { get; set; }
    public bool FutureLighting { get; set; }
    public bool NoPostFx { get; set; }
    public bool NoTelemetry { get; set; } = true;
    public bool HideUpdateLog { get; set; }
    public bool Autostart { get; set; }
    public bool Notifications { get; set; } = true;
    public bool OpenRobloxLinks { get; set; } = true;
    public bool Zapret { get; set; }
    public string ZapretPreset { get; set; } = "general";
    public string RobloxVersion { get; set; } = "";
    public string FontName { get; set; } = "Segoe UI";
    public string BackgroundUrl { get; set; } = "";
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "Blue";
    public long LastUsedAccountId { get; set; }
    public string NormalCursorPath { get; set; } = "";
    public string PointingCursorPath { get; set; } = "";
    public string ShiftCursorPath { get; set; } = "";
    public string IBeamCursorPath { get; set; } = "";
    public string EmoteCircleBgPath { get; set; } = "";
    public string EmoteSegmentedPath { get; set; } = "";
    public string EmoteGradientPath { get; set; } = "";
    public string EmoteSelectedLinePath { get; set; } = "";
    public string SkyFolderPath { get; set; } = "";
    public bool DiscordRpcEnabled { get; set; } = false;
    public string DiscordRpcClientId { get; set; } = "1542960817199128596";
    public string DiscordRpcDetails { get; set; } = "Playing Roblox";
    public string DiscordRpcState { get; set; } = "via Naxi Bootstrap";
    public string DiscordRpcLargeImage { get; set; } = "";
    public string DiscordRpcLargeText { get; set; } = "Naxi Bootstrap";
    public string DiscordRpcSmallImage { get; set; } = "";
    public string DiscordRpcSmallText { get; set; } = "";
    public string DiscordRpcButton1Label { get; set; } = "";
    public string DiscordRpcButton1Url { get; set; } = "";
    public string DiscordRpcButton2Label { get; set; } = "";
    public string DiscordRpcButton2Url { get; set; } = "";
    public bool DiscordRpcShowElapsed { get; set; } = true;
    public string Language { get; set; } = "En";
    public bool IsPro { get; set; }
    public string? ProExpiresAt { get; set; }
    public bool IsMax { get; set; }
    public string? MaxExpiresAt { get; set; }
    public bool AutoRejoin { get; set; }
    public string GameHubSort { get; set; } = "players";
    public bool QuickHotkey { get; set; } = true;
    public bool RpcAutoGame { get; set; }
    public Dictionary<string, long> PlaySeconds { get; set; } = new Dictionary<string, long>();
    public List<long> RecentPlaceIds { get; set; } = new List<long>();
    public Dictionary<string, GameProfile> GameProfiles { get; set; } = new Dictionary<string, GameProfile>();
    public List<long> FavoritePlaceIds { get; set; } = new List<long>();
    public List<GameFolder> GameFolders { get; set; } = new List<GameFolder>();
}

internal sealed class GameFolder
{
    public string Name { get; set; } = "";
    public List<long> PlaceIds { get; set; } = new List<long>();
}

internal class GameProfile
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool FpsUnlock { get; set; } = true;
    public int FpsLimit { get; set; } = 144;
    public bool NoShadows { get; set; }
    public bool PerfMode { get; set; }
    public bool FutureLighting { get; set; }
    public bool NoPostFx { get; set; }
    public bool NoTelemetry { get; set; } = true;
    public string CursorNormal { get; set; } = "";
    public string CursorPointing { get; set; } = "";
    public string CursorShiftLock { get; set; } = "";
}

internal static class RobloxLauncher
{
    private static readonly string RobloxRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");
    private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    private static readonly string[] TelemetryFlags =
    {
        "FFlagDebugDisableTelemetryEphemeralCounter",
        "FFlagDebugDisableTelemetryEphemeralStat",
        "FFlagDebugDisableTelemetryEventIngest",
        "FFlagDebugDisableTelemetryPoint",
        "FFlagDebugDisableTelemetryV2Counter",
        "FFlagDebugDisableTelemetryV2Event",
        "FFlagDebugDisableTelemetryV2Stat"
    };

    private static readonly HttpClient Http = new();

    static RobloxLauncher()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "NaxiBootstrap");
    }

    public static async Task<(long UniverseId, string Name)?> GetPlaceInfoAsync(string url)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(url);
            var m = System.Text.RegularExpressions.Regex.Match(decoded, @"placeid=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // placeId= from the official roblox:// deep links matches the same
            // regex; placi: is kept only for legacy launch URLs.
            if (!m.Success) m = System.Text.RegularExpressions.Regex.Match(decoded, @"placi:(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var placeId = m.Groups[1].Value;

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(4));
            var universeJson = await Http.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{placeId}/universe", cts.Token);
            using var u = JsonDocument.Parse(universeJson);
            var universeId = u.RootElement.GetProperty("universeId").GetInt64();

            var gamesJson = await Http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={universeId}", cts.Token);
            using var g = JsonDocument.Parse(gamesJson);
            var name = g.RootElement.GetProperty("data")[0].GetProperty("name").GetString() ?? "";
            return (universeId, name);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> GetPlaceNameAsync(string url)
    {
        var info = await GetPlaceInfoAsync(url);
        return info?.Name;
    }

    public static AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFile))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFile)) ?? new AppConfig();
        }
        catch { }
        return new AppConfig();
    }

    public static void SaveConfig(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static List<string> FindPlayerFolders()
    {
        var versions = Path.Combine(RobloxRoot, "Versions");
        if (!Directory.Exists(versions)) return new List<string>();
        var players = Directory.GetDirectories(versions)
            .Where(d => File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe")))
            .OrderByDescending(Directory.GetLastWriteTime)
            .ToList();
        if (players.Count > 0) return players;
        return Directory.GetDirectories(versions).OrderByDescending(Directory.GetLastWriteTime).Take(1).ToList();
    }

    public static string? FindStudioExe()
    {
        var candidates = new List<string>();
        var searchRoots = new[]
        {
            Path.Combine(RobloxRoot, "Versions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bloxstrap", "Versions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Roblox", "Versions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Roblox", "Versions"),
        };
        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var exe = Path.Combine(dir, "RobloxStudioBeta.exe");
                    if (File.Exists(exe)) candidates.Add(exe);
                }
            } catch { }
        }
        if (candidates.Count > 0)
            return candidates.OrderByDescending(File.GetLastWriteTime).First();
        // fallback: deep search in LocalAppData\Roblox
        try
        {
            var deep = Directory.GetFiles(RobloxRoot, "RobloxStudioBeta.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (deep != null) return deep;
        } catch { }
        return null;
    }

    public static Dictionary<string, string> BuildFlags(AppConfig config)
    {
        var flags = new Dictionary<string, string>();
        if (config.FpsUnlock) flags["DFIntTaskSchedulerTargetFps"] = config.FpsLimit.ToString();
        if (config.NoShadows) flags["FIntRenderShadowIntensity"] = "0";
        if (config.PerfMode)
        {
            // Full "potato" kit — every known real FPS flag applied together, so the
            // optimization preset gives a much bigger frame gain (50–60+ FPS).
            flags["DFIntDebugFRMQualityLevelOverride"] = "1";  // lowest render quality level
            flags["DFFlagDebugPauseVoxelizer"] = "True";       // pause terrain voxelizer
            flags["FFlagFastGPULightCulling3"] = "True";       // cheaper GPU light culling
            flags["FIntDebugForceMSAASamples"] = "0";          // disable anti-aliasing
            flags["DFFlagTextureQualityOverrideEnabled"] = "True";
            flags["DFIntTextureQualityOverride"] = "0";        // lowest texture quality
            flags["FIntTerrainArraySliceSize"] = "4";          // low-detail terrain
            flags["FIntFRMMinGrassDistance"] = "0";            // remove grass and foliage
            flags["FIntFRMMaxGrassDistance"] = "0";
            flags["FIntRenderGrassDetailStrands"] = "0";
            flags["FIntRenderGrassHeightScaler"] = "0";
            flags["FIntRenderLocalLightUpdatesMin"] = "8";     // cheaper dynamic lights
            flags["FIntRenderLocalLightUpdatesMax"] = "8";
            flags["FIntRenderLocalLightFadeInMs"] = "0";
        }
        if (config.FutureLighting) flags["FFlagDebugForceFutureIsBrightPhase3"] = "True";
        if (config.NoPostFx) flags["FFlagDisablePostFx"] = "True";
        if (config.NoTelemetry)
            foreach (var f in TelemetryFlags)
                flags[f] = "False";
        return flags;
    }

    public static string ApplyFlags(AppConfig config)
    {
        var folders = FindPlayerFolders();
        if (folders.Count == 0) return "Roblox not found on this PC";

        var flags = BuildFlags(config);
        try
        {
            foreach (var folder in folders)
            {
                var dir = Path.Combine(folder, "ClientSettings");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ClientAppSettings.json"), JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true }));
            }
            return flags.Count == 0
                ? "All flags cleared"
                : $"Applied {flags.Count} flag(s) to {Path.GetFileName(folders[0])}" + (folders.Count > 1 ? $" +{folders.Count - 1} more" : "");
        }
        catch
        {
            return "Failed to write ClientAppSettings.json";
        }
    }

    public static string ResetFlags()
    {
        var folders = FindPlayerFolders();
        if (folders.Count == 0) return "Roblox not found on this PC";
        try
        {
            foreach (var folder in folders)
            {
                var dir = Path.Combine(folder, "ClientSettings");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ClientAppSettings.json"), "{}");
            }
            return "All flags reset";
        }
        catch
        {
            return "Failed to reset flags";
        }
    }

    public static void CaptureGameProfile(AppConfig config, long? universeId, string? gameName)
    {
        if (universeId == null) return;
        if (config.GameProfiles == null) config.GameProfiles = new Dictionary<string, GameProfile>();
        var key = universeId.Value.ToString();
        if (config.GameProfiles.ContainsKey(key)) return;
        config.GameProfiles[key] = new GameProfile
        {
            Name = string.IsNullOrWhiteSpace(gameName) ? "Game " + key : gameName!,
            Enabled = true,
            FpsUnlock = config.FpsUnlock,
            FpsLimit = config.FpsLimit,
            NoShadows = config.NoShadows,
            PerfMode = config.PerfMode,
            FutureLighting = config.FutureLighting,
            NoPostFx = config.NoPostFx,
            NoTelemetry = config.NoTelemetry
        };
        SaveConfig(config);
    }

    public static void ApplyFlagsForGame(AppConfig config, long? universeId)
    {
        GameProfile? p = null;
        if (universeId != null && config.GameProfiles != null)
            config.GameProfiles.TryGetValue(universeId.Value.ToString(), out p);

        if (p != null && p.Enabled)
        {
            var flags = new Dictionary<string, string>();
            if (p.FpsUnlock) flags["DFIntTaskSchedulerTargetFps"] = p.FpsLimit.ToString();
            if (p.NoShadows) flags["FIntRenderShadowIntensity"] = "0";
            if (p.PerfMode)
            {
                flags["DFIntDebugFRMQualityLevelOverride"] = "1";
                // Same full "potato" kit as the global preset.
                flags["DFFlagDebugPauseVoxelizer"] = "True";
                flags["FFlagFastGPULightCulling3"] = "True";
                flags["FIntDebugForceMSAASamples"] = "0";
                flags["DFFlagTextureQualityOverrideEnabled"] = "True";
                flags["DFIntTextureQualityOverride"] = "0";
                flags["FIntTerrainArraySliceSize"] = "4";
                flags["FIntFRMMinGrassDistance"] = "0";
                flags["FIntFRMMaxGrassDistance"] = "0";
                flags["FIntRenderGrassDetailStrands"] = "0";
                flags["FIntRenderGrassHeightScaler"] = "0";
                flags["FIntRenderLocalLightUpdatesMin"] = "8";
                flags["FIntRenderLocalLightUpdatesMax"] = "8";
                flags["FIntRenderLocalLightFadeInMs"] = "0";
            }
            if (p.FutureLighting) flags["FFlagDebugForceFutureIsBrightPhase3"] = "True";
            if (p.NoPostFx) flags["FFlagDisablePostFx"] = "True";
            if (p.NoTelemetry)
                foreach (var f in TelemetryFlags)
                    flags[f] = "False";
            WriteFlagsToVersions(flags);
        }
        else
        {
            ApplyFlags(config);
        }

        string? cn = p != null && p.Enabled && !string.IsNullOrWhiteSpace(p.CursorNormal) ? p.CursorNormal : NullIfEmpty(config.NormalCursorPath);
        string? cp = p != null && p.Enabled && !string.IsNullOrWhiteSpace(p.CursorPointing) ? p.CursorPointing : NullIfEmpty(config.PointingCursorPath);
        string? cs = p != null && p.Enabled && !string.IsNullOrWhiteSpace(p.CursorShiftLock) ? p.CursorShiftLock : NullIfEmpty(config.ShiftCursorPath);
        MainWindow.ApplyCursorSetForLaunch(config, cn, cp, cs);
    }

    static string? NullIfEmpty(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    static void WriteFlagsToVersions(Dictionary<string, string> flags)
    {
        var folders = FindPlayerFolders();
        if (folders.Count == 0) return;
        try
        {
            foreach (var folder in folders)
            {
                var dir = Path.Combine(folder, "ClientSettings");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ClientAppSettings.json"), JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { }
    }

    public static async Task LaunchFromUrlAsync(string url, AppConfig config)
    {
        try
        {
            if (config.OpenRobloxLinks) RegisterProtocol(true);

            long? universeId = null;
            string? gameName = null;
            try
            {
                var info = await GetPlaceInfoAsync(url);
                if (info.HasValue) { universeId = info.Value.UniverseId; gameName = info.Value.Name; }
            }
            catch { }

            CaptureGameProfile(config, universeId, gameName);
            ApplyFlagsForGame(config, universeId);

            var player = FindPlayerFolders().FirstOrDefault();
            if (player == null) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(player, "RobloxPlayerBeta.exe"),
                Arguments = $"\"{url}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void RegisterProtocol(bool enable)
    {
        try
        {
            const string keyPath = @"Software\Classes\roblox-player";
            using (var proto = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (enable)
                {
                    var exe = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exe)) return;
                    proto.SetValue(null, "Naxi Bootstrap");
                    proto.SetValue("URL Protocol", string.Empty);
                    using var cmd = proto.CreateSubKey(@"shell\open\command");
                    cmd.SetValue(null, $"\"{exe}\" \"%1\"");
                }
                else
                {
                    var player = FindPlayerFolders().FirstOrDefault();
                    if (player == null)
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
                        return;
                    }
                    proto.SetValue(null, "Roblox Game Client");
                    proto.SetValue("URL Protocol", string.Empty);
                    using var cmd = proto.CreateSubKey(@"shell\open\command");
                    cmd.SetValue(null, $"\"{Path.Combine(player, "RobloxPlayerBeta.exe")}\" \"%1\"");
                }
            }
        }
        catch { }
    }

    public static void SetRobloxCookie(string token)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Roblox\RobloxStudioBrowser\roblox.com");
            key.SetValue("ROBLOSECURITY", $".ROBLOSECURITY|{token}|roblox.com|/|4102444800");
        }
        catch { }
    }

    public static string SetModernCookie(string token)
    {
        var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "play_debug.log");
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "LocalStorage", "RobloxCookies.dat");
            LogDebug(logFile, $"SetModernCookie: file exists={File.Exists(file)}");
            if (!File.Exists(file)) return "dat file not found";

            var json = File.ReadAllText(file);
            LogDebug(logFile, $"SetModernCookie: json length={json.Length}");
            using var doc = JsonDocument.Parse(json);
            var dataB64 = doc.RootElement.GetProperty("CookiesData").GetString();
            if (dataB64 == null) return "no CookiesData";
            LogDebug(logFile, $"SetModernCookie: dataB64 length={dataB64.Length}");

            var jar = Encoding.UTF8.GetString(
                System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromBase64String(dataB64), null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser));
            LogDebug(logFile, $"SetModernCookie: jar length={jar.Length}");

            var marker = "\t.ROBLOSECURITY\t";
            var idx = jar.IndexOf(marker, StringComparison.Ordinal);
            LogDebug(logFile, $"SetModernCookie: marker index={idx}");
            if (idx >= 0)
            {
                var start = idx + marker.Length;
                var end = jar.IndexOf("; ", start, StringComparison.Ordinal);
                if (end < 0) end = jar.Length;
                var oldLen = end - start;
                jar = jar.Substring(0, start) + token + jar.Substring(end);
                LogDebug(logFile, $"SetModernCookie: replaced token (oldLen={oldLen}, newLen={token.Length})");
            }
            else
            {
                if (jar.Length > 0 && !jar.EndsWith(";")) jar += ";";
                jar += " #HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t0\t.ROBLOSECURITY\t" + token;
                LogDebug(logFile, $"SetModernCookie: appended new cookie entry");
            }

            var protectedData = System.Security.Cryptography.ProtectedData.Protect(
                Encoding.UTF8.GetBytes(jar), null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            var newJson = JsonSerializer.Serialize(new { CookiesVersion = "1", CookiesData = Convert.ToBase64String(protectedData) });
            File.WriteAllText(file, newJson);
            LogDebug(logFile, $"SetModernCookie: SUCCESS, wrote {newJson.Length} bytes");
            return "ok";
        }
        catch (Exception ex)
        {
            LogDebug(logFile, $"SetModernCookie: EXCEPTION {ex.Message}");
            return "error: " + ex.Message;
        }
    }

    public static void LogDebug(string file, string msg)
    {
        try { File.AppendAllText(file, $"  {msg}\r\n"); } catch { }
    }

    public static long ExtractUid(string token)
    {
        try
        {
            var parts = token.Split('|');
            var last = parts[^1].Replace('-', '+').Replace('_', '/');
            switch (last.Length % 4) { case 2: last += "=="; break; case 3: last += "="; break; }
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(last));
            var m = System.Text.RegularExpressions.Regex.Match(decoded, "uid\x12([\x00-\x1f])(\\d+)");
            if (m.Success) return long.Parse(m.Groups[2].Value);
        }
        catch { }
        return 0;
    }

    public static (long Uid, string Token)? ReadDatToken()
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "LocalStorage", "RobloxCookies.dat");
            if (!File.Exists(file)) return null;
            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            var dataB64 = doc.RootElement.GetProperty("CookiesData").GetString();
            if (dataB64 == null) return null;
            var jar = Encoding.UTF8.GetString(
                System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromBase64String(dataB64), null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser));
            var marker = "\t.ROBLOSECURITY\t";
            var idx = jar.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            var start = idx + marker.Length;
            var end = jar.IndexOf("; ", start, StringComparison.Ordinal);
            if (end < 0) end = jar.Length;
            var token = jar.Substring(start, end - start);
            var uid = ExtractUid(token);
            if (uid == 0) return null;
            return (uid, token);
        }
        catch
        {
            return null;
        }
    }

    public static void RefreshTokensFromDat()
    {
        try
        {
            var dat = ReadDatToken();
            if (dat == null) return;
            var accounts = AccountStore.Load();
            var match = accounts.FirstOrDefault(a => a.UserId == dat.Value.Uid);
            if (match == null) return;
            var current = AccountStore.Unprotect(match.ProtectedToken);
            if (current == dat.Value.Token) return;
            match.ProtectedToken = AccountStore.Protect(dat.Value.Token);
            AccountStore.Save(accounts);
        }
        catch { }
    }

    public static async Task<List<(long UserId, string Name, bool IsValid, string Error)>> ValidateAllTokensAsync()
    {
        var results = new List<(long, string, bool, string)>();
        var accounts = AccountStore.Load();
        foreach (var acc in accounts)
        {
            try
            {
                var token = AccountStore.Unprotect(acc.ProtectedToken);
                var (ok, msg, id, name) = await AccountStore.ValidateToken(token);
                results.Add((acc.UserId, acc.Name, ok, ok ? "" : msg));
            }
            catch (Exception ex)
            {
                results.Add((acc.UserId, acc.Name, false, "Decrypt error: " + ex.Message));
            }
        }
        return results;
    }
}

internal sealed class RobloxAccount
{
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
}

internal static class AccountStore
{
    private static readonly string AccountsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "accounts.json");
    private static readonly HttpClient Http = new();
    private static readonly byte[] Entropy = { 0x4E, 0x61, 0x78, 0x69, 0x42, 0x6F, 0x6F, 0x74 };

    static AccountStore()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "NaxiBootstrap");
    }

    public static List<RobloxAccount> Load()
    {
        try
        {
            if (File.Exists(AccountsFile))
                return JsonSerializer.Deserialize<List<RobloxAccount>>(File.ReadAllText(AccountsFile)) ?? new List<RobloxAccount>();
        }
        catch { }
        return new List<RobloxAccount>();
    }

    public static void Save(List<RobloxAccount> accounts)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AccountsFile)!);
            File.WriteAllText(AccountsFile, JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static string Protect(string token)
        => Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser));

    public static string Unprotect(string protectedToken)
        => Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(protectedToken), Entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser));

    public static async Task<(bool Ok, string Message, long UserId, string Name)> ValidateToken(string token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            req.Headers.Add("Cookie", $".ROBLOSECURITY={token}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var resp = await Http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return (false, "Invalid or expired token", 0, "");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
            var id = doc.RootElement.GetProperty("id").GetInt64();
            var name = doc.RootElement.GetProperty("name").GetString() ?? "";
            return (true, name, id, name);
        }
        catch
        {
            return (false, "Failed to reach Roblox API", 0, "");
        }
    }

    public static async Task<(bool Ok, string Message, long UserId)> ValidateTokenQuick(string token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            req.Headers.Add("Cookie", $".ROBLOSECURITY={token}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var resp = await Http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return (false, "expired", 0);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
            return (true, "", doc.RootElement.GetProperty("id").GetInt64());
        }
        catch
        {
            return (false, "network error", 0);
        }
    }
}

public partial class MainWindow : Window
{
    // Keep update checks tied to the assembly metadata so a release cannot
    // advertise an older version when its source constant is missed.
    private static readonly string AppVersion = $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}";

    private static readonly string GitHubRepo = Deobfuscate("ZmVhcm1haXJvLWRlc2lnbi9OYXhpQm9vdHN0cmFw");

    private const string DiscordUrl = "https://discord.gg/UkKxjgGkvB";
    private const string TelegramUrl = "https://t.me/naxistudios";
    private const string WebsiteUrl = "https://naxi-bootstrap.vercel.app/";

    private static string UpdateLogUrl => $"https://raw.githubusercontent.com/{GitHubRepo}/main/updatelog.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    private static string NewsUrl => $"https://raw.githubusercontent.com/{GitHubRepo}/main/news.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    private static string ReleasesApiUrl => $"https://api.github.com/repos/{GitHubRepo}/releases/latest";

    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "updatelog.json");
    private static readonly string UpdateZip = Path.Combine(Path.GetTempPath(), "NaxiBootstrap.update.zip");
    private static readonly string UpdateDir = Path.Combine(Path.GetTempPath(), "NaxiBootstrap.update");
    private static readonly string UpdateScript = Path.Combine(Path.GetTempPath(), "NaxiBootstrap.update.cmd");
    private static readonly string RobloxRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");
    private static readonly string RobloxLogsDir = Path.Combine(RobloxRoot, "logs");
    private static readonly string RobloxStorageDir = Path.Combine(RobloxRoot, "rbx-storage");
    private static readonly string RobloxTempDir = Path.Combine(Path.GetTempPath(), "Roblox");
    private static readonly string BgCacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "background.cache");
    private static readonly string SkyBackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "sky_backup");
    private static readonly string FontBackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "font_backup");
    private static readonly string CursorBackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "cursor_backup");
    private static readonly string CustomCursorCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "custom_cursors");
    private static readonly string EmoteBackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "emote_backup");
    private static readonly string CustomEmoteCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "custom_emotes");
    private static readonly string CustomSkyCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "custom_sky");
    private static readonly string CustomFontPath_File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "custom_font.dat");

    private static readonly string[] Fonts =
    {
        "Segoe UI", "Arial", "Calibri", "Verdana", "Georgia", "Consolas",
        "Comic Sans MS", "Impact", "Trebuchet MS", "Times New Roman"
    };

    private static readonly HttpClient Http = new();

    private Grid _home;
    private Grid _news;
    private Grid _accounts;
    private Grid _fastFlags;
    private Grid _cursors;
    private Grid _maintenance;
    private Grid _settings;
    private Grid _pro;
    private Grid _about;
    private Grid _legal;
    private StackPanel _updateList = new();
    private StackPanel _newsList = new();
    private StackPanel _legalContent = new();
    private Button _licenseTab = new();
    private Button _privacyTab = new();
    private StackPanel _homeLeft = new();
    private Border _logCard = new();
    private ScrollViewer? _fastFlagsScroll;
    private ScrollViewer? _maintenanceScroll;
    private ScrollViewer? _settingsScroll;

    private List<LogEntry> _log;
    private AppConfig _config;

    private TextBlock _versionBadge = new();
    private TextBlock _updateText = new();
    private TextBlock _percent = new();
    private ProgressBar _progress = new();
    private Button _updateBtn = new();
    private Button? _applyBtn;

    private TextBox _fpsBox = new();
    private TextBlock _flagsStatus = new();
    private TextBlock _verdictText = new();

    private Button _fontBtn = new();
    private TextBox _bgUrlBox = new();
    private TextBlock _bgStatus = new();
    private TextBlock _skyStatus = new();
    private TextBlock? _zapretStatus;
    private Button? _zapretPresetBtn;
    private System.Windows.Controls.Primitives.Popup? _presetPopup;
    private static readonly FontFamily NavIconFont = new("Segoe MDL2 Assets");
    private readonly Dictionary<Button, (Border Host, TextBlock Label, string Title)> _navLabels = new();
    private TextBlock _rbxFontStatus = new();

    private TextBox _accountTokenBox = new();
    private TextBlock _accountStatus = new();
    private StackPanel _accountsList = new();

    private TextBlock _cursorStatus = new();
    private TextBlock _normalCursorPath = new();
    private TextBlock _pointingCursorPath = new();
    private TextBlock _shiftCursorPath = new();
    private TextBlock _ibeamCursorPath = new();
    private Image _normalCursorPreview = new();
    private Image _pointingCursorPreview = new();
    private Image _shiftCursorPreview = new();
    private Image _ibeamCursorPreview = new();
    private string? _normalCursorFile;
    private string? _pointingCursorFile;
    private string? _shiftCursorFile;
    private string? _ibeamCursorFile;

    private TextBlock _emoteStatus = new();
    private Image _emoteCircleBgPreview = new();
    private Image _emoteSegmentedPreview = new();
    private Image _emoteGradientPreview = new();
    private Image _emoteSelectedLinePreview = new();
    private string? _emoteCircleBgFile;
    private string? _emoteSegmentedFile;
    private string? _emoteGradientFile;
    private string? _emoteSelectedLineFile;
    private Grid _emotes;
    private Grid _games;
    private StackPanel _gamesList = new StackPanel();
    private Ellipse? _ghStatusDot;
    private TextBlock? _ghStatusText;
    private TextBlock? _ghPingValue;
    private TextBlock? _ghPingSub;
    private TextBox? _ghSearch;
    private TextBlock? _ghSearchHint;
    private DateTime _ghLastRefresh = DateTime.MinValue;
    private int _ghLoadSeq;
    private List<HubGame> _ghCurrentGames = new List<HubGame>();    // full curated list currently loaded
    private List<HubGame> _ghDisplayGames = new List<HubGame>();  // what the grid is currently showing
    private StackPanel _ghFolderBar = new StackPanel();
    private List<HubGame>? _ghAllGames;
    private string? _ghFolderFilter; // null = All games, "__fav__" = favorites, otherwise folder name
    private string? _ghGenreFilter;  // session-only genre filter
    private long _ghLucky;           // random-game pick shown first on next render
    private System.Windows.Threading.DispatcherTimer? _ghLiveTimer;
    private bool _ghDragMoved;
    private Point _ghDragStart;
    private Button? _ghSortBtn;
    private Button? _ghGenreBtn;

    // auto-rejoin state
    private System.Windows.Threading.DispatcherTimer? _rejoinTimer;
    private Window? _rejoinWindow;
    private long _lastJoinPlaceId;
    private string _lastJoinName = "";
    private DateTime _joinStartedUtc = DateTime.MinValue;
    private bool _robloxWasRunning;
    private DateTime _robloxLastSeen = DateTime.MinValue;
    private int _rejoinTries;
    private int _playSaveCounter;

    // entitlement helpers — Naxi MAX includes everything Pro offers
    private bool HasPro => _config.IsPro || _config.IsMax;

    private TextBlock _logsSizeText = new();
    private TextBlock _storageSizeText = new();
    private TextBlock _tempSizeText = new();
    private TextBlock _cleanStatus = new();
    private TextBlock _installInfo = new();
    private TextBlock _installStatus = new();

    private bool _updating;
    private bool _switching;
    private bool _robloxNeedsReapply;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool IsDebuggerPresent();
    static string Deobfuscate(string b64) { try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64)); } catch { return b64; } }

    static MainWindow()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "NaxiBootstrap");
        try
        {
            if (System.Diagnostics.Debugger.IsAttached || System.Diagnostics.Debugger.IsLogging() || IsDebuggerPresent())
                Environment.Exit(0);
            var bad = new[] { "dnspy", "decompiler", "ildasm", "ilspy", "dotpeek", "ida", "x64dbg", "ollydbg", "cheatengine", "fiddler", "wireshark" };
            foreach (var n in bad) foreach (var p in System.Diagnostics.Process.GetProcessesByName(n)) Environment.Exit(0);
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try { var name = p.ProcessName.ToLower(); foreach (var b in bad) if (name.Contains(b)) Environment.Exit(0); } catch { }
            }
            if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") == "1") Environment.Exit(0);
        } catch { }
    }

    public MainWindow()
    {
        InitializeComponent();
        try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/icon.ico")); } catch { }

        _config = RobloxLauncher.LoadConfig();
        if (Enum.TryParse<Lang>(_config.Language, true, out var _lang)) Localization.Current = _lang;
        ThemeService.Apply(_config.Theme, _config.Accent);
        Localization.Changed += () =>
        {
            _updateList = new StackPanel();
            _newsList = new StackPanel();
            _legalContent = new StackPanel();
            _accountsList = new StackPanel();
            _gamesList = new StackPanel();
            _homeLeft = new StackPanel();
            _logCard = new Border();
            _licenseTab = new Button();
            _privacyTab = new Button();
            _home = BuildHome(); _games = BuildGameHub(); _news = BuildNews(); _accounts = BuildAccounts(); _fastFlags = BuildFastFlags(); _cursors = BuildCursors(); _emotes = BuildEmotes(); _maintenance = BuildMaintenance(); _settings = BuildSettings(); _pro = BuildPro(); _about = BuildAbout(); _legal = BuildLegal();
            RenderLog(_log); _logCard.Visibility = _config.HideUpdateLog ? Visibility.Collapsed : Visibility.Visible;
            foreach (var kv in _navLabels) kv.Value.Label.Text = Localization.T(kv.Value.Title);
            PageHost.Content = _home; SetActive(HomeNav);
            _ = CheckForUpdatesAsync();
            _ = LoadNewsAsync();
        };
        if (_config.OpenRobloxLinks) RobloxLauncher.RegisterProtocol(true);
        FontFamily = new FontFamily(string.IsNullOrWhiteSpace(_config.FontName) ? "Segoe UI" : _config.FontName);

        _log = LoadLog();
        _home = BuildHome();
        _games = BuildGameHub();
        _news = BuildNews();
        _accounts = BuildAccounts();
        _fastFlags = BuildFastFlags();
        _cursors = BuildCursors();
        _emotes = BuildEmotes();
        _maintenance = BuildMaintenance();
        _settings = BuildSettings();
        _pro = BuildPro();
        _about = BuildAbout();
        _legal = BuildLegal();

        PageHost.Content = _home;
        SetActive(HomeNav);
        RenderLog(_log);
        _logCard.Visibility = _config.HideUpdateLog ? Visibility.Collapsed : Visibility.Visible;
        RestoreBackgroundFromCache();
        ApplySavedCursors();
        ApplySavedEmotes();
        RobloxLauncher.RefreshTokensFromDat();
        try { foreach (var f in Directory.GetFiles(AppContext.BaseDirectory, "*.old")) File.Delete(f); } catch { }
        if (_config.DiscordRpcEnabled) DiscordRpcService.Start(_config);
        if (_config.Zapret && !ZapretService.IsRunning())
            Task.Run(() => { try { ZapretService.Start(_config.ZapretPreset); } catch { } });
        InitNavButtons();
        ApplyProBranding();
        _ = CheckForUpdatesAsync();
        _ = SyncProStatusAsync();

        Loaded += (_, _) =>
        {
            PlayOpenAnimation();
            try
                {
                    System.Drawing.Icon icon;
                    var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
                    if (File.Exists(iconPath))
                        icon = new System.Drawing.Icon(iconPath);
                    else
                    {
                        var asm = typeof(MainWindow).Assembly;
                        var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));
                        if (resName != null)
                        {
                            using var s = asm.GetManifestResourceStream(resName);
                            icon = s != null ? new System.Drawing.Icon(s) : System.Drawing.SystemIcons.Application;
                        }
                        else
                            icon = System.Drawing.SystemIcons.Application;
                    }
                    _trayIcon = new System.Windows.Forms.NotifyIcon();
                    _trayIcon.Icon = icon;
                    _trayIcon.Text = "Naxi Bootstrap";
                    _trayIcon.Visible = false;
                    var menu = new System.Windows.Forms.ContextMenuStrip();
                    var showItem = menu.Items.Add("Show");
                    showItem.Click += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); _trayIcon!.Visible = false; _minimizedToTray = false; };
                    menu.Items.Add("-");
                    var exitItem = menu.Items.Add("Exit");
                    exitItem.Click += (_, _) => { try { _trayIcon!.Visible = false; _trayIcon.Dispose(); } catch { } Environment.Exit(0); };
                    _trayIcon.ContextMenuStrip = menu;
                    ApplyProBranding();
                    _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); _trayIcon!.Visible = false; _minimizedToTray = false; };
                    // hook for second instance to restore
                    var helper = new System.Windows.Interop.WindowInteropHelper(this);
                    var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                    source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                    {
                        if (msg == 0x0401) { Show(); WindowState = WindowState.Normal; Activate(); if (_trayIcon != null) _trayIcon.Visible = false; _minimizedToTray = false; }
                        if (msg == 0x0312 && wParam.ToInt32() == HotkeyId) { ShowQuickLaunch(); handled = true; }
                        return IntPtr.Zero;
                    });
                } catch { }
                ApplyQuickHotkey();
        };
        Closing += MainWindow_Closing;
    }

    private bool _minimizedToTray;

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_minimizedToTray)
        {
            e.Cancel = true;
            Hide();
            _trayIcon!.Visible = true;
            _minimizedToTray = true;
            return;
        }
        try { _trayIcon?.Dispose(); } catch { }
        DiscordRpcService.Stop();
    }

    private void PlayOpenAnimation()
    {
        var root = (Border)Content;
        root.Opacity = 0;
        var tt = new TranslateTransform(0, 20);
        root.RenderTransform = tt;
        AnimateFadeSlide(root, tt, 0, 20, 0);

        for (var i = 0; i < _homeLeft.Children.Count; i++)
            FadeSlideIn(_homeLeft.Children[i], 140 + i * 80);
        FadeSlideIn(_logCard, 480);
    }

    private static CubicEase EaseOut() => new() { EasingMode = EasingMode.EaseOut };

    // Universal hover/press scale animation: works with ScaleTransform,
    // TransformGroup (game cards) or no transform at all.
    private static void AnimScale(FrameworkElement el, double scale, int ms)
    {
        var d = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(ms)) { EasingFunction = EaseOut() };
        switch (el.RenderTransform)
        {
            case ScaleTransform st:
                st.BeginAnimation(ScaleTransform.ScaleXProperty, d);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, d);
                break;
            case TransformGroup tg:
                foreach (var t in tg.Children.OfType<ScaleTransform>())
                {
                    t.BeginAnimation(ScaleTransform.ScaleXProperty, d);
                    t.BeginAnimation(ScaleTransform.ScaleYProperty, d);
                }
                break;
            default:
                el.RenderTransformOrigin = new Point(0.5, 0.5);
                el.RenderTransform = new ScaleTransform(scale, scale);
                break;
        }
    }
    private static BackEase BackOut(double amplitude = 0.35) => new() { EasingMode = EasingMode.EaseOut, Amplitude = amplitude };

    // Springy (overshoot) scale for small elements: stars, chips, floating buttons.
    private static void AnimScaleSpring(FrameworkElement el, double scale, int ms)
    {
        var d = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(ms)) { EasingFunction = BackOut() };
        switch (el.RenderTransform)
        {
            case ScaleTransform st:
                st.BeginAnimation(ScaleTransform.ScaleXProperty, d);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, d);
                break;
            case TransformGroup tg:
                foreach (var t in tg.Children.OfType<ScaleTransform>())
                {
                    t.BeginAnimation(ScaleTransform.ScaleXProperty, d);
                    t.BeginAnimation(ScaleTransform.ScaleYProperty, d);
                }
                break;
            default:
                el.RenderTransformOrigin = new Point(0.5, 0.5);
                el.RenderTransform = new ScaleTransform(scale, scale);
                break;
        }
    }

    private static Color SolidColor(Brush b, Color fallback)
        => b is SolidColorBrush sc ? sc.Color : fallback;

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    // Animate a persistent glow effect (no effect swapping → no flicker).
    private static void GlowTo(System.Windows.Media.Effects.DropShadowEffect glow, double opacity, double blur, int ms)
    {
        glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(ms)) { EasingFunction = EaseOut() });
        glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty,
            new DoubleAnimation(blur, TimeSpan.FromMilliseconds(ms)) { EasingFunction = EaseOut() });
    }

    // Expanding fading ring (used when a game is favorited).
    private void PlayBurst(System.Windows.Controls.Grid host, Color color)
    {
        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = 30,
            Height = 30,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            Opacity = 0.85,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.6, 0.6)
        };
        host.Children.Add(ring);
        var sc = (ScaleTransform)ring.RenderTransform;
        var spread = new DoubleAnimation(0.6, 1.9, TimeSpan.FromMilliseconds(420)) { EasingFunction = EaseOut() };
        var fade = new DoubleAnimation(0.85, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = EaseOut() };
        fade.Completed += (_, _) => host.Children.Remove(ring);
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, spread);
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, spread);
        ring.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private static void AnimateFadeSlide(UIElement el, TranslateTransform tt, double fromY, double toY, int delayMs)
    {
        el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, toY, TimeSpan.FromMilliseconds(340)) { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void FadeSlideIn(UIElement el, int delayMs, double fromY = 16)
    {
        el.Opacity = 0;
        var tt = new TranslateTransform(0, fromY);
        el.RenderTransform = tt;
        AnimateFadeSlide(el, tt, fromY, 0, delayMs);
    }

    private void SwitchPage(Grid page, Button nav)
    {
        if (_switching || PageHost.Content == page) return;
        _switching = true;
        SetActive(nav);

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(110)) { EasingFunction = EaseOut() };
        fadeOut.Completed += (_, _) =>
        {
            PageHost.Content = page;
            ResetPageScroll(page);
            var tt = new TranslateTransform(0, 18);
            PageHost.RenderTransform = tt;
            PageHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = EaseOut() });
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = EaseOut() });
            _switching = false;
        };
        PageHost.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void ResetPageScroll(Grid page)
    {
        var sv = page == _fastFlags ? _fastFlagsScroll
            : page == _maintenance ? _maintenanceScroll
            : page == _settings ? _settingsScroll
            : null;
        if (sv == null) return;
        sv.BeginAnimation(SmoothScroll.VerticalOffsetProperty, null);
        sv.ScrollToTop();
    }

    private void DragWindow(object sender, MouseButtonEventArgs e)
    { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }

    private void SetActive(Button active)
    {
        foreach (var b in new[] { HomeNav, GamesNav, NewsNav, AccountsNav, FlagsNav, CursorsNav, EmotesNav, MaintenanceNav, SettingsNav, ProNav, AboutNav, LegalNav })
        {
            bool isActive = b == active;
            b.Style = (Style)FindResource(isActive ? "NavButtonActive" : "NavButton");
            try { AnimateNavLabel(b, isActive); } catch { }
        }
    }

    // Compact icon navigation: every section shows only its glyph; the active one
    // smoothly expands to reveal its name (label width + fade), like modern launchers.
    private void InitNavButtons()
    {
        try
        {
            InitNavButton(HomeNav, "\uE80F", "Home");
            InitNavButton(GamesNav, "\uE7FC", "Games");
            InitNavButton(NewsNav, "\uE8A5", "News");
            InitNavButton(AccountsNav, "\uE77B", "Accounts");
            InitNavButton(FlagsNav, "\uE7C1", "FastFlags");
            InitNavButton(CursorsNav, "\uE7C9", "Cursors");
            InitNavButton(EmotesNav, "\uE76E", "Emotes");
            InitNavButton(MaintenanceNav, "\uE90F", "Maintenance");
            InitNavButton(SettingsNav, "\uE713", "Settings");
            InitNavButton(ProNav, "\uE735", "Pro");
            InitNavButton(AboutNav, "\uE716", "About us");
            InitNavButton(LegalNav, "\uE72E", "Legal");
            foreach (var b in _navLabels.Keys) AnimateNavLabel(b, b == HomeNav);
        }
        catch { }
    }

    private void InitNavButton(Button b, string glyph, string title)
    {
        var icon = new TextBlock { Text = glyph, FontFamily = NavIconFont, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = Localization.T(title), FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 1, 0) };
        var host = new Border { MaxWidth = 0, Opacity = 0, ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center, Child = label };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(icon);
        panel.Children.Add(host);
        b.Content = panel;
        b.Padding = new Thickness(11, 7, 11, 7);
        System.Windows.Automation.AutomationProperties.SetAutomationId(b, title);
        _navLabels[b] = (host, label, title);
    }

    private void AnimateNavLabel(Button b, bool active, bool animate = true)
    {
        if (!_navLabels.TryGetValue(b, out var info)) return;
        var (host, label, title) = info;
        label.Text = Localization.T(title);
        // Expand by animating MaxWidth: the border grows to fit the text and stops
        // exactly at its width — no text measuring needed, robust for any language.
        if (!animate)
        {
            host.BeginAnimation(Border.MaxWidthProperty, null);
            host.BeginAnimation(UIElement.OpacityProperty, null);
            host.MaxWidth = active ? 200 : 0;
            host.Opacity = active ? 1 : 0;
            return;
        }
        double from = host.ActualWidth > 0 ? host.ActualWidth : Math.Max(0, host.MaxWidth);
        host.BeginAnimation(Border.MaxWidthProperty, new DoubleAnimation(from, active ? 200 : 0, TimeSpan.FromMilliseconds(480)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(active ? 0 : 1, active ? 1 : 0, TimeSpan.FromMilliseconds(360)));
    }

    private void Home_Click(object sender, RoutedEventArgs e) => SwitchPage(_home, HomeNav);
    private void Games_Click(object sender, RoutedEventArgs e) { RefreshGameHubLive(); SwitchPage(_games, GamesNav); }
    private void News_Click(object sender, RoutedEventArgs e) { _ = LoadNewsAsync(); SwitchPage(_news, NewsNav); }
    private void Accounts_Click(object sender, RoutedEventArgs e) { RenderAccounts(); SwitchPage(_accounts, AccountsNav); }
    private void Flags_Click(object sender, RoutedEventArgs e) { CheckRobloxVersion(); SwitchPage(_fastFlags, FlagsNav); }
    private void Cursors_Click(object sender, RoutedEventArgs e) { RefreshCursorPreviews(); SwitchPage(_cursors, CursorsNav); }
    private void Emotes_Click(object sender, RoutedEventArgs e) { SwitchPage(_emotes, EmotesNav); }
    private void Maintenance_Click(object sender, RoutedEventArgs e) { RefreshSizes(); RefreshInstallInfo(); SwitchPage(_maintenance, MaintenanceNav); }
    private void Settings_Click(object sender, RoutedEventArgs e) => SwitchPage(_settings, SettingsNav);
    private void Pro_Click(object sender, RoutedEventArgs e) => SwitchPage(_pro, ProNav);
    private void About_Click(object sender, RoutedEventArgs e) => SwitchPage(_about, AboutNav);
    private void Legal_Click(object sender, RoutedEventArgs e) => SwitchPage(_legal, LegalNav);
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private Grid BuildHome()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(470) });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(84, 0, 40, 0) };
        left.Children.Add(T("Free external utility", 13, (Brush)FindResource("Muted"), false));
        left.Children.Add(new TextBlock
        {
            Text = "Naxi",
            FontSize = 62,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TitleGradient"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        var badges = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        _versionBadge = T($"{AppVersion}    ·    Windows 10/11", 12.5, (Brush)FindResource("Muted"), false);
        badges.Children.Add(_versionBadge);
        left.Children.Add(badges);

        var updateBlock = new StackPanel { Margin = new Thickness(0, 32, 0, 0) };
        _updateText = T("Checking for updates...", 17, (Brush)FindResource("Text"), true);
        updateBlock.Children.Add(_updateText);
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Margin = new Thickness(0, 14, 0, 0) };
        updateBlock.Children.Add(_progress);
        _percent = T("0%", 12, (Brush)FindResource("Muted"), false, 7);
        updateBlock.Children.Add(_percent);
        left.Children.Add(updateBlock);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 28, 0, 0) };
        _updateBtn = Btn("Update", false, 130); _updateBtn.IsEnabled = false; _updateBtn.Click += Update_Click;
        var launch = Btn("Launch", true, 130); launch.Margin = new Thickness(10, 0, 0, 0); launch.Click += Launch_Click;
        var studio = Btn("Studio", false, 110); studio.Margin = new Thickness(10, 0, 0, 0); studio.Click += Studio_Click;
        var discord = Btn("Discord", false, 110); discord.Margin = new Thickness(10, 0, 0, 0); discord.Click += (_, _) => OpenLink(DiscordUrl);
        buttons.Children.Add(_updateBtn); buttons.Children.Add(launch); buttons.Children.Add(studio); buttons.Children.Add(discord);
        left.Children.Add(buttons);

        Grid.SetColumn(left, 0); g.Children.Add(left);
        _homeLeft = left;

        _logCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(22), Margin = new Thickness(0, 8, 24, 0) };
        var logGrid = new Grid();
        logGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        logGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var logHead = new StackPanel { Orientation = Orientation.Horizontal };
        logHead.Children.Add(T("UPDATE LOG", 11.5, (Brush)FindResource("LogHeader"), true));
        var logHint = T("Click a version to see the full log", 11, (Brush)FindResource("Muted"), false);
        logHint.VerticalAlignment = VerticalAlignment.Center;
        logHint.Margin = new Thickness(10, 0, 0, 0);
        logHead.Children.Add(logHint);
        logGrid.Children.Add(logHead);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 14, 0, 0) };
        scroll.Content = _updateList;
        Grid.SetRow(scroll, 1); logGrid.Children.Add(scroll);
        _logCard.Child = logGrid;
        Grid.SetColumn(_logCard, 1); g.Children.Add(_logCard);

        return g;
    }

    private async Task CheckForUpdatesAsync()
    {
        // Fetch the remote log and release info in parallel so a slow endpoint
        // can never block the other one (worst case = one 8s timeout, not two).
        var logTask = FetchRemoteLogAsync();
        var releaseTask = GetLatestReleaseAsync();

        try
        {
            var remote = await logTask;
            if (remote is { Count: > 0 })
            {
                _log = remote;
                RenderLog(_log);
                // cache successful fetch locally
                try { File.WriteAllText(LogFile, JsonSerializer.Serialize(remote, new JsonSerializerOptions { WriteIndented = true })); } catch { }
            }
        } catch { }

        try
        {
            var (tag, asset) = await releaseTask;
            if (IsNewer(tag))
            {
                _updateText.Text = $"Update available: {tag}";
                _updateBtn.IsEnabled = asset != null;
            }
            else
            {
                _updateText.Text = "Naxi is up to date";
            }
        }
        catch
        {
            _updateText.Text = "Could not check for updates";
            // keep log visible even if release check fails
            if (_log.Count == 0 || _log[0].Version == "v1.1.0")
            {
                try
                {
                    var fallback = LoadLog();
                    if (fallback.Count > 0) { _log = fallback; RenderLog(_log); }
                } catch { }
            }
        }

        try { CheckRobloxVersion(); } catch { }
    }

    private void CheckRobloxVersion()
    {
        var current = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        var name = current == null ? "" : Path.GetFileName(current);
        var stored = _config.RobloxVersion;

        if (name.Length == 0) return;

        if (string.IsNullOrEmpty(stored))
        {
            _config.RobloxVersion = name;
            RobloxLauncher.SaveConfig(_config);
            return;
        }

        if (name != stored)
        {
            // Auto-migrate everything to new version
            EnsureMigratedBackups();
            MigrateCustomizationsIfNeeded(_config, stored, name);
            ApplySavedCursors();
            RobloxLauncher.ApplyFlags(_config);
            // Apply sky/font from cache if exists (already done in Migrate, but ensure)
            try
            {
                if (Directory.Exists(CustomSkyCacheDir) && Directory.GetFiles(CustomSkyCacheDir, "*.tex").Length > 0)
                {
                    foreach (var pf in RobloxLauncher.FindPlayerFolders())
                    {
                        var skyDir = Path.Combine(pf, "PlatformContent", "pc", "textures", "sky");
                        if (!Directory.Exists(skyDir)) continue;
                        foreach (var f in Directory.GetFiles(CustomSkyCacheDir, "*.tex"))
                            File.Copy(f, Path.Combine(skyDir, Path.GetFileName(f)), true);
                    }
                }
                if (File.Exists(CustomFontPath_File))
                {
                    foreach (var pf in RobloxLauncher.FindPlayerFolders())
                    {
                        var fontsDir = Path.Combine(pf, "Content", "fonts");
                        if (!Directory.Exists(fontsDir)) continue;
                        var targets = Directory.GetFiles(fontsDir, "*.*").Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)).ToArray();
                        foreach (var t in targets) File.Copy(CustomFontPath_File, t, true);
                    }
                }
            } catch { }

            _config.RobloxVersion = name;
            RobloxLauncher.SaveConfig(_config);
            // keep flag for UI
            _robloxNeedsReapply = false;
            _updateText.Text = $"Roblox updated to {name} — customizations migrated automatically";
        }
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        _updateBtn.IsEnabled = false;

        try
        {
            var (tag, asset) = await GetLatestReleaseAsync();
            if (IsNewer(tag) && asset != null)
            {
                await RunLauncherUpdate(tag, asset);
                return;
            }

            if (_robloxNeedsReapply)
            {
                _flagsStatus.Text = RobloxLauncher.ApplyFlags(_config);
                var current = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
                _config.RobloxVersion = current == null ? "" : Path.GetFileName(current);
                RobloxLauncher.SaveConfig(_config);
                _robloxNeedsReapply = false;
                _updateText.Text = "FastFlags re-applied to " + (current == null ? "Roblox" : Path.GetFileName(current));
                _updating = false;
                return;
            }

            _updateText.Text = "Naxi is up to date";
            _updating = false;
        }
        catch (Exception ex)
        {
            _updateText.Text = "Update failed: " + ex.Message;
            _progress.Value = 0;
            _percent.Text = "0%";
            _updateBtn.IsEnabled = true;
            _updating = false;
        }
    }

    private async Task RunLauncherUpdate(string tag, string asset)
    {
        _updateText.Text = $"Downloading {tag}...";
        bool isZip = asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        string downloadPath = isZip ? UpdateZip : UpdateZip + ".exe";
        await DownloadAsync(asset, downloadPath);

        _updateText.Text = "Installing...";
        _progress.Value = 100;
        _percent.Text = "100%";

        var appDir = AppContext.BaseDirectory;
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExe)) currentExe = Path.Combine(appDir, "NaxiBootstrap.exe");

        if (isZip)
        {
            if (Directory.Exists(UpdateDir)) Directory.Delete(UpdateDir, true);
            ZipFile.ExtractToDirectory(downloadPath, UpdateDir);

            var source = UpdateDir;
            if (Directory.GetFiles(UpdateDir).Length == 0 && Directory.GetDirectories(UpdateDir).Length == 1)
                source = Directory.GetDirectories(UpdateDir)[0];

            var oldExe = currentExe + ".old";
            try { if (File.Exists(oldExe)) File.Delete(oldExe); } catch { }
            try { File.Move(currentExe, oldExe, true); } catch { }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(source, file);
                var dest = Path.Combine(appDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }
        }
        else
        {
            var newExePath = Path.Combine(appDir, "NaxiBootstrap.exe");
            var oldExe = currentExe + ".old";
            try { if (File.Exists(oldExe)) File.Delete(oldExe); } catch { }
            try { if (File.Exists(currentExe)) File.Move(currentExe, oldExe, true); } catch { }
            File.Copy(downloadPath, newExePath, true);
        }

        var newExe = Path.Combine(appDir, "NaxiBootstrap.exe");
        Process.Start(new ProcessStartInfo(newExe) { UseShellExecute = true });
        Environment.Exit(0);
    }

    private async Task<(string Tag, string? AssetUrl)> GetLatestReleaseAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(ReleasesApiUrl, cts.Token));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        string? asset = null;
        string? exeFallback = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    asset = a.GetProperty("browser_download_url").GetString();
                    break;
                }
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    exeFallback = a.GetProperty("browser_download_url").GetString();
            }
        }
        return (tag, asset ?? exeFallback);
    }

    private async Task<List<LogEntry>?> FetchRemoteLogAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var json = await Http.GetStringAsync(UpdateLogUrl, cts.Token);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<LogEntry>>(json, opts);
    }

    private async Task DownloadAsync(string url, string dest)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync();
        await using var dst = File.Create(dest);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer)) > 0)
        {
            await dst.WriteAsync(buffer, 0, n);
            read += n;
            if (total > 0)
            {
                var p = read * 100.0 / total;
                _progress.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(_progress.Value, p, TimeSpan.FromMilliseconds(180)));
                _percent.Text = $"{(int)p}%";
            }
        }
    }

    private static bool IsNewer(string tag)
    {
        try { return new Version(tag.TrimStart('v', 'V')) > new Version(AppVersion.TrimStart('v', 'V')); }
        catch { return false; }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        var player = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        if (player == null)
        {
            MessageBox.Show("Roblox is not installed yet. Install it from roblox.com first.", "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        RobloxLauncher.RefreshTokensFromDat();
        var accounts = AccountStore.Load();
        var acc = accounts.FirstOrDefault(a => a.UserId == _config.LastUsedAccountId) ?? accounts.FirstOrDefault();

        if (acc != null)
        {
            var token = AccountStore.Unprotect(acc.ProtectedToken);
            RobloxLauncher.SetRobloxCookie(token);
            RobloxLauncher.SetModernCookie(token);
        }

        RobloxLauncher.ApplyFlags(_config);
        Process.Start(Path.Combine(player, "RobloxPlayerBeta.exe"));
        Close();
    }

    private void Studio_Click(object sender, RoutedEventArgs e)
    {
        var studioExe = RobloxLauncher.FindStudioExe();
        if (studioExe == null)
        {
            MessageBox.Show("Roblox Studio is not installed. Install it from create.roblox.com first.", "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // apply account cookie like Launch does
        try
        {
            RobloxLauncher.RefreshTokensFromDat();
            var accounts = AccountStore.Load();
            var acc = accounts.FirstOrDefault(a => a.UserId == _config.LastUsedAccountId) ?? accounts.FirstOrDefault();
            if (acc != null)
            {
                var token = AccountStore.Unprotect(acc.ProtectedToken);
                RobloxLauncher.SetRobloxCookie(token);
                RobloxLauncher.SetModernCookie(token);
            }
        } catch { }

        RobloxLauncher.ApplyFlags(_config);
        try { Process.Start(new ProcessStartInfo { FileName = studioExe, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"Failed to launch Studio: {ex.Message}", "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        Close();
    }

    private void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { }
    }

    private List<LogEntry> LoadLog()
    {
        try
        {
            if (File.Exists(LogFile))
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var list = JsonSerializer.Deserialize<List<LogEntry>>(File.ReadAllText(LogFile), opts);
                if (list is { Count: > 0 }) return list;
            }
        }
        catch { }
        return DefaultLog();
    }

    private static List<LogEntry> DefaultLog() => new()
    {
        new("v1.1.0", null, true, new List<LogChange>
        {
            new("+", "Updated UI"),
            new("+", "Maintenance tab"),
            new("~", "Bug fix")
        })
    };

    private sealed record LogChange(string Type, string Text);
    private sealed record LogEntry(string Version, string? Date, bool New, List<LogChange> Changes);
    private sealed record NewsEntry(string Id, string Author, string? Date, string ImageUrl, string Text, string? FontFamily, string? AvatarUrl, string? Title, string? Subtitle);

    private static Version? ParseVersion(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        try { return new Version(v.TrimStart('v', 'V')); }
        catch { return null; }
    }

    private void RenderLog(List<LogEntry> log)
    {
        _updateList.Children.Clear();
        var ordered = log.OrderByDescending(e => ParseVersion(e.Version) ?? new Version(0, 0)).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            var card = new Border { Style = (Style)FindResource("UpdateCard"), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 10), Cursor = System.Windows.Input.Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ShowUpdateCardWindow(entry);
            var s = new StackPanel();

            var head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ver = T(entry.Version, 16.5, (Brush)FindResource("Text"), true);
            Grid.SetColumn(ver, 0); head.Children.Add(ver);
            if (entry.New)
            {
                var badge = new Border { Background = (Brush)FindResource("Accent"), CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 3, 9, 3), VerticalAlignment = VerticalAlignment.Center, Child = T("NEW", 10.5, (Brush)FindResource("AccentText"), true) };
                Grid.SetColumn(badge, 1); head.Children.Add(badge);
            }
            else if (entry.Date != null)
            {
                var d = T(entry.Date, 12.5, (Brush)FindResource("Muted"), false);
                d.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(d, 1); head.Children.Add(d);
            }
            s.Children.Add(head);

            foreach (var c in entry.Changes)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                row.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 9, 0) });
                row.Children.Add(T($"[{c.Type}] {Localization.T(c.Text)}", 13, (Brush)FindResource("Muted"), false));
                s.Children.Add(row);
            }

            card.Child = s;
            _updateList.Children.Add(card);
            FadeSlideIn(card, i * 70);
        }
    }

    private void ShowUpdateCardWindow(LogEntry entry)
    {
        var dialog = new Window
        {
            Title = "Update log",
            Owner = this,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var shell = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderBrush = new SolidColorBrush(Color.FromRgb(50, 66, 84)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.85, 0.85),
            Background = new LinearGradientBrush(Color.FromRgb(10, 18, 31), Color.FromRgb(13, 25, 42), new Point(0, 0), new Point(1, 1))
        };
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var closeRow = new Grid();
        var close = new Button { Content = "✕", HorizontalAlignment = HorizontalAlignment.Right };
        close.Style = (Style)FindResource("CloseCrossButton");
        closeRow.Children.Add(close);
        Grid.SetRow(closeRow, 0); root.Children.Add(closeRow);

        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(20), Margin = new Thickness(0, 10, 0, 0) };
        var s = new StackPanel();
        var head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ver = T(entry.Version, 18, (Brush)FindResource("Text"), true);
        Grid.SetColumn(ver, 0); head.Children.Add(ver);
        if (entry.New)
        {
            var badge = new Border { Background = (Brush)FindResource("Accent"), CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 3, 9, 3), VerticalAlignment = VerticalAlignment.Center, Child = T("NEW", 10.5, (Brush)FindResource("AccentText"), true) };
            Grid.SetColumn(badge, 1); head.Children.Add(badge);
        }
        else if (entry.Date != null)
        {
            var d = T(entry.Date, 12.5, (Brush)FindResource("Muted"), false);
            d.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(d, 1); head.Children.Add(d);
        }
        s.Children.Add(head);
        foreach (var c in entry.Changes)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            row.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 9, 0) });
            row.Children.Add(T($"[{c.Type}] {Localization.T(c.Text)}", 13.5, (Brush)FindResource("Muted"), false));
            s.Children.Add(row);
        }
        card.Child = s;
        Grid.SetRow(card, 1); root.Children.Add(card);

        shell.Child = root;
        dialog.Content = shell;

        var closing = false;
        void CloseAnimated()
        {
            if (closing) return;
            closing = true;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(170)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            var scale = new DoubleAnimation(1, 0.9, TimeSpan.FromMilliseconds(170)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += (_, _) => dialog.Close();
            shell.BeginAnimation(UIElement.OpacityProperty, fade);
            var st = (ScaleTransform)shell.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }

        close.Click += (_, _) => CloseAnimated();
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) CloseAnimated(); };
        dialog.Loaded += (_, _) =>
        {
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(230)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var scale = new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            shell.BeginAnimation(UIElement.OpacityProperty, fade);
            var st = (ScaleTransform)shell.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        };
        dialog.ShowDialog();
    }

    private Grid BuildFastFlags()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel();
        title.Children.Add(T("FastFlags", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        host.Children.Add(T("Changes apply automatically. Note: recent Roblox versions deny most local flags — run Diagnostics to see what your client accepts.", 13, (Brush)FindResource("Muted"), false));

        var perf = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24) };
        var ps = new StackPanel();
        ps.Children.Add(SectionLabel("PERFORMANCE"));
        ps.Children.Add(T("FPS Presets", 15.5, (Brush)FindResource("Text"), true));
        ps.Children.Add(T("One click — tested flag combos. Potato = maximum FPS, Quality = best graphics.", 12.5, (Brush)FindResource("Muted"), false, 3));
        var ffRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 6) };
        var ff1 = Btn("Potato", false, 130); ff1.Height = 40; ff1.Click += (_, _) => ApplyFfPreset("potato");
        var ff2 = Btn("Balanced", false, 130); ff2.Height = 40; ff2.Margin = new Thickness(8, 0, 0, 0); ff2.Click += (_, _) => ApplyFfPreset("balanced");
        var ff3 = Btn("Quality", false, 130); ff3.Height = 40; ff3.Margin = new Thickness(8, 0, 0, 0); ff3.Click += (_, _) => ApplyFfPreset("quality");
        ffRow.Children.Add(ff1); ffRow.Children.Add(ff2); ffRow.Children.Add(ff3);
        ps.Children.Add(ffRow);
        ps.Children.Add(ToggleRow("FPS Unlocker", "Remove the default FPS cap in Roblox.", _config.FpsUnlock, v => { _config.FpsUnlock = v; _fpsBox.IsEnabled = v; SaveAndApply(); }));

        var fpsRow = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        fpsRow.ColumnDefinitions.Add(new ColumnDefinition());
        fpsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var fpsLabel = new StackPanel();
        fpsLabel.Children.Add(T("FPS Limit", 15.5, (Brush)FindResource("Text"), true));
        fpsLabel.Children.Add(T("Target frames per second (1–9999)", 12.5, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(fpsLabel, 0); fpsRow.Children.Add(fpsLabel);
        _fpsBox = new TextBox { Style = (Style)FindResource("InputBox"), Width = 110, Text = _config.FpsLimit.ToString(), IsEnabled = _config.FpsUnlock, VerticalAlignment = VerticalAlignment.Center };
        _fpsBox.LostKeyboardFocus += (_, _) => SaveAndApply();
        Grid.SetColumn(_fpsBox, 1); fpsRow.Children.Add(_fpsBox);
        ps.Children.Add(fpsRow);

        ps.Children.Add(ToggleRow("Disable Shadows", "Turn off dynamic shadows for extra FPS.", _config.NoShadows, v => { _config.NoShadows = v; SaveAndApply(); }));
        ps.Children.Add(ToggleRow("Performance Mode", "Lowest quality, no grass and no anti-aliasing for maximum FPS.", _config.PerfMode, v => { _config.PerfMode = v; SaveAndApply(); }));
        perf.Child = ps;
        host.Children.Add(perf);

        var gfx = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var gs = new StackPanel();
        gs.Children.Add(SectionLabel("GRAPHICS"));
        gs.Children.Add(ToggleRow("Future Lighting", "Force the newest Roblox lighting engine.", _config.FutureLighting, v => { _config.FutureLighting = v; SaveAndApply(); }));
        gs.Children.Add(ToggleRow("Disable Post-Effects", "Turn off blur, bloom and color effects.", _config.NoPostFx, v => { _config.NoPostFx = v; SaveAndApply(); }));
        gfx.Child = gs;
        host.Children.Add(gfx);

        var priv = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var vs = new StackPanel();
        vs.Children.Add(SectionLabel("PRIVACY"));
        vs.Children.Add(ToggleRow("Disable Telemetry", "Send less analytics data to Roblox servers.", _config.NoTelemetry, v => { _config.NoTelemetry = v; SaveAndApply(); }));
        priv.Child = vs;
        host.Children.Add(priv);

        var gpCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var gsv = new StackPanel();
        gsv.Children.Add(SectionLabel("PER-GAME PROFILES"));
        gsv.Children.Add(T("Each game gets its own flag preset — captured automatically when you launch it through Naxi and applied on every launch.", 12.5, (Brush)FindResource("Muted"), false, 2));
        if (!HasPro) gsv.Children.Add(T("Editing game profiles requires Pro.", 12, (Brush)FindResource("Accent"), false, 4));

        if (_config.GameProfiles == null || _config.GameProfiles.Count == 0)
        {
            gsv.Children.Add(T("No game profiles yet — launch a game through Naxi and it will appear here.", 12, (Brush)FindResource("Muted"), false, 10));
        }
        else
        {
            foreach (var kv in _config.GameProfiles.OrderBy(k => k.Value.Name))
                gsv.Children.Add(GameProfileRow(kv.Key, kv.Value));
        }
        gpCard.Child = gsv;
        host.Children.Add(gpCard);

        var skyCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var ss = new StackPanel();
        ss.Children.Add(SectionLabel("SKYBOX"));
        ss.Children.Add(T("Replace the Roblox sky with your own .tex files. Select any .tex file from your sky folder — all .tex files from that folder will be applied. Close Roblox before changing.", 12.5, (Brush)FindResource("Muted"), false, 2));
        var skyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var setSky = Btn("Set Sky Folder...", false, 170); setSky.Click += SetSky_Click;
        var resetSky = Btn("Reset Sky", false, 130); resetSky.Margin = new Thickness(12, 0, 0, 0); resetSky.Click += ResetSky_Click;
        _skyStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        _skyStatus.VerticalAlignment = VerticalAlignment.Center;
        _skyStatus.Margin = new Thickness(14, 0, 0, 0);
        skyRow.Children.Add(setSky); skyRow.Children.Add(resetSky); skyRow.Children.Add(_skyStatus);
        ss.Children.Add(skyRow);
        skyCard.Child = ss;
        host.Children.Add(skyCard);

        var fontCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var fs = new StackPanel();
        fs.Children.Add(SectionLabel("ROBLOX FONT"));
        fs.Children.Add(T("Replace all Roblox fonts with your own .ttf/.otf file. This affects both the client and in-game text. Close Roblox before changing.", 12.5, (Brush)FindResource("Muted"), false, 2));
        var fontRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var setFont = Btn("Set Font File...", false, 170); setFont.Click += SetRobloxFont_Click;
        var resetFont = Btn("Reset Font", false, 130); resetFont.Margin = new Thickness(12, 0, 0, 0); resetFont.Click += ResetRobloxFont_Click;
        _rbxFontStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        _rbxFontStatus.VerticalAlignment = VerticalAlignment.Center;
        _rbxFontStatus.Margin = new Thickness(14, 0, 0, 0);
        fontRow2.Children.Add(setFont); fontRow2.Children.Add(resetFont); fontRow2.Children.Add(_rbxFontStatus);
        fs.Children.Add(fontRow2);
        fontCard.Child = fs;
        host.Children.Add(fontCard);

        var diag = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var ds = new StackPanel();
        ds.Children.Add(SectionLabel("DIAGNOSTICS"));
        ds.Children.Add(T("Roblox decides which flags to accept. This reads the client log of your last Roblox session and shows exactly which flags were denied.", 12.5, (Brush)FindResource("Muted"), false, 2));
        var check = Btn("Check Roblox Verdict", false, 200); check.Margin = new Thickness(0, 16, 0, 0); check.Click += CheckVerdict_Click;
        ds.Children.Add(check);
        _verdictText = T("", 12.5, (Brush)FindResource("Muted"), false, 12);
        _verdictText.TextWrapping = TextWrapping.Wrap;
        ds.Children.Add(_verdictText);
        diag.Child = ds;
        host.Children.Add(diag);

        var applyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        var apply = Btn("Apply to Roblox", true, 170); apply.Click += ApplyToRoblox_Click; _applyBtn = apply;
        var reset = Btn("Reset", false, 110); reset.Margin = new Thickness(12, 0, 0, 0); reset.Click += ResetFlags_Click;
        _flagsStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        _flagsStatus.VerticalAlignment = VerticalAlignment.Center;
        _flagsStatus.Margin = new Thickness(14, 0, 0, 0);
        applyRow.Children.Add(apply); applyRow.Children.Add(reset); applyRow.Children.Add(_flagsStatus);
        host.Children.Add(applyRow);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);
        _fastFlagsScroll = scroll;
        return g;
    }

    private async void ApplyToRoblox_Click(object sender, RoutedEventArgs e)
    {
        var btn = _applyBtn;
        if (btn == null) { SaveAndApply(); return; }

        // 1) press feedback — quick scale-down
        var tt = btn.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        btn.RenderTransform = tt;
        tt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(90)) { EasingFunction = EaseOut() });
        tt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(90)) { EasingFunction = EaseOut() });
        btn.Opacity = 0.7;

        // 2) short "Applying..." state so the press is visible
        var original = "Apply to Roblox";
        btn.Content = "Applying...";
        try { await Task.Delay(220); } catch { }

        // 3) do the actual apply (background, keeps UI responsive)
        string status = "";
        await Task.Run(() => { try { status = RobloxLauncher.ApplyFlags(_config); } catch { } });

        // 4) success feedback — restore, green check + pop
        btn.Opacity = 1;
        btn.Content = "Applied ✓";
        tt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        tt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        _flagsStatus.Text = status;
        _flagsStatus.Foreground = (Brush)FindResource("Green");

        try { await Task.Delay(900); } catch { }
        btn.Content = Localization.T(original);
    }

    private void SaveAndApply()
    {
        if (int.TryParse(_fpsBox.Text.Trim(), out var fps)) _config.FpsLimit = Math.Clamp(fps, 1, 9999);
        _fpsBox.Text = _config.FpsLimit.ToString();
        RobloxLauncher.SaveConfig(_config);
        _flagsStatus.Text = RobloxLauncher.ApplyFlags(_config);
    }

    private Button SmallBtn(string text, bool accent, int width)
        => new() { Content = text, Style = (Style)FindResource(accent ? "AccentPillButton" : "PillButton"), Width = width, Height = 32, FontSize = 12.5, Padding = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };

    private void FontBtn_Click(object sender, RoutedEventArgs e)
    {
        var idx = Array.IndexOf(Fonts, _config.FontName);
        var next = Fonts[idx < 0 ? 0 : (idx + 1) % Fonts.Length];
        _config.FontName = next;
        RobloxLauncher.SaveConfig(_config);
        FontFamily = new FontFamily(next);
        _fontBtn.Content = $"Font: {next}";
    }

    private async void ApplyBackground_Click(object sender, RoutedEventArgs e)
    {
        var url = _bgUrlBox.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            _bgStatus.Text = "Invalid image URL";
            return;
        }
        _bgStatus.Text = "Downloading...";
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            SetBackground(img);
            Directory.CreateDirectory(Path.GetDirectoryName(BgCacheFile)!);
            File.WriteAllBytes(BgCacheFile, bytes);
            _config.BackgroundUrl = url;
            RobloxLauncher.SaveConfig(_config);
            _bgStatus.Text = "Background applied";
        }
        catch
        {
            _bgStatus.Text = "Failed to download image";
        }
    }

    private void ClearBackground_Click(object sender, RoutedEventArgs e)
    {
        _config.BackgroundUrl = "";
        RobloxLauncher.SaveConfig(_config);
        _bgUrlBox.Text = "";
        BgImage.Source = null;
        BgImage.Visibility = Visibility.Collapsed;
        BgOverlay.Visibility = Visibility.Collapsed;
        try { if (File.Exists(BgCacheFile)) File.Delete(BgCacheFile); } catch { }
        _bgStatus.Text = "Background cleared";
    }

    private void SetBackground(ImageSource img)
    {
        BgImage.Source = img;
        BgImage.Visibility = Visibility.Visible;
        BgOverlay.Visibility = Visibility.Visible;
    }

    private void RestoreBackgroundFromCache()
    {
        try
        {
            if (string.IsNullOrEmpty(_config.BackgroundUrl)) return;
            if (File.Exists(BgCacheFile))
            {
                try
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = File.OpenRead(BgCacheFile);
                    img.EndInit();
                    img.Freeze();
                    SetBackground(img);
                    _bgUrlBox.Text = _config.BackgroundUrl;
                    return;
                }
                catch { try { File.Delete(BgCacheFile); } catch { } }
            }
            // Cache missing or corrupted (e.g. wiped by an update) — silently re-download
            // from the saved URL so the background always survives.
            var url = _config.BackgroundUrl;
            _ = Task.Run(async () =>
            {
                try
                {
                    var bytes = await Http.GetByteArrayAsync(url);
                    using var ms = new MemoryStream(bytes);
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = ms;
                    img.EndInit();
                    img.Freeze();
                    Dispatcher.Invoke(() => SetBackground(img));
                    Directory.CreateDirectory(Path.GetDirectoryName(BgCacheFile)!);
                    File.WriteAllBytes(BgCacheFile, bytes);
                    Dispatcher.Invoke(() => { try { _bgUrlBox.Text = url; } catch { } });
                }
                catch { }
            });
        }
        catch { }
    }

    private void SetSky_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _skyStatus.Text = Localization.T("Closing Roblox..."); foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta")) try { p.Kill(); } catch { } Thread.Sleep(1500); }
        var allFolders = RobloxLauncher.FindPlayerFolders();
        if (allFolders.Count == 0) { _skyStatus.Text = Localization.T("Roblox not found"); return; }

        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose .tex files from your sky folder", Filter = "TEX files (*.tex)|*.tex", Multiselect = true };
        if (dlg.ShowDialog() != true) return;

        var srcDir = Path.GetDirectoryName(dlg.FileNames[0]);
        if (srcDir == null) { _skyStatus.Text = "Invalid path"; return; }
        var srcFiles = dlg.FileNames;
        if (srcFiles.Length == 0) { _skyStatus.Text = Localization.T("No .tex files found"); return; }

        try
        {
            Directory.CreateDirectory(SkyBackupDir);
            foreach (var pf in allFolders)
            {
                var skyDir = Path.Combine(pf, "PlatformContent", "pc", "textures", "sky");
                if (!Directory.Exists(skyDir)) continue;
                foreach (var f in Directory.GetFiles(skyDir, "*.tex"))
                {
                    var bak = Path.Combine(SkyBackupDir, Path.GetFileName(f));
                    if (!File.Exists(bak)) File.Copy(f, bak, false);
                }
                foreach (var src in srcFiles)
                    File.Copy(src, Path.Combine(skyDir, Path.GetFileName(src)), true);
            }
            Directory.CreateDirectory(CustomSkyCacheDir);
            foreach (var src in srcFiles)
                File.Copy(src, Path.Combine(CustomSkyCacheDir, Path.GetFileName(src)), true);
            _config.SkyFolderPath = srcDir;
            RobloxLauncher.SaveConfig(_config);
            _skyStatus.Text = Localization.T("Sky replaced. Restart Roblox to see it.");
        }
        catch
        {
            _skyStatus.Text = Localization.T("Failed to replace sky");
        }
    }

    private void ResetSky_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _skyStatus.Text = Localization.T("Closing Roblox..."); foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta")) try { p.Kill(); } catch { } Thread.Sleep(1500); }
        if (!Directory.Exists(SkyBackupDir) || Directory.GetFiles(SkyBackupDir).Length == 0)
        {
            _skyStatus.Text = Localization.T("No sky backup found");
            return;
        }
        try
        {
            foreach (var pf in RobloxLauncher.FindPlayerFolders())
            {
                var skyDir = Path.Combine(pf, "PlatformContent", "pc", "textures", "sky");
                if (!Directory.Exists(skyDir)) continue;
                foreach (var f in Directory.GetFiles(SkyBackupDir))
                    File.Copy(f, Path.Combine(skyDir, Path.GetFileName(f)), true);
            }
            _config.SkyFolderPath = "";
            RobloxLauncher.SaveConfig(_config);
            if (Directory.Exists(CustomSkyCacheDir)) Directory.Delete(CustomSkyCacheDir, true);
            _skyStatus.Text = Localization.T("Original sky restored");
        }
        catch
        {
            _skyStatus.Text = Localization.T("Failed to restore sky");
        }
    }

    private void SetRobloxFont_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _rbxFontStatus.Text = Localization.T("Closing Roblox..."); foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta")) try { p.Kill(); } catch { } Thread.Sleep(1500); }
        var allFolders = RobloxLauncher.FindPlayerFolders();
        if (allFolders.Count == 0) { _rbxFontStatus.Text = Localization.T("Roblox not found"); return; }

        var dlg = new OpenFileDialog { Title = "Choose font file", Filter = "Font file|*.ttf;*.otf" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            Directory.CreateDirectory(FontBackupDir);
            foreach (var pf in allFolders)
            {
                var fontsDir = Path.Combine(pf, "Content", "fonts");
                if (!Directory.Exists(fontsDir)) continue;
                var targets = Directory.GetFiles(fontsDir, "*.*")
                    .Where(f => f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var t in targets)
                {
                    var backup = Path.Combine(FontBackupDir, Path.GetFileName(t));
                    if (!File.Exists(backup)) File.Copy(t, backup, false);
                    File.Copy(dlg.FileName, t, true);
                }
            }
            File.Copy(dlg.FileName, CustomFontPath_File, true);
            _rbxFontStatus.Text = "Font replaced. Restart Roblox.";
        }
        catch
        {
            _rbxFontStatus.Text = Localization.T("Failed to replace font");
        }
    }

    private void ResetRobloxFont_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _rbxFontStatus.Text = Localization.T("Closing Roblox..."); foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta")) try { p.Kill(); } catch { } Thread.Sleep(1500); }
        if (!Directory.Exists(FontBackupDir) || Directory.GetFiles(FontBackupDir).Length == 0)
        {
            _rbxFontStatus.Text = Localization.T("No font backup found");
            return;
        }
        try
        {
            foreach (var pf in RobloxLauncher.FindPlayerFolders())
            {
                var fontsDir = Path.Combine(pf, "Content", "fonts");
                if (!Directory.Exists(fontsDir)) continue;
                foreach (var f in Directory.GetFiles(FontBackupDir))
                    File.Copy(f, Path.Combine(fontsDir, Path.GetFileName(f)), true);
            }
            _config.FontName = "Segoe UI";
            RobloxLauncher.SaveConfig(_config);
            _rbxFontStatus.Text = Localization.T("Original font restored");
        }
        catch
        {
            _rbxFontStatus.Text = Localization.T("Failed to restore font");
        }
    }

    private void CheckVerdict_Click(object sender, RoutedEventArgs e)
    {
        var log = Directory.Exists(RobloxLogsDir)
            ? Directory.GetFiles(RobloxLogsDir, "*.log").OrderByDescending(File.GetLastWriteTime).FirstOrDefault()
            : null;

        if (log == null)
        {
            _verdictText.Foreground = (Brush)FindResource("Muted");
            _verdictText.Text = "No Roblox logs found. Launch Roblox once, then check again.";
            return;
        }

        try
        {
            var denied = File.ReadLines(log)
                .Where(l => l.Contains("Denied local configuration for: "))
                .Select(l => l.Substring(l.LastIndexOf(':') + 1).Trim())
                .Distinct()
                .ToList();

            var applied = RobloxLauncher.BuildFlags(_config).Keys.ToList();
            var accepted = applied.Where(f => !denied.Contains(f)).ToList();

            if (denied.Count == 0)
            {
                _verdictText.Foreground = new SolidColorBrush(Color.FromRgb(85, 216, 139));
                _verdictText.Text = "Roblox accepted all applied flags.";
            }
            else
            {
                _verdictText.Foreground = new SolidColorBrush(Color.FromRgb(232, 160, 96));
                var text = $"Roblox DENIED {denied.Count} of your flag(s) — the client refuses local overrides for them:\n";
                text += string.Join("\n", denied.Select(f => "  • " + f));
                if (accepted.Count > 0) text += $"\nAccepted: {string.Join(", ", accepted)}";
                _verdictText.Text = text;
            }
        }
        catch
        {
            _verdictText.Text = "Failed to read Roblox logs.";
        }
    }

    private void ResetFlags_Click(object sender, RoutedEventArgs e)
    {
        _flagsStatus.Text = RobloxLauncher.ResetFlags();
    }

    private static string? FindRobloxCursorsFolder()
    {
        var list = FindAllRobloxCursorsFolders();
        return list.FirstOrDefault();
    }

    internal static List<string> FindAllRobloxCursorsFolders()
    {
        var versionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
        var result = new List<string>();
        if (!Directory.Exists(versionsDir)) return result;
        foreach (var dir in Directory.GetDirectories(versionsDir).OrderByDescending(d => File.GetLastWriteTime(Path.Combine(d, "RobloxPlayerBeta.exe"))))
        {
            var cursorPath = Path.Combine(dir, "content", "textures", "Cursors", "KeyboardMouse");
            if (Directory.Exists(cursorPath) && File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe")))
                result.Add(cursorPath);
        }
        return result;
    }

    internal static void EnsureMigratedBackups()
    {
        // Migrate old per-version NaxiBackup/Cursors -> central CursorBackupDir
        try
        {
            var versionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
            if (!Directory.Exists(versionsDir)) return;
            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                var oldBackup = Path.Combine(dir, "content", "NaxiBackup", "Cursors");
                if (!Directory.Exists(oldBackup)) continue;
                Directory.CreateDirectory(CursorBackupDir);
                foreach (var f in Directory.GetFiles(oldBackup, "*.png"))
                {
                    var dest = Path.Combine(CursorBackupDir, Path.GetFileName(f));
                    if (!File.Exists(dest)) File.Copy(f, dest, false);
                }
            }
        } catch { }
    }

    private static void MigrateCustomizationsIfNeeded(AppConfig config, string oldVersion, string newVersion)
    {
        try
        {
            var versionsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
            var oldRoot = Path.Combine(versionsRoot, oldVersion);
            var newRoot = Path.Combine(versionsRoot, newVersion);
            if (!Directory.Exists(newRoot)) return;

            // --- Cursors: reapply from central config/cache or copy from old version ---
            try
            {
                var newCursors = Path.Combine(newRoot, "content", "textures", "Cursors", "KeyboardMouse");
                var newTex = Path.Combine(newRoot, "content", "textures");
                if (Directory.Exists(newCursors))
                {
                    // Prefer config paths (user's custom PNGs)
                    bool hasConfig = !string.IsNullOrEmpty(config.NormalCursorPath) || !string.IsNullOrEmpty(config.PointingCursorPath) || !string.IsNullOrEmpty(config.ShiftCursorPath) || !string.IsNullOrEmpty(config.IBeamCursorPath);
                    if (hasConfig)
                    {
                        if (!string.IsNullOrEmpty(config.NormalCursorPath) && File.Exists(config.NormalCursorPath))
                            File.Copy(config.NormalCursorPath, Path.Combine(newCursors, "ArrowFarCursor.png"), true);
                        if (!string.IsNullOrEmpty(config.PointingCursorPath) && File.Exists(config.PointingCursorPath))
                            File.Copy(config.PointingCursorPath, Path.Combine(newCursors, "ArrowCursor.png"), true);
                        if (!string.IsNullOrEmpty(config.ShiftCursorPath) && File.Exists(config.ShiftCursorPath))
                        {
                            File.Copy(config.ShiftCursorPath, Path.Combine(newCursors, "MouseLockedCursor.png"), true);
                            var destTex = Path.Combine(newTex, "MouseLockedCursor.png");
                            File.Copy(config.ShiftCursorPath, destTex, true);
                        }
                        if (!string.IsNullOrEmpty(config.IBeamCursorPath) && File.Exists(config.IBeamCursorPath))
                            File.Copy(config.IBeamCursorPath, Path.Combine(newCursors, "IBeamCursor.png"), true);
                    }
                    else if (Directory.Exists(oldRoot))
                    {
                        // Fallback: copy customized cursors from old version if they differ from backup (i.e. were customized)
                        var oldCursors = Path.Combine(oldRoot, "content", "textures", "Cursors", "KeyboardMouse");
                        var oldTex = Path.Combine(oldRoot, "content", "textures");
                        if (Directory.Exists(oldCursors))
                        {
                            foreach (var name in new[] { "ArrowFarCursor.png", "ArrowCursor.png", "MouseLockedCursor.png", "IBeamCursor.png" })
                            {
                                var oldFile = Path.Combine(oldCursors, name);
                                var newFile = Path.Combine(newCursors, name);
                                var backupFile = Path.Combine(CursorBackupDir, name);
                                if (!File.Exists(oldFile)) continue;
                                bool isCustom = !File.Exists(backupFile) || new FileInfo(oldFile).Length != new FileInfo(backupFile).Length;
                                // also compare content if same length but different: assume custom if old differs from current new default
                                if (isCustom && File.Exists(newFile))
                                {
                                    // if new is default (same as backup or freshly installed), overwrite with old custom
                                    File.Copy(oldFile, newFile, true);
                                }
                            }
                            var oldTexFile = Path.Combine(oldTex, "MouseLockedCursor.png");
                            var newTexFile = Path.Combine(newTex, "MouseLockedCursor.png");
                            if (File.Exists(oldTexFile) && File.Exists(newTexFile))
                            {
                                var bak = Path.Combine(CursorBackupDir, "MouseLockedCursor.png");
                                bool isCustom = !File.Exists(bak) || new FileInfo(oldTexFile).Length != new FileInfo(bak).Length;
                                if (isCustom) File.Copy(oldTexFile, newTexFile, true);
                            }
                        }
                    }
                }
            } catch { }

            // --- Sky: if we have cached custom sky, reapply; else copy from old version ---
            try
            {
                var newSky = Path.Combine(newRoot, "PlatformContent", "pc", "textures", "sky");
                var oldSky = Path.Combine(oldRoot, "PlatformContent", "pc", "textures", "sky");
                if (Directory.Exists(CustomSkyCacheDir) && Directory.GetFiles(CustomSkyCacheDir, "*.tex").Length > 0 && Directory.Exists(newSky))
                {
                    foreach (var f in Directory.GetFiles(CustomSkyCacheDir, "*.tex"))
                        File.Copy(f, Path.Combine(newSky, Path.GetFileName(f)), true);
                }
                else if (Directory.Exists(oldSky) && Directory.Exists(newSky) && Directory.Exists(SkyBackupDir) && Directory.GetFiles(SkyBackupDir).Length > 0)
                {
                    var hasCustom = Directory.GetFiles(oldSky, "*.tex").Any(f => {
                        var bak = Path.Combine(SkyBackupDir, Path.GetFileName(f));
                        return File.Exists(bak) && new FileInfo(f).Length != new FileInfo(bak).Length;
                    });
                    if (hasCustom)
                    {
                        foreach (var f in Directory.GetFiles(oldSky, "*.tex"))
                            File.Copy(f, Path.Combine(newSky, Path.GetFileName(f)), true);
                    }
                }
            } catch { }

            // --- Font: same logic ---
            try
            {
                var newFonts = Path.Combine(newRoot, "Content", "fonts");
                var oldFonts = Path.Combine(oldRoot, "Content", "fonts");
                if (File.Exists(CustomFontPath_File) && Directory.Exists(newFonts))
                {
                    var targets = Directory.GetFiles(newFonts, "*.*").Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (var t in targets) File.Copy(CustomFontPath_File, t, true);
                }
                else if (Directory.Exists(oldFonts) && Directory.Exists(newFonts) && Directory.Exists(FontBackupDir) && Directory.GetFiles(FontBackupDir).Length > 0)
                {
                    var oldFontFiles = Directory.GetFiles(oldFonts, "*.*").Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)).ToArray();
                    var hasCustom = oldFontFiles.Any(f => {
                        var bak = Path.Combine(FontBackupDir, Path.GetFileName(f));
                        return File.Exists(bak) && new FileInfo(f).Length != new FileInfo(bak).Length;
                    });
                    if (hasCustom)
                    {
                        foreach (var f in oldFontFiles)
                            File.Copy(f, Path.Combine(newFonts, Path.GetFileName(f)), true);
                    }
                }
            } catch { }

            // --- Emote wheel: reapply from cache or copy from old version ---
            try
            {
                var newEmotes = Path.Combine(newRoot, "content", "textures", "ui", "Emotes", "Large");
                var oldEmotes = Path.Combine(oldRoot, "content", "textures", "ui", "Emotes", "Large");
                if (!Directory.Exists(newEmotes)) return;

                string ResolveEmote(string cfgPath, string cacheName)
                {
                    if (!string.IsNullOrEmpty(cfgPath) && File.Exists(cfgPath)) return cfgPath;
                    var cached = Path.Combine(CustomEmoteCacheDir, cacheName);
                    if (File.Exists(cached)) return cached;
                    return "";
                }

                bool hasConfig = !string.IsNullOrEmpty(config.EmoteCircleBgPath) || !string.IsNullOrEmpty(config.EmoteSegmentedPath) || !string.IsNullOrEmpty(config.EmoteGradientPath) || !string.IsNullOrEmpty(config.EmoteSelectedLinePath);
                if (hasConfig)
                {
                    var map = new[] { ("CircleBackground", config.EmoteCircleBgPath), ("SegmentedCircle", config.EmoteSegmentedPath), ("SelectedGradient", config.EmoteGradientPath), ("SelectedLine", config.EmoteSelectedLinePath) };
                    foreach (var (baseName, cfgVal) in map)
                    {
                        var src = ResolveEmote(cfgVal, $"{baseName}.png");
                        if (!string.IsNullOrEmpty(src))
                            foreach (var s in EmoteSuffixes) File.Copy(src, Path.Combine(newEmotes, $"{baseName}{s}.png"), true);
                    }
                }
                else if (Directory.Exists(oldEmotes) && Directory.Exists(EmoteBackupDir))
                {
                    // Check if old version was customized by comparing file sizes with backup
                    bool isCustom = false;
                    foreach (var baseName in EmoteFileNames)
                    {
                        var oldFile = Path.Combine(oldEmotes, $"{baseName}.png");
                        var bak = Path.Combine(EmoteBackupDir, $"{baseName}.png");
                        if (File.Exists(oldFile) && File.Exists(bak) && new FileInfo(oldFile).Length != new FileInfo(bak).Length) { isCustom = true; break; }
                    }
                    if (isCustom)
                    {
                        foreach (var baseName in EmoteFileNames)
                            foreach (var s in EmoteSuffixes)
                            {
                                var src = Path.Combine(oldEmotes, $"{baseName}{s}.png");
                                var dest = Path.Combine(newEmotes, $"{baseName}{s}.png");
                                if (File.Exists(src)) File.Copy(src, dest, true);
                            }
                    }
                }
            } catch { }
        } catch { }
    }

    private void ApplySavedCursors()
    {
        EnsureMigratedBackups();
        var allDirs = FindAllRobloxCursorsFolders();
        if (allDirs.Count == 0) return;
        // create central backup if missing
        try
        {
            Directory.CreateDirectory(CursorBackupDir);
            foreach (var cursorsDir in allDirs)
            {
                var texFile = Path.Combine(cursorsDir, "..", "..", "MouseLockedCursor.png");
                foreach (var (name, src) in new[] { ("ArrowFarCursor.png", Path.Combine(cursorsDir, "ArrowFarCursor.png")), ("ArrowCursor.png", Path.Combine(cursorsDir, "ArrowCursor.png")), ("MouseLockedCursor.png", Path.Combine(cursorsDir, "MouseLockedCursor.png")), ("IBeamCursor.png", Path.Combine(cursorsDir, "IBeamCursor.png")) })
                {
                    var bak = Path.Combine(CursorBackupDir, name);
                    if (!File.Exists(bak) && File.Exists(src)) File.Copy(src, bak, false);
                }
                if (File.Exists(texFile))
                {
                    var bak = Path.Combine(CursorBackupDir, "MouseLockedCursor.png");
                    if (!File.Exists(bak)) File.Copy(texFile, bak, false);
                }
            }
        } catch { }

        foreach (var cursorsDir in allDirs)
        {
            try
            {
                string ResolveCursor(string cfgPath, string cacheName)
                {
                    if (!string.IsNullOrEmpty(cfgPath) && File.Exists(cfgPath)) return cfgPath;
                    var cached = Path.Combine(CustomCursorCacheDir, cacheName);
                    if (File.Exists(cached)) return cached;
                    return "";
                }
                var normalSrc = ResolveCursor(_config.NormalCursorPath, "ArrowFarCursor.png");
                if (!string.IsNullOrEmpty(normalSrc)) File.Copy(normalSrc, Path.Combine(cursorsDir, "ArrowFarCursor.png"), true);
                var pointSrc = ResolveCursor(_config.PointingCursorPath, "ArrowCursor.png");
                if (!string.IsNullOrEmpty(pointSrc)) File.Copy(pointSrc, Path.Combine(cursorsDir, "ArrowCursor.png"), true);
                var shiftSrc = ResolveCursor(_config.ShiftCursorPath, "MouseLockedCursor.png");
                if (!string.IsNullOrEmpty(shiftSrc))
                {
                    File.Copy(shiftSrc, Path.Combine(cursorsDir, "MouseLockedCursor.png"), true);
                    var destTex = Path.Combine(cursorsDir, "..", "..", "MouseLockedCursor.png");
                    File.Copy(shiftSrc, destTex, true);
                }
                var ibeamSrc = ResolveCursor(_config.IBeamCursorPath, "IBeamCursor.png");
                if (!string.IsNullOrEmpty(ibeamSrc)) File.Copy(ibeamSrc, Path.Combine(cursorsDir, "IBeamCursor.png"), true);
            } catch { }
        }
    }

    private Grid BuildCursors()
    {
        // recreate preview images to avoid "already the logical child" error on rebuild
        _normalCursorPreview = new Image();
        _pointingCursorPreview = new Image();
        _shiftCursorPreview = new Image();
        _ibeamCursorPreview = new Image();
        _normalCursorPath = new TextBlock();
        _pointingCursorPath = new TextBlock();
        _shiftCursorPath = new TextBlock();
        _ibeamCursorPath = new TextBlock();
        _cursorStatus = new TextBlock();
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel();
        title.Children.Add(T("Cursors", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        host.Children.Add(T("Replace Roblox cursors with custom PNG images. Originals are backed up automatically.", 13, (Brush)FindResource("Muted"), false));

        var cursorsDir = FindRobloxCursorsFolder();
        if (cursorsDir == null)
        {
            host.Children.Add(T("Roblox installation not found.", 14, (Brush)FindResource("Muted"), false));
            var scroll2 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
            SmoothScroll.SetEnabled(scroll2, true);
            Grid.SetRow(scroll2, 1); g.Children.Add(scroll2);
            return g;
        }

        string ResolveShow(string cfgPath, string cacheName)
        {
            if (!string.IsNullOrEmpty(cfgPath) && File.Exists(cfgPath)) return cfgPath;
            var cached = Path.Combine(CustomCursorCacheDir, cacheName);
            if (File.Exists(cached)) return cached;
            return "";
        }
        var n = ResolveShow(_config.NormalCursorPath, "ArrowFarCursor.png");
        if (!string.IsNullOrEmpty(n)) _normalCursorFile = n;
        var p = ResolveShow(_config.PointingCursorPath, "ArrowCursor.png");
        if (!string.IsNullOrEmpty(p)) _pointingCursorFile = p;
        var s = ResolveShow(_config.ShiftCursorPath, "MouseLockedCursor.png");
        if (!string.IsNullOrEmpty(s)) _shiftCursorFile = s;
        var ib = ResolveShow(_config.IBeamCursorPath, "IBeamCursor.png");
        if (!string.IsNullOrEmpty(ib)) _ibeamCursorFile = ib;

        var normalCard = CursorCard("Normal Cursor", "ArrowFarCursor.png — default pointer", () => _normalCursorFile, v => _normalCursorFile = v, v => _config.NormalCursorPath = v, () => _normalCursorPath, _normalCursorPreview, cursorsDir);
        host.Children.Add(normalCard);

        var pointingCard = CursorCard("Pointing Cursor", "ArrowCursor.png — hover over buttons", () => _pointingCursorFile, v => _pointingCursorFile = v, v => _config.PointingCursorPath = v, () => _pointingCursorPath, _pointingCursorPreview, cursorsDir);
        host.Children.Add(pointingCard);

        var shiftCard = CursorCard("Shift-Lock Cursor", "MouseLockedCursor.png — crosshair when shift-locked", () => _shiftCursorFile, v => _shiftCursorFile = v, v => _config.ShiftCursorPath = v, () => _shiftCursorPath, _shiftCursorPreview, Path.Combine(cursorsDir, "..", ".."));
        host.Children.Add(shiftCard);

        var ibeamCard = CursorCard("IBeam Cursor", "IBeamCursor.png — text input cursor", () => _ibeamCursorFile, v => _ibeamCursorFile = v, v => _config.IBeamCursorPath = v, () => _ibeamCursorPath, _ibeamCursorPreview, cursorsDir);
        host.Children.Add(ibeamCard);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        var applyBtn = Btn("Apply Cursors", true, 180);
        applyBtn.Click += ApplyCursors_Click;
        var resetBtn = Btn("Reset to Default", false, 180);
        resetBtn.Margin = new Thickness(12, 0, 0, 0);
        resetBtn.Click += ResetCursors_Click;
        _cursorStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        _cursorStatus.VerticalAlignment = VerticalAlignment.Center;
        _cursorStatus.Margin = new Thickness(14, 0, 0, 0);
        btnRow.Children.Add(applyBtn);
        btnRow.Children.Add(resetBtn);
        btnRow.Children.Add(_cursorStatus);
        host.Children.Add(btnRow);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);

        RefreshCursorPreviews();

        return g;
    }

    private Border CursorCard(string title, string desc, Func<string?> getFilePath, Action<string> setFilePath, Action<string> saveToConfig, Func<TextBlock?> getPathText, Image preview, string cursorsDir)
    {
        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(22), Margin = new Thickness(0, 12, 0, 0) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var previewBorder = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)), ClipToBounds = true };
        preview.Width = 48; preview.Height = 48; preview.Stretch = Stretch.UniformToFill;
        var currentFile = getFilePath();
        if (currentFile != null)
            previewBorder.Child = preview;
        else
            previewBorder.Child = new TextBlock { Text = "?", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Muted") };
        Grid.SetColumn(previewBorder, 0); grid.Children.Add(previewBorder);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        info.Children.Add(T(title, 15, (Brush)FindResource("Text"), true));
        info.Children.Add(T(desc, 12, (Brush)FindResource("Muted"), false, 3));
        var pathText = T(currentFile != null ? Path.GetFileName(currentFile) : "Default", 11.5, (Brush)FindResource("Muted"), false);
        info.Children.Add(pathText);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var importBtn = Btn("Import", false, 100);
        importBtn.Click += (_, _) =>
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "PNG files (*.png)|*.png", Title = $"Select {title}" };
            if (ofd.ShowDialog() == true)
            {
                setFilePath(ofd.FileName);
                saveToConfig(ofd.FileName);
                RobloxLauncher.SaveConfig(_config);
                try
                {
                    // cache copy so it survives even if original deleted and survives Roblox updates
                    Directory.CreateDirectory(CustomCursorCacheDir);
                    string cacheName = title.Contains("Normal") ? "ArrowFarCursor.png" : title.Contains("Pointing") ? "ArrowCursor.png" : title.Contains("IBeam") ? "IBeamCursor.png" : "MouseLockedCursor.png";
                    File.Copy(ofd.FileName, Path.Combine(CustomCursorCacheDir, cacheName), true);
                } catch { }
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(ofd.FileName);
                    bmp.EndInit();
                    bmp.Freeze();
                    preview.Source = bmp;
                    previewBorder.Child = preview;
                } catch { }
                pathText.Text = Path.GetFileName(ofd.FileName);
                pathText.Foreground = (Brush)FindResource("Text");
            }
        };
        btnRow.Children.Add(importBtn);
        info.Children.Add(btnRow);

        Grid.SetColumn(info, 1); grid.Children.Add(info);
        card.Child = grid;
        return card;
    }

    private void RefreshCursorPreviews()
    {
        var cursorsDir = FindRobloxCursorsFolder();
        if (cursorsDir == null) return;

        LoadCursorPreview(_normalCursorPreview, Path.Combine(cursorsDir, "ArrowFarCursor.png"), _normalCursorFile);
        LoadCursorPreview(_pointingCursorPreview, Path.Combine(cursorsDir, "ArrowCursor.png"), _pointingCursorFile);
        LoadCursorPreview(_shiftCursorPreview, Path.Combine(cursorsDir, "MouseLockedCursor.png"), _shiftCursorFile);
        LoadCursorPreview(_ibeamCursorPreview, Path.Combine(cursorsDir, "IBeamCursor.png"), _ibeamCursorFile);
    }

    private void LoadCursorPreview(Image preview, string defaultPath, string? customFile)
    {
        try
        {
            var path = customFile ?? defaultPath;
            if (!File.Exists(path)) return;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            preview.Source = bmp;
        } catch { }
    }

    private void LoadEmotePreview(Image preview, string? customFile)
    {
        if (string.IsNullOrEmpty(customFile)) return;
        try
        {
            if (!File.Exists(customFile)) return;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(customFile);
            bmp.EndInit();
            bmp.Freeze();
            preview.Source = bmp;
        } catch { }
    }

    private void ApplyCursors_Click(object sender, RoutedEventArgs e)
    {
        var cursorsDir = FindRobloxCursorsFolder();
        if (cursorsDir == null) { _cursorStatus.Text = Localization.T("Roblox not found"); return; }
        ApplyCursors(cursorsDir);
    }

    private void ApplyCursors(string cursorsDir)
    {
        try
        {
            EnsureMigratedBackups();
            Directory.CreateDirectory(CursorBackupDir);
            // ensure backup exists from first found version
            var firstBackupDone = false;
            foreach (var dir in FindAllRobloxCursorsFolders())
            {
                if (firstBackupDone) break;
                var tex = Path.Combine(dir, "..", "..", "MouseLockedCursor.png");
                foreach (var (name, src) in new[] { ("ArrowFarCursor.png", Path.Combine(dir, "ArrowFarCursor.png")), ("ArrowCursor.png", Path.Combine(dir, "ArrowCursor.png")), ("MouseLockedCursor.png", Path.Combine(dir, "MouseLockedCursor.png")) })
                {
                    var bak = Path.Combine(CursorBackupDir, name);
                    if (!File.Exists(bak) && File.Exists(src)) File.Copy(src, bak, false);
                }
                if (File.Exists(tex))
                {
                    var bak = Path.Combine(CursorBackupDir, "MouseLockedCursor.png");
                    if (!File.Exists(bak)) File.Copy(tex, bak, false);
                }
                firstBackupDone = true;
            }

            int applied = 0;
            // save config cursors already done on Import, but ensure central cache: copy source files to AppData for safety if original deleted later? We keep path as-is.
            var allDirs = FindAllRobloxCursorsFolders();
            if (allDirs.Count == 0) allDirs = new List<string> { cursorsDir };
            foreach (var dir in allDirs)
            {
                if (_normalCursorFile != null && File.Exists(_normalCursorFile))
                {
                    File.Copy(_normalCursorFile, Path.Combine(dir, "ArrowFarCursor.png"), true);
                }
                if (_pointingCursorFile != null && File.Exists(_pointingCursorFile))
                {
                    File.Copy(_pointingCursorFile, Path.Combine(dir, "ArrowCursor.png"), true);
                }
                if (_shiftCursorFile != null && File.Exists(_shiftCursorFile))
                {
                    File.Copy(_shiftCursorFile, Path.Combine(dir, "MouseLockedCursor.png"), true);
                    var destTex = Path.Combine(dir, "..", "..", "MouseLockedCursor.png");
                    File.Copy(_shiftCursorFile, destTex, true);
                }
            }
            if (_normalCursorFile != null && File.Exists(_normalCursorFile)) applied++;
            if (_pointingCursorFile != null && File.Exists(_pointingCursorFile)) applied++;
            if (_shiftCursorFile != null && File.Exists(_shiftCursorFile)) applied++;

            if (_cursorStatus != null)
            {
                _cursorStatus.Text = applied > 0 ? $"Applied {applied} cursor(s) to {allDirs.Count} version(s)" : "No cursors selected";
                _cursorStatus.Foreground = (Brush)FindResource("Accent");
            }
            // persist version after apply
            var curr = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
            if (curr != null) { _config.RobloxVersion = Path.GetFileName(curr); RobloxLauncher.SaveConfig(_config); }
        }
        catch (Exception ex) { if (_cursorStatus != null) _cursorStatus.Text = $"Error: {ex.Message}"; }
    }

    internal static bool TryExtractEmbeddedDefault(string resourceName, string destPath)
    {
        try
        {
            var asm = typeof(MainWindow).Assembly;
            var fullName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
            if (fullName == null) return false;
            using var s = asm.GetManifestResourceStream(fullName);
            if (s == null || s.Length == 0) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var fs = File.Create(destPath);
            s.CopyTo(fs);
            return true;
        } catch { return false; }
    }

    internal static void ApplyCursorSetForLaunch(AppConfig config, string? normal, string? pointing, string? shift)
    {
        try
        {
            string? cn = string.IsNullOrWhiteSpace(normal) ? NullIfEmpty(config.NormalCursorPath) : normal;
            string? cp = string.IsNullOrWhiteSpace(pointing) ? NullIfEmpty(config.PointingCursorPath) : pointing;
            string? cs = string.IsNullOrWhiteSpace(shift) ? NullIfEmpty(config.ShiftCursorPath) : shift;

            EnsureMigratedBackups();
            Directory.CreateDirectory(CursorBackupDir);
            bool firstBackupDone = false;
            foreach (var dir in FindAllRobloxCursorsFolders())
            {
                if (firstBackupDone) break;
                var tex = Path.Combine(dir, "..", "..", "MouseLockedCursor.png");
                foreach (var (name, srcPath) in new[] { ("ArrowFarCursor.png", Path.Combine(dir, "ArrowFarCursor.png")), ("ArrowCursor.png", Path.Combine(dir, "ArrowCursor.png")), ("MouseLockedCursor.png", Path.Combine(dir, "MouseLockedCursor.png")) })
                {
                    var bak = Path.Combine(CursorBackupDir, name);
                    if (!File.Exists(bak) && File.Exists(srcPath)) File.Copy(srcPath, bak, false);
                }
                if (File.Exists(tex))
                {
                    var bak = Path.Combine(CursorBackupDir, "MouseLockedCursor.png");
                    if (!File.Exists(bak)) File.Copy(tex, bak, false);
                }
                firstBackupDone = true;
            }

            foreach (var dir in FindAllRobloxCursorsFolders())
            {
                ApplyOneCursor(cn, Path.Combine(dir, "ArrowFarCursor.png"));
                ApplyOneCursor(cp, Path.Combine(dir, "ArrowCursor.png"));
                ApplyOneCursor(cs, Path.Combine(dir, "MouseLockedCursor.png"));
                ApplyOneCursor(cs, Path.Combine(dir, "..", "..", "MouseLockedCursor.png"));
            }

            var curr = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
            if (curr != null) { config.RobloxVersion = Path.GetFileName(curr); RobloxLauncher.SaveConfig(config); }
        }
        catch { }
    }

    static string? NullIfEmpty(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    static void ApplyOneCursor(string? src, string dest)
    {
        if (!string.IsNullOrEmpty(src) && File.Exists(src)) { File.Copy(src, dest, true); return; }
        if (!TryExtractEmbeddedDefault(Path.GetFileName(dest), dest))
        {
            if (File.Exists(dest)) File.Delete(dest);
        }
    }

    private void ResetCursors_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var allDirs = FindAllRobloxCursorsFolders();
            if (allDirs.Count == 0) { _cursorStatus.Text = Localization.T("Roblox not found"); return; }

            int restored = 0;
            int deleted = 0;
            foreach (var dir in allDirs)
            {
                try
                {
                    var map = new[]
                    {
                        (res: "ArrowFarCursor.png", dest: Path.Combine(dir, "ArrowFarCursor.png")),
                        (res: "ArrowCursor.png", dest: Path.Combine(dir, "ArrowCursor.png")),
                        (res: "MouseLockedCursor.png", dest: Path.Combine(dir, "MouseLockedCursor.png")),
                        (res: "IBeamCursor.png", dest: Path.Combine(dir, "IBeamCursor.png")),
                    };
                    foreach (var (res, dest) in map)
                    {
                        if (TryExtractEmbeddedDefault(res, dest)) restored++;
                        else if (File.Exists(dest)) { File.Delete(dest); deleted++; }
                    }
                    var texDest = Path.Combine(dir, "..", "..", "MouseLockedCursor.png");
                    if (TryExtractEmbeddedDefault("MouseLockedCursor.png", texDest)) restored++;
                    else if (File.Exists(texDest)) { File.Delete(texDest); deleted++; }
                } catch { }
            }
            string statusMsg = restored > 0
                ? $"Restored default cursors in {allDirs.Count} version(s) ({restored} files)"
                : deleted > 0 ? $"Removed {deleted} file(s) in {allDirs.Count} version(s) — defaults will restore on next Roblox launch"
                : "No custom cursors found";

            _normalCursorFile = null; _pointingCursorFile = null; _shiftCursorFile = null; _ibeamCursorFile = null;
            _config.NormalCursorPath = ""; _config.PointingCursorPath = ""; _config.ShiftCursorPath = ""; _config.IBeamCursorPath = "";
            try { if (Directory.Exists(CustomCursorCacheDir)) foreach (var f in Directory.GetFiles(CustomCursorCacheDir, "*.png")) File.Delete(f); } catch { }
            RobloxLauncher.SaveConfig(_config);
            _cursors = BuildCursors();
            PageHost.Content = _cursors;
            _cursorStatus.Text = statusMsg;
            _cursorStatus.Foreground = (Brush)FindResource("Accent");
        }
        catch (Exception ex) { _cursorStatus.Text = $"Error: {ex.Message}"; }
    }

    private static string? FindRobloxEmotesFolder()
    {
        var versionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
        if (!Directory.Exists(versionsDir)) return null;
        string? newest = null;
        var newestTime = DateTime.MinValue;
        foreach (var dir in Directory.GetDirectories(versionsDir))
        {
            var emotesDir = Path.Combine(dir, "content", "textures", "ui", "Emotes", "Large");
            if (!Directory.Exists(emotesDir)) continue;
            var exe = Path.Combine(dir, "RobloxPlayerBeta.exe");
            if (!File.Exists(exe)) continue;
            var time = File.GetLastWriteTime(exe);
            if (time > newestTime) { newestTime = time; newest = emotesDir; }
        }
        return newest;
    }

    private static List<string> FindAllRobloxEmotesFolders()
    {
        var versionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
        var result = new List<string>();
        if (!Directory.Exists(versionsDir)) return result;
        foreach (var dir in Directory.GetDirectories(versionsDir).OrderByDescending(d => File.GetLastWriteTime(Path.Combine(d, "RobloxPlayerBeta.exe"))))
        {
            var emotesDir = Path.Combine(dir, "content", "textures", "ui", "Emotes", "Large");
            if (Directory.Exists(emotesDir) && File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe")))
                result.Add(emotesDir);
        }
        return result;
    }

    private static readonly string[] EmoteFileNames = { "CircleBackground", "SegmentedCircle", "SelectedGradient", "SelectedLine" };
    private static readonly string[] EmoteSuffixes = { "", "@2x", "@3x" };

    private static bool TryExtractEmbeddedEmote(string baseName, string suffix, string destPath)
    {
        try
        {
            var asm = typeof(MainWindow).Assembly;
            var resourceName = $"NaxiBootstrap.Assets.DefaultEmotes.{baseName}{suffix}.png";
            var fullName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($"{baseName}{suffix}.png", StringComparison.OrdinalIgnoreCase));
            if (fullName == null) return false;
            using var s = asm.GetManifestResourceStream(fullName);
            if (s == null || s.Length == 0) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var fs = File.Create(destPath);
            s.CopyTo(fs);
            return true;
        } catch { return false; }
    }

    private void EnsureEmoteBackups()
    {
        try
        {
            var emotesDir = FindRobloxEmotesFolder();
            if (emotesDir == null) return;
            Directory.CreateDirectory(EmoteBackupDir);
            foreach (var baseName in EmoteFileNames)
            {
                foreach (var suffix in EmoteSuffixes)
                {
                    var file = Path.Combine(emotesDir, $"{baseName}{suffix}.png");
                    var bak = Path.Combine(EmoteBackupDir, $"{baseName}{suffix}.png");
                    if (!File.Exists(bak) && File.Exists(file)) File.Copy(file, bak, false);
                }
            }
        } catch { }
    }

    private void ApplySavedEmotes()
    {
        EnsureEmoteBackups();
        var allDirs = FindAllRobloxEmotesFolders();
        if (allDirs.Count == 0) return;

        string Resolve(string cfgPath, string cacheName)
        {
            if (!string.IsNullOrEmpty(cfgPath) && File.Exists(cfgPath)) return cfgPath;
            var cached = Path.Combine(CustomEmoteCacheDir, cacheName);
            if (File.Exists(cached)) return cached;
            return "";
        }

        foreach (var dir in allDirs)
        {
            try
            {
                var bgSrc = Resolve(_config.EmoteCircleBgPath, "CircleBackground.png");
                if (!string.IsNullOrEmpty(bgSrc))
                    foreach (var s in EmoteSuffixes)
                        File.Copy(bgSrc, Path.Combine(dir, $"CircleBackground{s}.png"), true);

                var segSrc = Resolve(_config.EmoteSegmentedPath, "SegmentedCircle.png");
                if (!string.IsNullOrEmpty(segSrc))
                    foreach (var s in EmoteSuffixes)
                        File.Copy(segSrc, Path.Combine(dir, $"SegmentedCircle{s}.png"), true);

                var gradSrc = Resolve(_config.EmoteGradientPath, "SelectedGradient.png");
                if (!string.IsNullOrEmpty(gradSrc))
                    foreach (var s in EmoteSuffixes)
                        File.Copy(gradSrc, Path.Combine(dir, $"SelectedGradient{s}.png"), true);

                var lineSrc = Resolve(_config.EmoteSelectedLinePath, "SelectedLine.png");
                if (!string.IsNullOrEmpty(lineSrc))
                    foreach (var s in EmoteSuffixes)
                        File.Copy(lineSrc, Path.Combine(dir, $"SelectedLine{s}.png"), true);
            } catch { }
        }
    }

    private Grid BuildEmotes()
    {
        _emoteCircleBgPreview = new Image();
        _emoteSegmentedPreview = new Image();
        _emoteGradientPreview = new Image();
        _emoteSelectedLinePreview = new Image();
        _emoteStatus = new TextBlock();
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(T("Emote Wheel", 26, (Brush)FindResource("Text"), true));
        if (!HasPro)
        {
            var proBadge = new Border
            {
                Background = (Brush)FindResource("AccentGradient"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 4, 0, 0),
                Child = new TextBlock { Text = "PRO", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("AccentText") }
            };
            title.Children.Add(proBadge);
        }
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        host.Children.Add(T("Replace Roblox emote wheel textures with custom PNG images. Upload your own base image — @2x and @3x variants are applied automatically.", 13, (Brush)FindResource("Muted"), false));
        if (!HasPro) host.Children.Add(T("Customizing the emote wheel requires Pro.", 12.5, (Brush)FindResource("Accent"), false, 4));

        var emotesDir = FindRobloxEmotesFolder();
        if (emotesDir == null)
        {
            host.Children.Add(T("Roblox installation not found.", 14, (Brush)FindResource("Muted"), false));
            var scroll2 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
            SmoothScroll.SetEnabled(scroll2, true);
            Grid.SetRow(scroll2, 1); g.Children.Add(scroll2);
            return g;
        }

        string ResolveShow(string cfgPath, string cacheName)
        {
            if (!string.IsNullOrEmpty(cfgPath) && File.Exists(cfgPath)) return cfgPath;
            var cached = Path.Combine(CustomEmoteCacheDir, cacheName);
            if (File.Exists(cached)) return cached;
            return "";
        }
        var bg = ResolveShow(_config.EmoteCircleBgPath, "CircleBackground.png");
        if (!string.IsNullOrEmpty(bg)) _emoteCircleBgFile = bg;
        var seg = ResolveShow(_config.EmoteSegmentedPath, "SegmentedCircle.png");
        if (!string.IsNullOrEmpty(seg)) _emoteSegmentedFile = seg;
        var grad = ResolveShow(_config.EmoteGradientPath, "SelectedGradient.png");
        if (!string.IsNullOrEmpty(grad)) _emoteGradientFile = grad;
        var line = ResolveShow(_config.EmoteSelectedLinePath, "SelectedLine.png");
        if (!string.IsNullOrEmpty(line)) _emoteSelectedLineFile = line;

        LoadEmotePreview(_emoteCircleBgPreview, _emoteCircleBgFile);
        LoadEmotePreview(_emoteSegmentedPreview, _emoteSegmentedFile);
        LoadEmotePreview(_emoteGradientPreview, _emoteGradientFile);
        LoadEmotePreview(_emoteSelectedLinePreview, _emoteSelectedLineFile);

        host.Children.Add(EmoteCard("Circle Background", "CircleBackground.png — background of the emote ring", () => _emoteCircleBgFile, v => _emoteCircleBgFile = v, v => _config.EmoteCircleBgPath = v, _emoteCircleBgPreview));
        host.Children.Add(EmoteCard("Segmented Circle", "SegmentedCircle.png — dividing lines between slots", () => _emoteSegmentedFile, v => _emoteSegmentedFile = v, v => _config.EmoteSegmentedPath = v, _emoteSegmentedPreview));
        host.Children.Add(EmoteCard("Selected Gradient", "SelectedGradient.png — highlight on active slot", () => _emoteGradientFile, v => _emoteGradientFile = v, v => _config.EmoteGradientPath = v, _emoteGradientPreview));
        host.Children.Add(EmoteCard("Selected Line", "SelectedLine.png — thin line indicator", () => _emoteSelectedLineFile, v => _emoteSelectedLineFile = v, v => _config.EmoteSelectedLinePath = v, _emoteSelectedLinePreview));

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        var applyBtn = Btn(HasPro ? "Apply Emotes" : "🔒 Apply Emotes", true, 180);
        applyBtn.Click += ApplyEmotes_Click;
        var resetBtn = Btn("Reset to Default", false, 180);
        resetBtn.Margin = new Thickness(12, 0, 0, 0);
        resetBtn.Click += ResetEmotes_Click;
        _emoteStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        _emoteStatus.VerticalAlignment = VerticalAlignment.Center;
        _emoteStatus.Margin = new Thickness(14, 0, 0, 0);
        btnRow.Children.Add(applyBtn);
        btnRow.Children.Add(resetBtn);
        btnRow.Children.Add(_emoteStatus);
        host.Children.Add(btnRow);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);
        return g;
    }

    private Border EmoteCard(string title, string desc, Func<string?> getFilePath, Action<string> setFilePath, Action<string> saveToConfig, Image preview)
    {
        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(22), Margin = new Thickness(0, 12, 0, 0) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var previewBorder = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)), ClipToBounds = true };
        preview.Width = 48; preview.Height = 48; preview.Stretch = Stretch.UniformToFill;
        var currentFile = getFilePath();
        if (currentFile != null)
            previewBorder.Child = preview;
        else
            previewBorder.Child = new TextBlock { Text = "?", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Muted") };
        Grid.SetColumn(previewBorder, 0); grid.Children.Add(previewBorder);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        info.Children.Add(T(title, 15, (Brush)FindResource("Text"), true));
        info.Children.Add(T(desc, 12, (Brush)FindResource("Muted"), false, 3));
        var pathText = T(currentFile != null ? Path.GetFileName(currentFile) : "Default", 11.5, (Brush)FindResource("Muted"), false);
        info.Children.Add(pathText);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var importBtn = Btn(HasPro ? "Import" : "🔒 Import", false, 100);
        importBtn.Click += async (_, _) =>
        {
            if (!await EnsureProAsync()) return;
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "PNG files (*.png)|*.png", Title = $"Select {title}" };
            if (ofd.ShowDialog() == true)
            {
                setFilePath(ofd.FileName);
                saveToConfig(ofd.FileName);
                RobloxLauncher.SaveConfig(_config);
                try
                {
                    Directory.CreateDirectory(CustomEmoteCacheDir);
                    File.Copy(ofd.FileName, Path.Combine(CustomEmoteCacheDir, Path.GetFileName(getFilePath() != null ? Path.GetFileName(getFilePath()) : "emote.png")), true);
                    // Also save with proper base name for ResolveShow
                    var baseName = title.Contains("Background") ? "CircleBackground.png" : title.Contains("Segmented") ? "SegmentedCircle.png" : title.Contains("Gradient") ? "SelectedGradient.png" : "SelectedLine.png";
                    File.Copy(ofd.FileName, Path.Combine(CustomEmoteCacheDir, baseName), true);
                } catch { }
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(ofd.FileName);
                    bmp.EndInit();
                    bmp.Freeze();
                    preview.Source = bmp;
                    previewBorder.Child = preview;
                } catch { }
                pathText.Text = Path.GetFileName(ofd.FileName);
                pathText.Foreground = (Brush)FindResource("Text");
            }
        };
        btnRow.Children.Add(importBtn);
        info.Children.Add(btnRow);

        Grid.SetColumn(info, 1); grid.Children.Add(info);
        card.Child = grid;
        return card;
    }

    private async void ApplyEmotes_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureProAsync()) return;
        var emotesDir = FindRobloxEmotesFolder();
        if (emotesDir == null) { _emoteStatus.Text = Localization.T("Roblox not found"); return; }
        ApplyEmotes(emotesDir);
    }

    private void ApplyEmotes(string emotesDir)
    {
        try
        {
            EnsureEmoteBackups();
            int applied = 0;
            var allDirs = FindAllRobloxEmotesFolders();
            if (allDirs.Count == 0) allDirs = new List<string> { emotesDir };

            foreach (var dir in allDirs)
            {
                if (_emoteCircleBgFile != null && File.Exists(_emoteCircleBgFile))
                    foreach (var s in EmoteSuffixes) File.Copy(_emoteCircleBgFile, Path.Combine(dir, $"CircleBackground{s}.png"), true);
                if (_emoteSegmentedFile != null && File.Exists(_emoteSegmentedFile))
                    foreach (var s in EmoteSuffixes) File.Copy(_emoteSegmentedFile, Path.Combine(dir, $"SegmentedCircle{s}.png"), true);
                if (_emoteGradientFile != null && File.Exists(_emoteGradientFile))
                    foreach (var s in EmoteSuffixes) File.Copy(_emoteGradientFile, Path.Combine(dir, $"SelectedGradient{s}.png"), true);
                if (_emoteSelectedLineFile != null && File.Exists(_emoteSelectedLineFile))
                    foreach (var s in EmoteSuffixes) File.Copy(_emoteSelectedLineFile, Path.Combine(dir, $"SelectedLine{s}.png"), true);
            }
            if (_emoteCircleBgFile != null && File.Exists(_emoteCircleBgFile)) applied++;
            if (_emoteSegmentedFile != null && File.Exists(_emoteSegmentedFile)) applied++;
            if (_emoteGradientFile != null && File.Exists(_emoteGradientFile)) applied++;
            if (_emoteSelectedLineFile != null && File.Exists(_emoteSelectedLineFile)) applied++;

            _emoteStatus.Text = applied > 0 ? $"Applied {applied} texture(s) to {allDirs.Count} version(s)" : "No textures selected";
            _emoteStatus.Foreground = (Brush)FindResource("Accent");
            var curr = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
            if (curr != null) { _config.RobloxVersion = Path.GetFileName(curr); RobloxLauncher.SaveConfig(_config); }
        }
        catch (Exception ex) { _emoteStatus.Text = $"Error: {ex.Message}"; }
    }

    private void ResetEmotes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureEmoteBackups();
            var allDirs = FindAllRobloxEmotesFolders();
            if (allDirs.Count == 0) { _emoteStatus.Text = Localization.T("Roblox not found"); return; }

            int restored = 0;
            int deleted = 0;
            foreach (var dir in allDirs)
            {
                try
                {
                    foreach (var baseName in EmoteFileNames)
                    {
                        foreach (var suffix in EmoteSuffixes)
                        {
                            var dest = Path.Combine(dir, $"{baseName}{suffix}.png");
                            if (TryExtractEmbeddedEmote(baseName, suffix, dest)) restored++;
                            else if (File.Exists(dest)) { File.Delete(dest); deleted++; }
                        }
                    }
                } catch { }
            }
            string statusMsg = restored > 0
                ? $"Restored default emotes in {allDirs.Count} version(s) ({restored} files)"
                : deleted > 0 ? $"Removed {deleted} file(s) — defaults will restore on next Roblox launch"
                : "No custom emotes found";

            _emoteCircleBgFile = null; _emoteSegmentedFile = null; _emoteGradientFile = null; _emoteSelectedLineFile = null;
            _config.EmoteCircleBgPath = ""; _config.EmoteSegmentedPath = ""; _config.EmoteGradientPath = ""; _config.EmoteSelectedLinePath = "";
            try { if (Directory.Exists(CustomEmoteCacheDir)) foreach (var f in Directory.GetFiles(CustomEmoteCacheDir, "*.png")) File.Delete(f); } catch { }
            RobloxLauncher.SaveConfig(_config);
            _emotes = BuildEmotes();
            PageHost.Content = _emotes;
            _emoteStatus.Text = statusMsg;
            _emoteStatus.Foreground = (Brush)FindResource("Accent");
        }
        catch (Exception ex) { _emoteStatus.Text = $"Error: {ex.Message}"; }
    }

    private Grid GameProfileRow(string key, GameProfile p)
    {
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(T(p.Name, 14.5, (Brush)FindResource("Text"), true));
        string status = !p.Enabled
            ? Localization.T("Off")
            : p.FpsUnlock ? Localization.T("On") + $" · FPS {p.FpsLimit}" : Localization.T("On");
        info.Children.Add(T(status, 12, (Brush)FindResource("Muted"), false, 2));
        Grid.SetColumn(info, 0); grid.Children.Add(info);

        var editBtn = Btn(HasPro ? "Edit" : "🔒 Edit", false, 110);
        editBtn.VerticalAlignment = VerticalAlignment.Center;
        editBtn.Click += async (_, _) =>
        {
            if (!await EnsureProAsync()) return;
            ShowGameProfileDialog(key);
        };
        Grid.SetColumn(editBtn, 1); grid.Children.Add(editBtn);
        return grid;
    }

    private void ShowGameProfileDialog(string key)
    {
        if (_config.GameProfiles == null || !_config.GameProfiles.TryGetValue(key, out var src) || src == null) return;
        var p = new GameProfile
        {
            Name = src.Name,
            Enabled = src.Enabled,
            FpsUnlock = src.FpsUnlock,
            FpsLimit = src.FpsLimit,
            NoShadows = src.NoShadows,
            PerfMode = src.PerfMode,
            FutureLighting = src.FutureLighting,
            NoPostFx = src.NoPostFx,
            NoTelemetry = src.NoTelemetry
        };

        var dialog = new Window
        {
            Title = "Game profile",
            Owner = this,
            Width = 540,
            Height = 660,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var shell = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderBrush = new SolidColorBrush(Color.FromRgb(50, 66, 84)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Background = new LinearGradientBrush(Color.FromRgb(10, 18, 31), Color.FromRgb(13, 25, 42), new Point(0, 0), new Point(1, 1))
        };
        var root = new Grid();
        var close = new Button
        {
            Content = "✕",
            FontSize = 23,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(18)
        };
        close.Style = (Style)FindResource("CloseCrossButton");
        close.Click += (_, _) => dialog.Close();

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(36, 78, 36, 24) };
        var content = new StackPanel();
        content.Children.Add(T("Game profile", 24, (Brush)FindResource("Text"), true));
        content.Children.Add(T(p.Name, 14, (Brush)FindResource("Accent"), false, 4));

        var fpsBox = new TextBox { Style = (Style)FindResource("InputBox"), Width = 110, Text = p.FpsLimit.ToString(), IsEnabled = p.FpsUnlock, VerticalAlignment = VerticalAlignment.Center };

        content.Children.Add(ToggleRow("Enabled", "Apply this profile when the game launches.", p.Enabled, v => p.Enabled = v));

        content.Children.Add(ToggleRow("FPS Unlocker", "Remove the default FPS cap in Roblox.", p.FpsUnlock, v => { p.FpsUnlock = v; fpsBox.IsEnabled = v; }));
        var fpsRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        fpsRow.ColumnDefinitions.Add(new ColumnDefinition());
        fpsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var fpsLabel = new StackPanel();
        fpsLabel.Children.Add(T("FPS Limit", 15.5, (Brush)FindResource("Text"), true));
        fpsLabel.Children.Add(T("Target frames per second (1–9999)", 12.5, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(fpsLabel, 0); fpsRow.Children.Add(fpsLabel);
        Grid.SetColumn(fpsBox, 1); fpsRow.Children.Add(fpsBox);
        content.Children.Add(fpsRow);

        content.Children.Add(ToggleRow("Disable Shadows", "Turn off dynamic shadows for extra FPS.", p.NoShadows, v => p.NoShadows = v));
        content.Children.Add(ToggleRow("Performance Mode", "Lowest quality, no grass and no anti-aliasing for maximum FPS.", p.PerfMode, v => p.PerfMode = v));
        content.Children.Add(ToggleRow("Future Lighting", "Force the newest Roblox lighting engine.", p.FutureLighting, v => p.FutureLighting = v));
        content.Children.Add(ToggleRow("Disable Post-Effects", "Turn off blur, bloom and color effects.", p.NoPostFx, v => p.NoPostFx = v));
        content.Children.Add(ToggleRow("Disable Telemetry", "Send less analytics data to Roblox servers.", p.NoTelemetry, v => p.NoTelemetry = v));

        content.Children.Add(SectionLabel("GAME CURSORS"));
        content.Children.Add(T("Per-game cursors override the global ones — clear a cursor to use the global setting.", 12, (Brush)FindResource("Muted"), false, 2));

        StackPanel CursorPickRow(string label, Func<string> get, Action<string> set, out TextBlock status)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var lbl = T(label, 14, (Brush)FindResource("Text"), false);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            lbl.Width = 140;
            row.Children.Add(lbl);
            var st = T(string.IsNullOrEmpty(get()) ? Localization.T("Global") : Path.GetFileName(get()), 12, (Brush)FindResource("Muted"), false);
            st.VerticalAlignment = VerticalAlignment.Center;
            st.Margin = new Thickness(6, 0, 0, 0);
            var importBtn = Btn("Import", false, 90);
            importBtn.Click += (_, _) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "PNG files (*.png)|*.png", Title = label };
                if (ofd.ShowDialog() == true)
                {
                    set(ofd.FileName);
                    st.Text = Path.GetFileName(ofd.FileName);
                    st.Foreground = (Brush)FindResource("Text");
                }
            };
            row.Children.Add(importBtn);
            var clearBtn = Btn("Clear", false, 90);
            clearBtn.Margin = new Thickness(8, 0, 0, 0);
            clearBtn.Click += (_, _) =>
            {
                set("");
                st.Text = Localization.T("Global");
                st.Foreground = (Brush)FindResource("Muted");
            };
            row.Children.Add(clearBtn);
            status = st;
            return row;
        }

        content.Children.Add(CursorPickRow("Normal cursor", () => p.CursorNormal, v => p.CursorNormal = v, out var normStatus));
        content.Children.Add(CursorPickRow("Pointing cursor", () => p.CursorPointing, v => p.CursorPointing = v, out var pointStatus));
        content.Children.Add(CursorPickRow("Shift-Lock cursor", () => p.CursorShiftLock, v => p.CursorShiftLock = v, out var shiftStatus));

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        var saveBtn = Btn("Save", true, 150);
        var removeBtn = Btn("Remove", false, 150);
        removeBtn.Margin = new Thickness(12, 0, 0, 0);
        var dlgStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        dlgStatus.VerticalAlignment = VerticalAlignment.Center;
        dlgStatus.Margin = new Thickness(14, 0, 0, 0);
        btnRow.Children.Add(saveBtn); btnRow.Children.Add(removeBtn); btnRow.Children.Add(dlgStatus);
        content.Children.Add(btnRow);

        scroll.Content = content;
        root.Children.Add(scroll);
        root.Children.Add(close);
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(); };
        shell.Child = root;
        dialog.Content = shell;

        saveBtn.Click += (_, _) =>
        {
            if (int.TryParse(fpsBox.Text.Trim(), out var fps)) p.FpsLimit = Math.Clamp(fps, 1, 9999);
            _config.GameProfiles[key] = p;
            RobloxLauncher.SaveConfig(_config);
            RebuildFastFlagsPage();
            dlgStatus.Text = Localization.T("Profile saved");
        };
        removeBtn.Click += (_, _) =>
        {
            _config.GameProfiles.Remove(key);
            RobloxLauncher.SaveConfig(_config);
            RebuildFastFlagsPage();
            dialog.Close();
        };

        dialog.ShowDialog();
    }

    private void RebuildFastFlagsPage()
    {
        var showing = ReferenceEquals(PageHost.Content, _fastFlags);
        _fastFlags = BuildFastFlags();
        if (showing) PageHost.Content = _fastFlags;
    }

    private Grid BuildMaintenance()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel();
        title.Children.Add(T("Maintenance", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        host.Children.Add(T("Clean caches and manage your Roblox installation.", 13, (Brush)FindResource("Muted"), false));

        var cleanup = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24) };
        var cs = new StackPanel();
        cs.Children.Add(SectionLabel("CLEANUP"));
        cs.Children.Add(SizeRow("Roblox Logs", "Session and crash logs from the Roblox client.", RobloxLogsDir, out _logsSizeText));
        cs.Children.Add(SizeRow("RBX Storage", "Downloaded assets and thumbnail cache.", RobloxStorageDir, out _storageSizeText));
        cs.Children.Add(SizeRow("Temporary Files", "Leftover files in the temp folder.", RobloxTempDir, out _tempSizeText));

        var cleanRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        var cleanBtn = Btn("Clean Cache", true, 150); cleanBtn.Click += CleanCache_Click;
        var openLogs = Btn("Open Logs Folder", false, 170); openLogs.Margin = new Thickness(12, 0, 0, 0); openLogs.Click += (_, _) => OpenFolder(RobloxLogsDir);
        _cleanStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        _cleanStatus.VerticalAlignment = VerticalAlignment.Center;
        _cleanStatus.Margin = new Thickness(14, 0, 0, 0);
        cleanRow.Children.Add(cleanBtn); cleanRow.Children.Add(openLogs); cleanRow.Children.Add(_cleanStatus);
        cs.Children.Add(cleanRow);
        cleanup.Child = cs;
        host.Children.Add(cleanup);

        var install = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var isv = new StackPanel();
        isv.Children.Add(SectionLabel("ROBLOX INSTALL"));
        _installInfo = T("", 14.5, (Brush)FindResource("Text"), true, 4);
        isv.Children.Add(_installInfo);
        var installRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var openFolder = Btn("Open Roblox Folder", false, 190); openFolder.Click += (_, _) => OpenFolder(Path.Combine(RobloxRoot, "Versions"));
        var resetBtn = Btn("Reset Roblox", false, 140); resetBtn.Margin = new Thickness(12, 0, 0, 0); resetBtn.Click += ResetRoblox_Click;
        _installStatus = T("", 12.5, (Brush)FindResource("Muted"), false);
        _installStatus.VerticalAlignment = VerticalAlignment.Center;
        _installStatus.Margin = new Thickness(14, 0, 0, 0);
        installRow.Children.Add(openFolder); installRow.Children.Add(resetBtn); installRow.Children.Add(_installStatus);
        isv.Children.Add(installRow);
        install.Child = isv;
        host.Children.Add(install);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);
        _maintenanceScroll = scroll;
        return g;
    }

    private Grid SizeRow(string title, string desc, string path, out TextBlock sizeText)
    {
        var grid = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var s = new StackPanel();
        s.Children.Add(T(title, 14.5, (Brush)FindResource("Text"), true));
        s.Children.Add(T(desc, 12, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(s, 0); grid.Children.Add(s);
        sizeText = T(FormatSize(DirSize(path)), 14.5, (Brush)FindResource("Accent"), true);
        sizeText.VerticalAlignment = VerticalAlignment.Center;
        sizeText.MinWidth = 80;
        sizeText.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(sizeText, 1); grid.Children.Add(sizeText);
        return grid;
    }

    private static long DirSize(string? path)
    {
        if (path == null || !Directory.Exists(path)) return 0;
        try { return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { return 0; }
    }

    private static string FormatSize(long bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.0} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0} KB" : $"{bytes} B";

    private static long CleanDir(string path)
    {
        if (!Directory.Exists(path)) return 0;
        long size = 0;
        try
        {
            size = DirSize(path);
            foreach (var f in Directory.GetFiles(path)) File.Delete(f);
            foreach (var d in Directory.GetDirectories(path)) Directory.Delete(d, true);
        }
        catch { }
        return size;
    }

    private void RefreshSizes()
    {
        _logsSizeText.Text = FormatSize(DirSize(RobloxLogsDir));
        _storageSizeText.Text = FormatSize(DirSize(RobloxStorageDir));
        _tempSizeText.Text = FormatSize(DirSize(RobloxTempDir));
    }

    private void RefreshInstallInfo()
    {
        var folders = RobloxLauncher.FindPlayerFolders();
        _installInfo.Text = folders.Count == 0
            ? Localization.T("Roblox is not installed")
            : $"Roblox {Path.GetFileName(folders[0])}" + (folders.Count > 1 ? $" (+{folders.Count - 1} more version(s))" : "");
    }

    private void CleanCache_Click(object sender, RoutedEventArgs e)
    {
        var freed = CleanDir(RobloxLogsDir) + CleanDir(RobloxStorageDir) + CleanDir(RobloxTempDir);
        RefreshSizes();
        _cleanStatus.Text = $"Freed {FormatSize(freed)}";
    }

    private void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{path}\"", UseShellExecute = true });
    }

    private void ResetRoblox_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _installStatus.Text = Localization.T("Closing Roblox..."); foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta")) try { p.Kill(); } catch { } Thread.Sleep(1500); }

        var confirm = MessageBox.Show(
            "This deletes the Roblox client files. Roblox will automatically re-download and reinstall on next launch.\n\nContinue?",
            "Reset Roblox", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var versions = Path.Combine(RobloxRoot, "Versions");
        if (!Directory.Exists(versions)) { _installStatus.Text = Localization.T("Roblox is not installed"); return; }

        var failed = 0;
        foreach (var dir in Directory.GetDirectories(versions))
        {
            try { Directory.Delete(dir, true); }
            catch { failed++; }
        }

        RefreshInstallInfo();
        _installStatus.Text = failed == 0
            ? "Roblox reset. It will reinstall on next launch."
            : $"Reset with {failed} folder(s) locked. Close Roblox and try again.";
    }

    private Grid BuildSettings()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel();
        title.Children.Add(T("Launcher Settings", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        host.Children.Add(T("Control how Naxi behaves on your PC.", 13, (Brush)FindResource("Muted"), false));

        var iface = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24) };
        var isv = new StackPanel();
        isv.Children.Add(SectionLabel("INTERFACE CUSTOMIZATION"));

        var fontRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        fontRow.ColumnDefinitions.Add(new ColumnDefinition());
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var fontLabel = new StackPanel();
        fontLabel.Children.Add(T("Launcher Font", 15.5, (Brush)FindResource("Text"), true));
        fontLabel.Children.Add(T("Click to cycle through available fonts.", 12.5, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(fontLabel, 0); fontRow.Children.Add(fontLabel);
        _fontBtn = Btn($"Font: {_config.FontName}", false, 240);
        _fontBtn.Height = 46;
        _fontBtn.Padding = new Thickness(18, 8, 18, 8);
        _fontBtn.Click += FontBtn_Click;
        Grid.SetColumn(_fontBtn, 1); fontRow.Children.Add(_fontBtn);
        isv.Children.Add(fontRow);

        var bgRow = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        bgRow.ColumnDefinitions.Add(new ColumnDefinition());
        bgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var bgLabel = new StackPanel();
        bgLabel.Children.Add(T("Custom Background Image", 15.5, (Brush)FindResource("Text"), true));
        bgLabel.Children.Add(T("Enter a direct image URL (PNG/JPG).", 12.5, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(bgLabel, 0); bgRow.Children.Add(bgLabel);
        var bgControls = new StackPanel { Orientation = Orientation.Horizontal };
        _bgUrlBox = new TextBox { Style = (Style)FindResource("InputBox"), Width = 250, Text = _config.BackgroundUrl, VerticalAlignment = VerticalAlignment.Center };
        var bgApply = SmallBtn("Apply", true, 80); bgApply.Click += ApplyBackground_Click;
        var bgClear = SmallBtn("Clear", false, 80); bgClear.Margin = new Thickness(8, 0, 0, 0); bgClear.Click += ClearBackground_Click;
        bgControls.Children.Add(_bgUrlBox); bgControls.Children.Add(bgApply); bgControls.Children.Add(bgClear);
        Grid.SetColumn(bgControls, 1); bgRow.Children.Add(bgControls);
        isv.Children.Add(bgRow);

        _bgStatus = T("", 12.5, (Brush)FindResource("Muted"), false, 10);
        isv.Children.Add(_bgStatus);

        var themeRow = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        themeRow.ColumnDefinitions.Add(new ColumnDefinition());
        themeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var themeLabel = new StackPanel();
        themeLabel.Children.Add(T("Theme", 15.5, (Brush)FindResource("Text"), true));
        themeLabel.Children.Add(T("Dark or light interface.", 12.5, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(themeLabel, 0); themeRow.Children.Add(themeLabel);
        var themeBtn = Btn("Theme: " + (string.Equals(_config.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark"), false, 220);
        themeBtn.Height = 46; themeBtn.Click += ThemeBtn_Click;
        Grid.SetColumn(themeBtn, 1); themeRow.Children.Add(themeBtn);
        isv.Children.Add(themeRow);

        var accentRow = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        accentRow.ColumnDefinitions.Add(new ColumnDefinition());
        accentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var accentLabel = new StackPanel();
        accentLabel.Children.Add(T("Accent color", 15.5, (Brush)FindResource("Text"), true));
        accentLabel.Children.Add(T("Color of buttons, toggles and active elements.", 12.5, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(accentLabel, 0); accentRow.Children.Add(accentLabel);
        var swatches = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var a in ThemeService.Accents)
        {
            bool sel = a.Name.Equals(_config.Accent, StringComparison.OrdinalIgnoreCase);
            var sw = new Button
            {
                Style = (Style)FindResource("SwatchButton"),
                Background = new SolidColorBrush(a.Base),
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = ThemeService.MaxOnly.Contains(a.Name) ? a.Name + "  ·  MAX" : a.Name,
            };
            if (sel) { sw.BorderBrush = (Brush)FindResource("Text"); }
            System.Windows.Automation.AutomationProperties.SetAutomationId(sw, "Swatch" + a.Name);
            var nm = a.Name;
            sw.Click += (_, _) => AccentSwatch_Click(nm);
            swatches.Children.Add(sw);
        }
        Grid.SetColumn(swatches, 1); accentRow.Children.Add(swatches);
        isv.Children.Add(accentRow);

        isv.Children.Add(ToggleRow("Hide Update Log", "Show or completely hide the Update log block on Home tab.", _config.HideUpdateLog, v =>
        {
            _config.HideUpdateLog = v;
            _logCard.Visibility = v ? Visibility.Collapsed : Visibility.Visible;
            RobloxLauncher.SaveConfig(_config);
        }));
        iface.Child = isv;
        host.Children.Add(iface);

        var general = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var gv = new StackPanel();
        gv.Children.Add(SectionLabel("GENERAL PREFERENCES"));
        gv.Children.Add(ToggleRow("Open Roblox links through Naxi", "Handle roblox-player links so FastFlags apply on every game join. The browser will show \"Open Naxi Bootstrap?\".", _config.OpenRobloxLinks, v =>
        {
            _config.OpenRobloxLinks = v;
            RobloxLauncher.RegisterProtocol(v);
            RobloxLauncher.SaveConfig(_config);
        }));
        gv.Children.Add(ToggleRow("Autostart Launcher", "Automatically open launcher when system starts.", _config.Autostart, v => { _config.Autostart = v; SetAutostart(v); RobloxLauncher.SaveConfig(_config); }));
        gv.Children.Add(ToggleRow("Update Notifications", "Receive system popups when a new version is released.", _config.Notifications, v => { _config.Notifications = v; RobloxLauncher.SaveConfig(_config); }));
        general.Child = gv;
        host.Children.Add(general);

        var netCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var nv = new StackPanel();
        nv.Children.Add(SectionLabel("NETWORK"));
        nv.Children.Add(ToggleRow("Bypass ISP Blocks (Zapret)", "DPI bypass so Roblox images, Discord and other services load on ISPs that block them. Windows will ask for administrator permission when enabling.", _config.Zapret, v => _ = ZapretToggleAsync(v)));

        var presetRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        presetRow.ColumnDefinitions.Add(new ColumnDefinition());
        presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var presetLabel = new StackPanel();
        presetLabel.Children.Add(T("Preset", 15.5, (Brush)FindResource("Text"), true));
        presetLabel.Children.Add(T("Different ISPs need different strategies. If the default does not help, cycle through ALT presets.", 12.5, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(presetLabel, 0); presetRow.Children.Add(presetLabel);
        _zapretPresetBtn = Btn("Preset: " + ZapretPresetDisplay(_config.ZapretPreset) + "  ▾", false, 260);
        _zapretPresetBtn.Height = 46;
        _zapretPresetBtn.Padding = new Thickness(18, 8, 18, 8);
        _zapretPresetBtn.Click += ZapretPreset_Click;
        Grid.SetColumn(_zapretPresetBtn, 1); presetRow.Children.Add(_zapretPresetBtn);
        nv.Children.Add(presetRow);

        _zapretStatus = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
        nv.Children.Add(_zapretStatus);
        UpdateZapretStatus();
        netCard.Child = nv;
        host.Children.Add(netCard);

        var langCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var lv = new StackPanel();
        lv.Children.Add(SectionLabel("Language"));
        lv.Children.Add(T("Choose launcher language. All texts will translate instantly.", 12.5, (Brush)FindResource("Muted"), false, 4));
        var langRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var (name, lang) in new[] { ("English", Lang.En), ("Русский", Lang.Ru), ("Română", Lang.Ro), ("Français", Lang.Fr), ("日本語", Lang.Ja), ("Čeština", Lang.Cs) })
        {
            var isActive = Localization.Current == lang;
            var b = Btn(name, isActive, 110); b.Margin = new Thickness(0, 0, 8, 0);
            var l = lang;
            b.Click += (_, _) => { Localization.Set(l); _config.Language = l.ToString(); RobloxLauncher.SaveConfig(_config); };
            langRow.Children.Add(b);
        }
        lv.Children.Add(langRow);
        langCard.Child = lv;
        host.Children.Add(langCard);

        var rpcCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var rp = new StackPanel();
        rp.Children.Add(SectionLabel("DISCORD RPC EDITOR"));
        rp.Children.Add(T("Show custom Rich Presence in Discord. Requires Discord desktop app running. Large image must be uploaded in Discord Developer Portal → Rich Presence → Art Assets.", 12.5, (Brush)FindResource("Muted"), false, 4));
        rp.Children.Add(ToggleRow("Enable Discord RPC", "Show presence when launcher is open. Updates live when you edit fields.", _config.DiscordRpcEnabled, v =>
        {
            _config.DiscordRpcEnabled = v;
            RobloxLauncher.SaveConfig(_config);
            if (v) DiscordRpcService.Start(_config); else DiscordRpcService.Stop();
        }));
        rp.Children.Add(ToggleRow("Show Elapsed Time", "Display timer since RPC started.", _config.DiscordRpcShowElapsed, v => { _config.DiscordRpcShowElapsed = v; RobloxLauncher.SaveConfig(_config); DiscordRpcService.Update(_config); }));

        TextBox RpcInput(string label, string value, Action<string> onChange)
        {
            var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var lb = T(label, 12.5, (Brush)FindResource("Muted"), false); lb.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(lb, 0); row.Children.Add(lb);
            var tb = new TextBox { Style = (Style)FindResource("InputBox"), Text = value, Padding = new Thickness(10, 7, 10, 7), FontSize = 12.5 };
            tb.TextChanged += (_, _) => { onChange(tb.Text); RobloxLauncher.SaveConfig(_config); DiscordRpcService.Update(_config); };
            Grid.SetColumn(tb, 1); row.Children.Add(tb);
            rp.Children.Add(row);
            return tb;
        }

        RpcInput("Details (line 1)", _config.DiscordRpcDetails, v => _config.DiscordRpcDetails = v);
        RpcInput("State (line 2)", _config.DiscordRpcState, v => _config.DiscordRpcState = v);
        RpcInput("Large Image Key", _config.DiscordRpcLargeImage, v => _config.DiscordRpcLargeImage = v);
        RpcInput("Large Image Text", _config.DiscordRpcLargeText, v => _config.DiscordRpcLargeText = v);

        var rpcBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var rpcTest = SmallBtn("Test / Refresh", true, 140); rpcTest.Click += (_, _) => { RobloxLauncher.SaveConfig(_config); DiscordRpcService.Update(_config); MessageBox.Show(DiscordRpcService.IsRunning ? "RPC sent! Check Discord profile." : "Not connected to Discord. Open Discord desktop app and try again.", "Discord RPC", MessageBoxButton.OK, DiscordRpcService.IsRunning ? MessageBoxImage.Information : MessageBoxImage.Warning); };
        var rpcStop = SmallBtn("Stop", false, 90); rpcStop.Margin = new Thickness(8, 0, 0, 0); rpcStop.Click += (_, _) => DiscordRpcService.Stop();
        rpcBtnRow.Children.Add(rpcTest); rpcBtnRow.Children.Add(rpcStop);
        rp.Children.Add(rpcBtnRow);

        rpcCard.Child = rp;
        host.Children.Add(rpcCard);

        // --- game card: auto-rejoin toggle ---
        var gameCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var gcv = new StackPanel();
        gcv.Children.Add(SectionLabel("Game"));
        gcv.Children.Add(ToggleRow("Auto-rejoin", "If Roblox disconnects, offer to rejoin the same game automatically after 10 seconds.", _config.AutoRejoin, v =>
        {
            _config.AutoRejoin = v;
            RobloxLauncher.SaveConfig(_config);
            if (v) EnsureRejoinWatcher();
        }));
        if (_config.IsMax)
        {
            gcv.Children.Add(ToggleRow("Quick launch hotkey (Alt+R)", "Press Alt+R anywhere for the instant game search overlay.", _config.QuickHotkey, v =>
            {
                _config.QuickHotkey = v;
                RobloxLauncher.SaveConfig(_config);
                ApplyQuickHotkey();
            }));
            gcv.Children.Add(ToggleRow("Show current game in Discord", "Rich Presence automatically switches to the Roblox game you are playing.", _config.RpcAutoGame, v =>
            {
                _config.RpcAutoGame = v;
                RobloxLauncher.SaveConfig(_config);
                if (!v) { DiscordRpcService.AutoGameName = null; DiscordRpcService.Update(_config); }
            }));
        }
        gameCard.Child = gcv;
        host.Children.Add(gameCard);

        // --- backup card: export / import settings ---
        var backupCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var bv = new StackPanel();
        bv.Children.Add(SectionLabel("Backup"));
        bv.Children.Add(T("Export all settings to a file or import them back — handy when moving to another PC.", 12.5, (Brush)FindResource("Muted"), false, 4));
        var backupRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var exportBtn = Btn(Localization.T("Export settings"), true, 170);
        exportBtn.Click += (_, _) => ExportSettings();
        var importBtn = Btn(Localization.T("Import settings"), false, 170);
        importBtn.Margin = new Thickness(10, 0, 0, 0);
        importBtn.Click += (_, _) => ImportSettings();
        backupRow.Children.Add(exportBtn);
        backupRow.Children.Add(importBtn);
        bv.Children.Add(backupRow);
        backupCard.Child = bv;
        host.Children.Add(backupCard);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);
        _settingsScroll = scroll;
        return g;
    }

    // ---------- settings export / import ----------

    // Serializes the whole AppConfig (fast flags, folders, favorites, accounts
    // metadata, theme…) to a JSON file the user can keep or move to another PC.
    // Account tokens are stored protected (DPAPI) and are portable only between
    // machines of the same user, which is the safest default.
    private async void ExportSettings()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = Localization.T("Export settings"),
            FileName = "naxi-bootstrap-settings.json",
            Filter = "JSON (*.json)|*.json"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dlg.FileName, json);
            MessageBox.Show(this, Localization.T("Settings exported."), "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportSettings()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Localization.T("Import settings"),
            Filter = "JSON (*.json)|*.json"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var json = await File.ReadAllTextAsync(dlg.FileName);
            var imported = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json);
            if (imported == null) throw new InvalidDataException("bad file");
            // keep session-only bits: current Pro state must stay server-driven
            imported.IsPro = _config.IsPro;
            imported.ProExpiresAt = _config.ProExpiresAt;
            _config = imported;
            RobloxLauncher.SaveConfig(_config);
            ThemeService.Apply(_config.Theme, _config.Accent);
            if (Enum.TryParse<Lang>(_config.Language, true, out var lang) && Localization.Current != lang) Localization.Set(lang);
            FontFamily = new FontFamily(string.IsNullOrWhiteSpace(_config.FontName) ? "Segoe UI" : _config.FontName);
            RebuildAllPagesKeepCurrent();
            MessageBox.Show(this, Localization.T("Settings imported."), "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SyncProStatusAsync()
    {
        var result = await ProLicense.CheckAsync();
        if (!result.Ok) return;
        var changed = _config.IsPro != result.Pro || _config.ProExpiresAt != result.ExpiresAt
            || _config.IsMax != result.Max || (result.Max && _config.MaxExpiresAt != result.ExpiresAt);
        _config.IsPro = result.Pro;
        _config.IsMax = result.Max;
        _config.ProExpiresAt = result.ExpiresAt;
        if (result.Max) _config.MaxExpiresAt = result.ExpiresAt;
        if (!changed) return;
        RobloxLauncher.SaveConfig(_config);
        Dispatcher.Invoke(RefreshProPage);
    }

    private System.Windows.Media.Imaging.BitmapFrame? _proIconFrame;
    private System.Drawing.Icon? _proTrayIcon;

    private void RefreshProPage()
    {
        var showing = ReferenceEquals(PageHost.Content, _pro);
        _pro = BuildPro();
        if (showing) PageHost.Content = _pro;
        ApplyProBranding();
    }

    private static string? ProTimeLeftText(string? expiresAtIso)
    {
        if (string.IsNullOrWhiteSpace(expiresAtIso)) return null;
        if (!DateTimeOffset.TryParse(expiresAtIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exp)) return null;
        var left = exp - DateTimeOffset.UtcNow;
        if (left <= TimeSpan.Zero) return null;
        if (left.TotalDays >= 1)
            return Localization.T("Pro ends in # days").Replace("#", Math.Ceiling(left.TotalDays).ToString("0"));
        return Localization.T("Pro ends in # h").Replace("#", Math.Max(1, (int)Math.Ceiling(left.TotalHours)).ToString("0"));
    }

    private static string? MaxTimeLeftText(string? expiresAtIso)
    {
        if (string.IsNullOrWhiteSpace(expiresAtIso)) return null;
        if (!DateTimeOffset.TryParse(expiresAtIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exp)) return null;
        var left = exp - DateTimeOffset.UtcNow;
        if (left <= TimeSpan.Zero) return null;
        if (left.TotalDays >= 1)
            return Localization.T("Max ends in # days").Replace("#", Math.Ceiling(left.TotalDays).ToString("0"));
        return Localization.T("Max ends in # h").Replace("#", Math.Max(1, (int)Math.Ceiling(left.TotalHours)).ToString("0"));
    }

    private void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        _config.Theme = string.Equals(_config.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        RobloxLauncher.SaveConfig(_config);
        ThemeService.Apply(_config.Theme, _config.Accent);
        RebuildAllPagesKeepCurrent();
    }

    private void AccentSwatch_Click(string name)
    {
        if (ThemeService.MaxOnly.Contains(name) && !_config.IsMax)
        {
            ShowProInviteDialog(allowWhenActive: true);
            return;
        }
        _config.Accent = name;
        RobloxLauncher.SaveConfig(_config);
        ThemeService.Apply(_config.Theme, _config.Accent);
        RebuildAllPagesKeepCurrent();
    }

    private void RebuildAllPagesKeepCurrent()
    {
        var cur = PageHost.Content;
        bool onHome = ReferenceEquals(cur, _home), onGames = ReferenceEquals(cur, _games), onNews = ReferenceEquals(cur, _news), onAccounts = ReferenceEquals(cur, _accounts),
             onFlags = ReferenceEquals(cur, _fastFlags), onCursors = ReferenceEquals(cur, _cursors), onEmotes = ReferenceEquals(cur, _emotes),
             onMaint = ReferenceEquals(cur, _maintenance), onSettings = ReferenceEquals(cur, _settings), onPro = ReferenceEquals(cur, _pro),
             onAbout = ReferenceEquals(cur, _about), onLegal = ReferenceEquals(cur, _legal);
        _home = BuildHome(); _games = BuildGameHub(); _news = BuildNews(); _accounts = BuildAccounts(); _fastFlags = BuildFastFlags(); _cursors = BuildCursors();
        _emotes = BuildEmotes(); _maintenance = BuildMaintenance(); _settings = BuildSettings(); _pro = BuildPro(); _about = BuildAbout(); _legal = BuildLegal();
        PageHost.Content = onGames ? _games : onNews ? _news : onAccounts ? _accounts : onFlags ? _fastFlags : onCursors ? _cursors : onEmotes ? _emotes
            : onMaint ? _maintenance : onSettings ? _settings : onPro ? _pro : onAbout ? _about : onLegal ? _legal : _home;
    }

    private void ApplyFfPreset(string preset)
    {
        if (preset == "potato")
        {
            _config.FpsUnlock = true; _config.FpsLimit = 999; _config.NoShadows = true; _config.PerfMode = true;
            _config.FutureLighting = false; _config.NoPostFx = true; _config.NoTelemetry = true;
        }
        else if (preset == "balanced")
        {
            _config.FpsUnlock = true; _config.NoShadows = true; _config.PerfMode = false;
            _config.FutureLighting = false; _config.NoPostFx = true; _config.NoTelemetry = true;
        }
        else
        {
            _config.FpsUnlock = true; _config.NoShadows = false; _config.PerfMode = false;
            _config.FutureLighting = true; _config.NoPostFx = false; _config.NoTelemetry = true;
        }
        SaveAndApply();
        RebuildFastFlagsPage();
    }

    private void RebuildSettingsPage()
    {
        var showing = ReferenceEquals(PageHost.Content, _settings);
        _settings = BuildSettings();
        if (showing) PageHost.Content = _settings;
    }

    private async Task ZapretToggleAsync(bool on)
    {
        if (!on)
        {
            _config.Zapret = false;
            RobloxLauncher.SaveConfig(_config);
            await Task.Run(() => { try { ZapretService.Stop(); } catch { } });
            RebuildSettingsPage();
            return;
        }
        _config.Zapret = true;
        RobloxLauncher.SaveConfig(_config);
        bool ok = await Task.Run(() => { try { return ZapretService.Start(_config.ZapretPreset); } catch { return false; } });
        if (!ok)
        {
            _config.Zapret = false;
            RobloxLauncher.SaveConfig(_config);
        }
        RebuildSettingsPage();
    }

    private async void ZapretPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_presetPopup != null && _presetPopup.IsOpen) { _presetPopup.IsOpen = false; return; }
        var list = new StackPanel { MinWidth = 236 };
        foreach (var name in ZapretService.PresetNames())
        {
            bool sel = string.Equals(name, _config.ZapretPreset, StringComparison.OrdinalIgnoreCase);
            var row = new Border { Padding = new Thickness(12, 8, 12, 8), CornerRadius = new CornerRadius(8), Margin = new Thickness(4, 1, 4, 1), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = sel ? "\uE73E" : "", FontFamily = NavIconFont, FontSize = 12, Width = 20, Foreground = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = ZapretPresetDisplay(name), FontSize = 13, FontWeight = sel ? FontWeights.SemiBold : FontWeights.Normal, Foreground = (Brush)FindResource(sel ? "Accent" : "Text"), VerticalAlignment = VerticalAlignment.Center });
            row.Child = sp;
            var captured = name;
            row.MouseEnter += (_, _) => row.Background = (Brush)FindResource("NavHover");
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, _) => { _presetPopup!.IsOpen = false; SelectZapretPreset(captured); };
            list.Children.Add(row);
        }
        var scroll = new ScrollViewer { MaxHeight = 384, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list };
        var card = new Border { Background = (Brush)FindResource("Surface"), BorderBrush = (Brush)FindResource("Stroke"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(4), Child = scroll, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 0, Opacity = 0.45 } };
        _presetPopup = new System.Windows.Controls.Primitives.Popup { PlacementTarget = _zapretPresetBtn, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = card };
        _presetPopup.IsOpen = true;
    }

    private async void SelectZapretPreset(string name)
    {
        _config.ZapretPreset = name;
        RobloxLauncher.SaveConfig(_config);
        if (_zapretPresetBtn != null) _zapretPresetBtn.Content = "Preset: " + ZapretPresetDisplay(name) + "  ▾";
        if (_config.Zapret)
        {
            await Task.Run(() => { try { ZapretService.Restart(name); } catch { } });
            UpdateZapretStatus();
        }
    }

    private static string ZapretPresetDisplay(string name)
    {
        var s = name.StartsWith("general", StringComparison.OrdinalIgnoreCase) ? name[7..].Trim() : name.Trim();
        return s.Length == 0 ? "general" : s.Trim('(', ')');
    }

    private void UpdateZapretStatus()
    {
        if (_zapretStatus == null) return;
        _zapretStatus.Text = ZapretService.IsRunning()
            ? $"Zapret is running ({_config.ZapretPreset}) — Roblox images and Discord voice go through the bypass."
            : _config.Zapret
                ? "Zapret is enabled but not running — toggle it off and on (administrator permission may be needed)."
                : "Zapret is not running.";
    }

    private void ApplyProBranding()
    {
        var pro = _config.IsPro || _config.IsMax;
        var isMax = _config.IsMax;
        Title = isMax ? "NaxiBootstrap MAX" : pro ? "Naxi Bootstrap PRO" : "Naxi Bootstrap";
        try
        {
            if (pro)
            {
                if (_proIconFrame == null)
                {
                    using var s = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Icons/icon-pro.ico")).Stream;
                    var ms = new MemoryStream();
                    s.CopyTo(ms);
                    ms.Position = 0;
                    _proIconFrame = System.Windows.Media.Imaging.BitmapFrame.Create(ms, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                }
                Icon = _proIconFrame;
            }
            else
            {
                Icon = null;
            }
        }
        catch { }
        try
        {
            if (LogoBadge != null)
            {
                LogoBadge.Background = isMax
                    ? new LinearGradientBrush(Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x5B, 0x21, 0xB6), new Point(0, 0), new Point(1, 1))
                    : pro
                    ? new LinearGradientBrush(Color.FromRgb(0xE7, 0xC8, 0x77), Color.FromRgb(0xC0, 0x96, 0x3F), new Point(0, 0), new Point(1, 1))
                    : (Brush)FindResource("AccentGradient");
                LogoBadge.BorderBrush = new SolidColorBrush(pro ? Color.FromRgb(0x14, 0x14, 0x14) : Color.FromRgb(0x4A, 0x7A, 0x96));
            }
        }
        catch { }
        try
        {
            if (_trayIcon != null)
            {
                _trayIcon.Text = Title;
                if (pro)
                {
                    if (_proTrayIcon == null)
                    {
                        using var s = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Icons/icon-pro.ico")).Stream;
                        _proTrayIcon = new System.Drawing.Icon(s);
                    }
                    _trayIcon.Icon = _proTrayIcon;
                }
                else if (!string.IsNullOrEmpty(Environment.ProcessPath))
                {
                    _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                }
            }
        }
        catch { }
    }

    private Grid BuildPro()
    {
        var page = new Grid { Margin = new Thickness(72, 22, 40, 0) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        var content = new StackPanel { Width = 620, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 24) };

        var title = new TextBlock
        {
            Text = "Naxi Pro",
            FontSize = 38,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TitleGradient"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(title);
        var pro = _config.IsPro || _config.IsMax;
        var timeLeft = ProTimeLeftText(_config.ProExpiresAt);
        var maxTime = MaxTimeLeftText(_config.MaxExpiresAt);
        var subtitle = T(
            _config.IsMax ? (maxTime ?? "MAX is active on this PC.")
            : pro ? (timeLeft ?? "Pro is active on this PC.")
            : "Unlock Naxi Pro with an invite code.", 14, (Brush)FindResource("Muted"), false, 8);
        subtitle.TextAlignment = TextAlignment.Center;
        content.Children.Add(subtitle);

        var proCardCursor = _config.IsPro || _config.IsMax ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand;
        var card = new Border
        {
            Style = (Style)FindResource("Card"),
            Padding = new Thickness(26),
            Margin = new Thickness(0, 24, 0, 0),
            Cursor = proCardCursor,
            BorderBrush = new SolidColorBrush(Color.FromRgb(74, 122, 150))
        };
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(18),
            Background = (Brush)FindResource("AccentGradient"),
            Child = new TextBlock { Text = _config.IsPro || _config.IsMax ? "✓" : "✦", FontSize = 27, Foreground = (Brush)FindResource("AccentText"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(icon, 0); row.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 18, 0) };
        text.Children.Add(T(_config.IsMax ? "Naxi Pro — included in MAX" : _config.IsPro ? "Naxi Pro unlocked" : "Upgrade to Pro", 18, (Brush)FindResource("Text"), true));
        text.Children.Add(T(_config.IsMax ? "Everything Pro offers is already unlocked with Naxi MAX." : _config.IsPro ? (timeLeft ?? "This invite is bound to this computer.") : "Naxi Pro is invite only", 13, (Brush)FindResource("Muted"), false, 4));
        Grid.SetColumn(text, 1); row.Children.Add(text);
        if (!_config.IsPro && !_config.IsMax)
        {
            var arrow = new TextBlock { Text = "›", FontSize = 34, Foreground = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
            card.MouseLeftButtonUp += (_, _) => ShowProInviteDialog();
        }
        card.Child = row;
        content.Children.Add(card);

        content.Children.Add(BuildMaxCard());
        content.Children.Add(BuildProScanCard());
        content.Children.Add(BuildProPerksCard());
        content.Children.Add(BuildMaxPerksCard());

        scroll.Content = content;
        page.Children.Add(scroll);
        return page;
    }

    // ---------- Naxi MAX ----------

    private Border BuildMaxCard()
    {
        var card = new Border
        {
            Style = (Style)FindResource("Card"),
            Padding = new Thickness(26),
            Margin = new Thickness(0, 18, 0, 0),
            Cursor = _config.IsMax ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
            BorderThickness = new Thickness(1)
        };
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(18),
            Background = new LinearGradientBrush(Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x5B, 0x21, 0xB6), new Point(0, 0), new Point(1, 1)),
            Child = new TextBlock { Text = _config.IsMax ? "✓" : "⚡", FontSize = 26, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(icon, 0); row.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 18, 0) };
        var maxTime = MaxTimeLeftText(_config.MaxExpiresAt);
        text.Children.Add(T(_config.IsMax ? "Naxi MAX unlocked" : "Upgrade to MAX", 18, (Brush)FindResource("Text"), true));
        text.Children.Add(T(_config.IsMax ? (maxTime ?? "This invite is bound to this computer.") : "Everything in Pro + exclusive MAX powers", 13, (Brush)FindResource("Muted"), false, 4));
        Grid.SetColumn(text, 1); row.Children.Add(text);
        if (!_config.IsMax)
        {
            var arrow = new TextBlock { Text = "›", FontSize = 34, Foreground = new SolidColorBrush(Color.FromRgb(0x9F, 0x92, 0xFF)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
            card.MouseLeftButtonUp += (_, _) => ShowProInviteDialog(allowWhenActive: true);
        }
        card.Child = row;
        return card;
    }

    private FrameworkElement MaxPerkRow(string title, string desc)
    {
        var locked = !_config.IsMax;
        var g = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(13),
            Background = locked ? new SolidColorBrush(Color.FromRgb(24, 30, 40)) : new LinearGradientBrush(Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x5B, 0x21, 0xB6), new Point(0, 0), new Point(1, 1)),
            Child = new TextBlock { Text = locked ? "🔒" : "✓", FontSize = 17, Foreground = locked ? (Brush)FindResource("Muted") : Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(icon, 0); g.Children.Add(icon);
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 10, 0) };
        t.Children.Add(T(title, 14.5, (Brush)FindResource("Text"), true));
        t.Children.Add(T(desc, 12, (Brush)FindResource("Muted"), false, 2));
        Grid.SetColumn(t, 1); g.Children.Add(t);
        var tag = new TextBlock { Text = locked ? "🔒" : "✓", FontSize = 15, Foreground = locked ? (Brush)FindResource("Muted") : new SolidColorBrush(Color.FromRgb(0x9F, 0x92, 0xFF)), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(tag, 2); g.Children.Add(tag);
        return g;
    }

    private Border BuildMaxPerksCard()
    {
        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(26), Margin = new Thickness(0, 18, 0, 0), BorderBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)) };
        var host = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(T("What you get with MAX", 18, (Brush)FindResource("Text"), true));
        var badge = new Border
        {
            Background = new LinearGradientBrush(Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x5B, 0x21, 0xB6), new Point(0, 0), new Point(1, 1)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 3, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Child = new TextBlock { Text = "MAX", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
        };
        head.Children.Add(badge);
        host.Children.Add(head);

        host.Children.Add(MaxPerkRow("Server Hopper", "Browse live servers of any game and jump into the exact one you want."));
        host.Children.Add(MaxPerkRow("Recently played + playtime", "Your last 8 games with total time played, right above the grid."));
        host.Children.Add(MaxPerkRow("Quick launch hotkey (Alt+R)", "Instant game search from anywhere — type, Enter, you are in."));
        host.Children.Add(MaxPerkRow("Auto Discord presence", "Rich Presence switches to your live Roblox game automatically."));
        host.Children.Add(MaxPerkRow("Exclusive MAX accents", "Nebula, Magenta and Toxic — colors no free plan can pick."));
        host.Children.Add(MaxPerkRow("MAX identity", "Purple branding, app icon and tray badge — your plan is visible everywhere."));
        host.Children.Add(MaxPerkRow("Everything in Pro", "Best-server join, deep ping scan, game profiles, emote wheel and more."));

        host.Children.Add(T("More coming soon: texture packs, cloud backups and game auto-tuning.", 12, (Brush)FindResource("Muted"), false, 14));
        card.Child = host;
        return card;
    }

    private Border BuildProScanCard()
    {
        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(26), Margin = new Thickness(0, 18, 0, 0) };
        var host = new StackPanel();

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(T("PC optimization", 18, (Brush)FindResource("Text"), true));
        var badge = new Border
        {
            Background = (Brush)FindResource("AccentGradient"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 3, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "PRO", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("AccentText") }
        };
        Grid.SetColumn(badge, 1); head.Children.Add(badge);
        host.Children.Add(head);

        host.Children.Add(T("Free scan shows an FPS forecast for your PC. Pro applies the optimal settings with one click.", 12.5, (Brush)FindResource("Muted"), false, 6));

        var scanBtn = Btn("Scan my PC", true, 170);
        scanBtn.Margin = new Thickness(0, 16, 0, 0);
        host.Children.Add(scanBtn);

        var result = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 18, 0, 0) };
        host.Children.Add(result);

        var status = T("", 12.5, (Brush)FindResource("Muted"), false, 10);
        host.Children.Add(status);

        scanBtn.Click += (_, _) =>
        {
            scanBtn.IsEnabled = false;
            status.Text = Localization.T("Scanning your PC...");
            result.Visibility = Visibility.Collapsed;
            try
            {
                var scan = PcScan.Run();
                FillScanResult(result, scan);
                result.Visibility = Visibility.Visible;
                status.Text = "";
            }
            catch
            {
                status.Text = Localization.T("Scan failed. Try again.");
            }
            scanBtn.IsEnabled = true;
            scanBtn.Content = Localization.T("Scan again");
        };

        card.Child = host;
        return card;
    }

    private void FillScanResult(StackPanel result, PcScanResult scan)
    {
        result.Children.Clear();

        var hw = new StackPanel();
        hw.Children.Add(T("Your hardware", 13, (Brush)FindResource("Accent"), true));
        hw.Children.Add(T($"CPU  ·  {scan.Cpu}  ·  {scan.Threads} {Localization.T("threads")}", 12.5, (Brush)FindResource("Text"), false, 6));
        hw.Children.Add(T($"GPU  ·  {scan.Gpu}", 12.5, (Brush)FindResource("Text"), false, 3));
        hw.Children.Add(T(scan.RamGb > 0 ? $"RAM  ·  {scan.RamGb.ToString("0.#", CultureInfo.InvariantCulture)} GB" : "RAM  ·  —", 12.5, (Brush)FindResource("Text"), false, 3));
        result.Children.Add(hw);

        var forecast = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        forecast.Children.Add(T("Estimated FPS gain with Pro", 13, (Brush)FindResource("Accent"), true));
        forecast.Children.Add(new TextBlock { Text = $"+{scan.FpsFrom}–{scan.FpsTo} FPS", FontSize = 34, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TitleGradient"), Margin = new Thickness(0, 4, 0, 0) });
        forecast.Children.Add(T("Estimate — the real gain depends on the game.", 11.5, (Brush)FindResource("Muted"), false, 2));
        result.Children.Add(forecast);

        var apply = new Button
        {
            Content = HasPro ? (object)Localization.T("Apply optimization") : $"🔒  {Localization.T("Apply optimization")}",
            Style = (Style)FindResource(HasPro ? "AccentPillButton" : "PillButton"),
            Width = 250,
            Height = 46,
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        apply.Click += async (_, _) => await TryApplyOptimizationAsync(apply);
        result.Children.Add(apply);
    }

    private async Task<bool> EnsureProAsync()
    {
        // Server-verified entitlement check (Pro or MAX) — a locally edited
        // config cannot bypass this.
        var check = await ProLicense.CheckAsync();
        if (!check.Ok)
        {
            MessageBox.Show(Localization.T("Could not verify Pro (no connection)."), "Naxi Pro", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!check.Pro && !check.Max)
        {
            _config.IsPro = false;
            _config.IsMax = false;
            RobloxLauncher.SaveConfig(_config);
            RefreshProPage();
            ShowProInviteDialog();
            return false;
        }
        if (_config.IsPro != check.Pro || _config.IsMax != check.Max)
        {
            _config.IsPro = check.Pro;
            _config.IsMax = check.Max;
            RobloxLauncher.SaveConfig(_config);
            RefreshProPage();
        }
        return true;
    }

    private async Task TryApplyOptimizationAsync(Button apply)
    {
        apply.IsEnabled = false;
        if (!await EnsureProAsync()) { apply.IsEnabled = true; return; }
        _config.FpsUnlock = true;
        _config.FpsLimit = 999;
        _config.NoShadows = true;
        _config.PerfMode = true;
        _config.NoPostFx = true;
        _config.NoTelemetry = true;
        _config.FutureLighting = false;
        RobloxLauncher.SaveConfig(_config);
        _fastFlags = BuildFastFlags();
        var applied = RobloxLauncher.ApplyFlags(_config);
        MessageBox.Show(Localization.T("Optimization applied!") + "\n" + applied, "Naxi Pro", MessageBoxButton.OK, MessageBoxImage.Information);
        RefreshProPage();
    }

    private Border BuildProPerksCard()
    {
        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(26), Margin = new Thickness(0, 18, 0, 0) };
        var host = new StackPanel();
        host.Children.Add(T("What you get with Pro", 18, (Brush)FindResource("Text"), true));

        host.Children.Add(ProPerkRow("Best-server join", "Joins one of the least loaded servers instead of a random match."));
        host.Children.Add(ProPerkRow("Deep ping scan", "Measures ~100 network nodes to find your lowest possible ping."));
        host.Children.Add(ProPerkRow("Per-game profiles", "Each game gets its own flag preset, applied on every launch."));
        host.Children.Add(ProPerkRow("Emote wheel studio", "Replace Roblox emote wheel textures with your own PNG images."));
        host.Children.Add(ProPerkRow("Personal PC optimization preset", "One-click optimal settings based on your hardware."));
        host.Children.Add(ProPerkRow("All future Pro features included", "New Pro perks arrive at no extra cost."));
        host.Children.Add(ProPerkRow("Server-verified activation", "Pro status is checked with the server, so it cannot be faked."));

        host.Children.Add(T("More coming soon: launcher themes, texture packs and game auto-tuning.", 12, (Brush)FindResource("Muted"), false, 14));
        card.Child = host;
        return card;
    }

    private FrameworkElement ProPerkRow(string title, string desc)
    {
        var locked = !HasPro;
        var g = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(13),
            Background = locked ? new SolidColorBrush(Color.FromRgb(24, 30, 40)) : (Brush)FindResource("AccentGradient"),
            Child = new TextBlock { Text = locked ? "🔒" : "✓", FontSize = 17, Foreground = locked ? (Brush)FindResource("Muted") : (Brush)FindResource("AccentText"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(icon, 0); g.Children.Add(icon);
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 10, 0) };
        t.Children.Add(T(title, 14.5, (Brush)FindResource("Text"), true));
        t.Children.Add(T(desc, 12, (Brush)FindResource("Muted"), false, 2));
        Grid.SetColumn(t, 1); g.Children.Add(t);
        var tag = new TextBlock { Text = locked ? "🔒" : "✓", FontSize = 15, Foreground = locked ? (Brush)FindResource("Muted") : (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(tag, 2); g.Children.Add(tag);
        return g;
    }

    // allowWhenActive: Pro users may open the dialog too — to upgrade to MAX with a code.
    private void ShowProInviteDialog(bool allowWhenActive = false)
    {
        if (!allowWhenActive && (_config.IsPro || _config.IsMax)) return;
        var dialog = new Window
        {
            Title = "Naxi Pro",
            Owner = this,
            Width = 560,
            Height = 560,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var shell = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderBrush = new SolidColorBrush(Color.FromRgb(50, 66, 84)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Background = new LinearGradientBrush(Color.FromRgb(10, 18, 31), Color.FromRgb(13, 25, 42), new Point(0, 0), new Point(1, 1))
        };
        var root = new Grid();
        var glow = new Ellipse { Width = 460, Height = 460, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -250, -180, 0), IsHitTestVisible = false };
        glow.Fill = new RadialGradientBrush(Color.FromArgb(68, 124, 183, 224), Color.FromArgb(0, 124, 183, 224));
        root.Children.Add(glow);

        var close = new Button
        {
            Content = "✕",
            FontSize = 23,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(18)
        };
        close.Style = (Style)FindResource("CloseCrossButton");
        close.Click += (_, _) => dialog.Close();
        root.Children.Add(close);

        var content = new StackPanel { Width = 430, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
        content.Children.Add(new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(18),
            Background = (Brush)FindResource("AccentGradient"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = "✦", FontSize = 26, Foreground = (Brush)FindResource("AccentText"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        var heading = T("Naxi Pro or MAX is invite only", 27, (Brush)FindResource("Text"), true, 18);
        heading.TextAlignment = TextAlignment.Center;
        content.Children.Add(heading);
        var subtitle = T("If you have an invite code, enter it below.", 15, (Brush)FindResource("Muted"), false, 8);
        subtitle.TextAlignment = TextAlignment.Center;
        content.Children.Add(subtitle);
        var codeLabel = T("Invite code", 12, (Brush)FindResource("Accent"), true, 38);
        codeLabel.TextAlignment = TextAlignment.Center;
        content.Children.Add(codeLabel);

        var boxes = new List<TextBox>();
        var busy = false;
        var status = new TextBlock
        {
            FontSize = 13,
            Foreground = (Brush)FindResource("Muted"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0),
            MinHeight = 20
        };
        var activate = Btn("Activate", true, 180);
        activate.Margin = new Thickness(0, 18, 0, 0);
        activate.HorizontalAlignment = HorizontalAlignment.Center;

        async Task TryRedeem()
        {
            if (busy) return;
            var code = string.Concat(boxes.Select(b => b.Text.Trim()));
            if (code.Length != 6)
            {
                status.Foreground = new SolidColorBrush(Color.FromRgb(232, 120, 120));
                status.Text = Localization.T("Enter the 6-character invite code.");
                return;
            }

            busy = true;
            foreach (var b in boxes) b.IsEnabled = false;
            activate.IsEnabled = false;
            status.Foreground = (Brush)FindResource("Muted");
            status.Text = Localization.T("Checking code...");

            var result = await ProLicense.RedeemAsync(code);
            if (result.Ok && (result.Pro || result.Max))
            {
                _config.IsPro = result.Pro;
                _config.IsMax = result.Max;
                _config.ProExpiresAt = result.ExpiresAt;
                if (result.Max) _config.MaxExpiresAt = result.ExpiresAt;
                RobloxLauncher.SaveConfig(_config);
                dialog.Close();
                RefreshProPage();
                return;
            }

            busy = false;
            foreach (var b in boxes) b.IsEnabled = true;
            activate.IsEnabled = true;
            status.Foreground = new SolidColorBrush(Color.FromRgb(232, 120, 120));
            status.Text = Localization.T(result.Error switch
            {
                "used" => "This code was already used.",
                "expired" => "This code has expired.",
                "network" => "Could not reach the server.",
                _ => "Invalid invite code."
            });
            boxes[0].Focus();
            boxes[0].SelectAll();
        }

        void FillCode(string raw)
        {
            var chars = raw.Where(char.IsLetterOrDigit).Take(6).Select(char.ToUpperInvariant).ToArray();
            for (var i = 0; i < boxes.Count; i++)
                boxes[i].Text = i < chars.Length ? chars[i].ToString() : "";
            if (chars.Length == 6) _ = TryRedeem();
            else boxes[Math.Clamp(chars.Length, 0, boxes.Count - 1)].Focus();
        }

        var codeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        for (var i = 0; i < 6; i++)
        {
            var index = i;
            var box = new TextBox
            {
                Style = (Style)FindResource("InputBox"),
                Width = 52,
                Height = 66,
                MaxLength = 1,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(0),
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                CharacterCasing = System.Windows.Controls.CharacterCasing.Upper
            };
            box.PreviewTextInput += (_, e) => e.Handled = e.Text.Any(c => !char.IsLetterOrDigit(c));
            System.Windows.DataObject.AddPastingHandler(box, (_, e) =>
            {
                if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText)) return;
                e.CancelCommand();
                FillCode(e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) as string ?? "");
            });
            box.TextChanged += (_, _) =>
            {
                if (box.Text.Length == 1 && index < boxes.Count - 1)
                    boxes[index + 1].Focus();
                if (boxes.Count == 6 && boxes.All(b => b.Text.Length == 1))
                    _ = TryRedeem();
            };
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Back && box.Text.Length == 0 && index > 0)
                {
                    boxes[index - 1].Focus();
                    boxes[index - 1].SelectAll();
                }
            };
            boxes.Add(box);
            codeRow.Children.Add(box);
        }
        content.Children.Add(codeRow);
        content.Children.Add(status);
        activate.Click += (_, _) => _ = TryRedeem();
        content.Children.Add(activate);
        root.Children.Add(content);
        shell.Child = root;
        dialog.Content = shell;
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; _ = TryRedeem(); }
            else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                FillCode(System.Windows.Clipboard.GetText());
            }
        };
        dialog.Loaded += (_, _) => boxes[0].Focus();
        dialog.ShowDialog();
    }

    private void SetAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enable)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe)) key.SetValue("NaxiBootstrap", exe);
            }
            else key.DeleteValue("NaxiBootstrap", false);
        }
        catch { }
    }

    private Grid BuildAbout()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel();
        title.Children.Add(T("About us", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };

        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(22) };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var avatar = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(32)
        };
        avatar.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(143, 196, 234), 0),
                new GradientStop(Color.FromRgb(140, 130, 220), 1)
            }
        };
        avatar.Child = new TextBlock { Text = "🦊", FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(avatar);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        info.Children.Add(T("Foxzy", 22, (Brush)FindResource("Text"), true));
        info.Children.Add(T("Creator of Naxi Bootstrap", 13.5, (Brush)FindResource("Muted"), false, 4));
        row.Children.Add(info);
        card.Child = row;
        host.Children.Add(card);

        var card2 = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(22), Margin = new Thickness(0, 12, 0, 0) };
        var row2 = new StackPanel { Orientation = Orientation.Horizontal };
        var avatar2 = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Background = Brushes.Transparent };
        avatar2.Clip = new EllipseGeometry(new Point(32, 32), 32, 32);
        var img2 = new Image { Width = 64, Height = 64, Stretch = Stretch.UniformToFill };
        avatar2.Child = img2;
        row2.Children.Add(avatar2);
        var info2 = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        info2.Children.Add(T("Liniks69", 22, (Brush)FindResource("Text"), true));
        info2.Children.Add(T("Co-owner of Naxi Bootstrap", 13.5, (Brush)FindResource("Muted"), false, 4));
        row2.Children.Add(info2);
        card2.Child = row2;
        host.Children.Add(card2);
        _ = LoadAboutAvatar(img2, "https://i.pinimg.com/736x/af/60/bb/af60bb72c465711a8c7c13174bb40fe4.jpg");

        var card3 = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(22), Margin = new Thickness(0, 12, 0, 0) };
        var row3 = new StackPanel { Orientation = Orientation.Horizontal };
        var avatar3 = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Background = Brushes.Transparent };
        avatar3.Clip = new EllipseGeometry(new Point(32, 32), 32, 32);
        var img3 = new Image { Width = 64, Height = 64, Stretch = Stretch.UniformToFill };
        avatar3.Child = img3;
        row3.Children.Add(avatar3);
        var info3 = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        info3.Children.Add(T("mxxnbyal", 22, (Brush)FindResource("Text"), true));
        info3.Children.Add(T("Beta-Tester and Idea Creator", 13.5, (Brush)FindResource("Muted"), false, 4));
        row3.Children.Add(info3);
        card3.Child = row3;
        host.Children.Add(card3);
        _ = LoadAboutAvatar(img3, "https://media.discordapp.net/attachments/1541918313280442440/1541918335426367559/image.png?ex=6a8f56da&is=6a8e055a&hm=e6b1bc1183cfeb31a93fee3fb3932e454d89ad57329345d45ceba25ecc073f35&=&format=webp&quality=lossless");

        host.Children.Add(T("OFFICIAL LINKS", 11.5, (Brush)FindResource("Muted"), true).With(margin: new Thickness(0, 24, 0, 0)));

        var links = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var tg = Btn("Telegram", false, 130); tg.Click += (_, _) => OpenLink(TelegramUrl);
        var web = Btn("Website", false, 130); web.Margin = new Thickness(10, 0, 0, 0); web.Click += (_, _) => OpenLink(WebsiteUrl);
        var dc = Btn("Discord", false, 130); dc.Margin = new Thickness(10, 0, 0, 0); dc.Click += (_, _) => OpenLink(DiscordUrl);
        links.Children.Add(tg); links.Children.Add(web); links.Children.Add(dc);
        host.Children.Add(links);

        Grid.SetRow(host, 1); g.Children.Add(host);
        return g;
    }

    private Grid BuildAccounts()
    {
        _accountsList = new StackPanel();
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel();
        title.Children.Add(T("Accounts", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        host.Children.Add(T("Add Roblox accounts and launch the game logged into the selected account.", 13, (Brush)FindResource("Muted"), false));

        var browserCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(24), Margin = new Thickness(0, 18, 0, 0) };
        var bs = new StackPanel();
        bs.Children.Add(SectionLabel("ADD VIA ROBLOX LOGIN"));
        bs.Children.Add(T("Opens the official Roblox login page. Log in once (2FA and captcha are handled by Roblox itself) and the account is added automatically. If you are already logged in on the page, the account is added instantly.", 12.5, (Brush)FindResource("Muted"), false, 4));
        var browserBtn = Btn("Add Account via Browser", true, 260);
        browserBtn.Margin = new Thickness(0, 16, 0, 0);
        browserBtn.Click += AddBrowserAccount_Click;
        bs.Children.Add(browserBtn);
        browserCard.Child = bs;
        host.Children.Add(browserCard);

        _accountStatus = T("", 12.5, (Brush)FindResource("Muted"), false, 12);
        host.Children.Add(_accountStatus);

        _accountsList.Margin = new Thickness(0, 18, 0, 0);
        host.Children.Add(_accountsList);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);
        RenderAccounts();
        return g;
    }

    private void RenderAccounts()
    {
        _accountsList.Children.Clear();
        var accounts = AccountStore.Load();
        if (accounts.Count == 0)
        {
            _accountsList.Children.Add(T("No accounts added yet.", 13, (Brush)FindResource("Muted"), false));
            return;
        }

        foreach (var acc in accounts)
        {
            var card = new Border { Style = (Style)FindResource("UpdateCard"), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 10) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarHolder = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), ClipToBounds = true };
            var avatar = new Image { Width = 44, Height = 44 };
            avatarHolder.Child = avatar;
            Grid.SetColumn(avatarHolder, 0); grid.Children.Add(avatarHolder);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            info.Children.Add(T(acc.Name, 14.5, (Brush)FindResource("Text"), true));
            var sub = new StackPanel { Orientation = Orientation.Horizontal };
            sub.Children.Add(T($"ID: {acc.UserId}", 11.5, (Brush)FindResource("Muted"), false));
            if (acc.UserId == _config.LastUsedAccountId)
                sub.Children.Add(T("  •  Last used", 11.5, (Brush)FindResource("Accent"), false));
            info.Children.Add(sub);
            Grid.SetColumn(info, 1); grid.Children.Add(info);

            var play = SmallBtn("Play", true, 96);
            play.Margin = new Thickness(0, 0, 8, 0);
            var account = acc;
            play.Click += (_, _) => PlayAccount(account);
            Grid.SetColumn(play, 2); grid.Children.Add(play);

            var del = SmallBtn("\u2715", false, 40);
            del.Click += (_, _) => { var list = AccountStore.Load(); list.RemoveAll(a => a.UserId == account.UserId); AccountStore.Save(list); RenderAccounts(); };
            Grid.SetColumn(del, 3); grid.Children.Add(del);

            card.Child = grid;
            _accountsList.Children.Add(card);
            _ = LoadAvatarAsync(avatar, acc.UserId);
        }
    }

    private async Task LoadAvatarAsync(Image img, long userId)
    {
        try
        {
            var json = await Http.GetStringAsync($"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png");
            using var doc = JsonDocument.Parse(json);
            var url = doc.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString();
            if (url == null) return;
            var bytes = await Http.GetByteArrayAsync(url);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            img.Source = bmp;
        }
        catch { }
    }

    private async Task LoadAboutAvatar(Image img, string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            img.Source = bmp;
        }
        catch { }
    }

    private async void AddBrowserAccount_Click(object sender, RoutedEventArgs e)
    {
        _accountStatus.Text = "Waiting for Roblox login...";
        var existing = AccountStore.Load().Select(a => a.UserId).ToHashSet();
        var win = new LoginWindow(existing) { Owner = this };
        win.ShowDialog();
        var token = win.SecurityToken;
        if (string.IsNullOrEmpty(token)) { _accountStatus.Text = "Login cancelled"; return; }
        _accountStatus.Text = "Checking token...";
        var (ok, msg, id, name) = await AccountStore.ValidateToken(token);
        if (!ok) { _accountStatus.Text = msg; return; }
        var accounts = AccountStore.Load();
        if (accounts.Any(a => a.UserId == id)) { _accountStatus.Text = $"{name} is already added"; return; }
        accounts.Add(new RobloxAccount { UserId = id, Name = name, ProtectedToken = AccountStore.Protect(token) });
        AccountStore.Save(accounts);
        _accountStatus.Text = $"Added {name}";
        RenderAccounts();
    }

    private void PlayAccount(RobloxAccount acc)
    {
        var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "play_debug.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
            File.AppendAllText(logFile, $"\r\n=== {DateTime.Now:HH:mm:ss} PlayAccount called for {acc.Name} (id={acc.UserId}) ===\r\n");
        }
        catch { }

        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { RobloxLauncher.LogDebug(logFile, "Roblox running, auto-closing"); _accountStatus.Text = Localization.T("Closing Roblox..."); foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta")) try { p.Kill(); } catch { } Thread.Sleep(1500); }

        var player = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        if (player == null)
        {
            RobloxLauncher.LogDebug(logFile, "Player not found");
            _accountStatus.Text = "Roblox not installed";
            return;
        }
        RobloxLauncher.LogDebug(logFile, $"Player: {player}");

        RobloxLauncher.LogDebug(logFile, "Calling RefreshTokensFromDat...");
        RobloxLauncher.RefreshTokensFromDat();

        RobloxLauncher.LogDebug(logFile, "Loading accounts from file...");
        var accounts = AccountStore.Load();
        RobloxLauncher.LogDebug(logFile, $"Loaded {accounts.Count} accounts");
        foreach (var a in accounts)
            RobloxLauncher.LogDebug(logFile, $"  Account: {a.Name} id={a.UserId} tokenLen={a.ProtectedToken.Length}");

        var loaded = accounts.FirstOrDefault(a => a.UserId == acc.UserId);
        if (loaded == null)
        {
            RobloxLauncher.LogDebug(logFile, $"Account {acc.Name} (id={acc.UserId}) NOT FOUND in store!");
            _accountStatus.Text = $"{acc.Name} not found in accounts";
            return;
        }
        acc = loaded;
        RobloxLauncher.LogDebug(logFile, $"Using account: {acc.Name} id={acc.UserId}");

        RobloxLauncher.LogDebug(logFile, "Unprotecting token...");
        string token;
        try
        {
            token = AccountStore.Unprotect(acc.ProtectedToken);
            RobloxLauncher.LogDebug(logFile, $"Token decrypted, length={token.Length}");
        }
        catch (Exception ex)
        {
            RobloxLauncher.LogDebug(logFile, $"Token decrypt FAILED: {ex.Message}");
            _accountStatus.Text = "Token decrypt error";
            return;
        }

        RobloxLauncher.LogDebug(logFile, "Writing to registry...");
        try
        {
            RobloxLauncher.SetRobloxCookie(token);
            RobloxLauncher.LogDebug(logFile, "Registry write OK");
        }
        catch (Exception ex)
        {
            RobloxLauncher.LogDebug(logFile, $"Registry write FAILED: {ex.Message}");
        }

        RobloxLauncher.LogDebug(logFile, "Writing to dat...");
        var datResult = RobloxLauncher.SetModernCookie(token);
        RobloxLauncher.LogDebug(logFile, $"Dat write result: {datResult}");

        if (datResult != "ok")
        {
            _accountStatus.Text = "Cookie error: " + datResult;
            return;
        }

        RobloxLauncher.LogDebug(logFile, "Applying flags...");
        RobloxLauncher.ApplyFlags(_config);

        _config.LastUsedAccountId = acc.UserId;
        RobloxLauncher.SaveConfig(_config);
        RobloxLauncher.LogDebug(logFile, $"Config saved, LastUsedAccountId={acc.UserId}");

        RobloxLauncher.LogDebug(logFile, "LAUNCHING ROBLOX...");
        _accountStatus.Text = "Launching " + acc.Name + "...";
        Process.Start(Path.Combine(player, "RobloxPlayerBeta.exe"));
        Close();
    }

    private Border? _newsDetailOverlay;
    private Grid BuildNews()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);
        var title = new StackPanel();
        title.Children.Add(T("News", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);
        _newsList.Margin = new Thickness(0, 18, 0, 0);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _newsList, ClipToBounds = true, Padding = new Thickness(0, 0, 12, 0) };
        SmoothScroll.SetEnabled(scroll, true);
        scroll.ScrollChanged += (_, e) =>
        {
            double o = Math.Clamp(e.VerticalOffset / 280.0, 0, 0.68);
            foreach (Border c in _newsList.Children.OfType<Border>())
            {
                if (c.Tag is Border overlay)
                    overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.18 + o, TimeSpan.FromMilliseconds(140)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            }
        };
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);
        _newsList.Children.Add(T("Loading news...", 13, (Brush)FindResource("Muted"), false, 20));
        // detail overlay (hidden)
        _newsDetailOverlay = new Border { Background = new SolidColorBrush(Color.FromArgb(210, 8, 10, 18)), CornerRadius = new CornerRadius(14), Visibility = Visibility.Collapsed, ClipToBounds = true };
        _newsDetailOverlay.MouseLeftButtonDown += (_, _) => HideNewsDetail();
        Grid.SetRowSpan(_newsDetailOverlay, 2); g.Children.Add(_newsDetailOverlay);
        _ = LoadNewsAsync();
        return g;
    }

    private async Task LoadNewsAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(NewsUrl);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<NewsEntry>>(json, opts);
            if (list != null) RenderNews(list);
        }
        catch
        {
            if (_newsList.Children.Count == 1) { _newsList.Children.Clear(); _newsList.Children.Add(T("Could not load news. Check internet or news.json on GitHub.", 13, (Brush)FindResource("Muted"), false, 20)); }
        }
    }

    private void RenderNews(List<NewsEntry> news)
    {
        _newsList.Children.Clear();
        if (news.Count == 0) { _newsList.Children.Add(T("No news yet.", 13, (Brush)FindResource("Muted"), false, 20)); return; }
        foreach (var n in news.OrderByDescending(x => { try { return DateTime.Parse(x.Date ?? ""); } catch { return DateTime.MinValue; } }))
        {
            var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(0), CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 0, 0, 18), ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(18, 21, 28)), Cursor = System.Windows.Input.Cursors.Hand };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(280) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var imageWrapper = new Border { CornerRadius = new CornerRadius(16, 16, 0, 0), ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(12, 15, 20)) };
            imageWrapper.Loaded += (s, _) => { var b = (Border)s; b.Clip = new RectangleGeometry(new Rect(0, 0, b.ActualWidth, b.ActualHeight + 16), 16, 16); };
            var imageHost = new Grid { ClipToBounds = true };
            var newsImg = new Image { Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(newsImg, BitmapScalingMode.HighQuality);
            imageHost.Children.Add(newsImg);
            if (!string.IsNullOrWhiteSpace(n.ImageUrl)) _ = LoadNewsImage(newsImg, n.ImageUrl);
            var gradient = new Border { VerticalAlignment = VerticalAlignment.Bottom, Height = 140, Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Color.FromArgb(230, 14, 18, 32), 1) } } };
            imageHost.Children.Add(gradient);
            var grayOverlay = new Border { Background = new SolidColorBrush(Color.FromRgb(14, 18, 32)), Opacity = 0.22 };
            imageHost.Children.Add(grayOverlay);
            card.Tag = grayOverlay;
            var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20, 0, 20, 18) };
            var titleText = string.IsNullOrWhiteSpace(n.Title) ? n.Text.Split('\n').FirstOrDefault()?.Trim() ?? "Update" : n.Title!;
            var t1 = new TextBlock { Text = titleText.ToUpper(), FontSize = 36, FontWeight = FontWeights.ExtraBold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
            try { if (!string.IsNullOrWhiteSpace(n.FontFamily)) t1.FontFamily = new FontFamily(n.FontFamily); } catch { }
            titlePanel.Children.Add(t1);
            var subtitle = string.IsNullOrWhiteSpace(n.Subtitle) ? "GAME UPDATE" : n.Subtitle!;
            var sub = new TextBlock { Text = $"{subtitle}  Posted { (DateTime.TryParse(n.Date, out var d) ? d.ToString("MMMM d'th' yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-US")) : n.Date ?? "") }", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(140, 145, 155)), Margin = new Thickness(0, 4, 0, 0), FontWeight = FontWeights.SemiBold };
            titlePanel.Children.Add(sub);
            imageHost.Children.Add(titlePanel);
            var authorBadge = new Border { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14, 14, 0, 0), Background = new SolidColorBrush(Color.FromArgb(170, 18, 21, 27)), CornerRadius = new CornerRadius(20), Padding = new Thickness(8, 5, 12, 5) };
            var authorRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var av = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
            if (n.Author == "Foxzy") { av.Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), GradientStops = { new GradientStop(Color.FromRgb(143, 196, 234), 0), new GradientStop(Color.FromRgb(140, 130, 220), 1) } }; av.Child = new TextBlock { Text = "🦊", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; }
            else { var ai = new Image { Width = 22, Height = 22, Stretch = Stretch.UniformToFill }; av.Child = ai; var url = n.AvatarUrl; if (string.IsNullOrWhiteSpace(url)) url = n.Author == "Liniks69" ? "https://i.pinimg.com/736x/af/60/bb/af60bb72c465711a8c7c13174bb40fe4.jpg" : n.Author == "mxxnbyal" ? "https://media.discordapp.net/attachments/1541918313280442440/1541918335426367559/image.png?ex=6a8f56da&is=6a8e055a&hm=e6b1bc1183cfeb31a93fee3fb3932e454d89ad57329345d45ceba25ecc073f35&=&format=webp&quality=lossless" : ""; if (!string.IsNullOrWhiteSpace(url)) _ = LoadAboutAvatar(ai, url); }
            authorRow.Children.Add(av);
            authorRow.Children.Add(new TextBlock { Text = n.Author, FontSize = 11.5, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) });
            authorBadge.Child = authorRow;
            imageHost.Children.Add(authorBadge);
            bool isNew = false;
            try { if (DateTime.TryParse(n.Date, out var isNewDate)) isNew = (DateTime.Now - isNewDate).TotalDays < 7; } catch { }
            if (isNew)
            {
                var newTag = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 14, 14, 0), Background = new SolidColorBrush(Color.FromRgb(94, 156, 200)), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 4, 10, 4) };
                newTag.Child = new TextBlock { Text = Localization.T("NEW"), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(10, 20, 32)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                imageHost.Children.Add(newTag);
            }
            imageWrapper.Child = imageHost;
            Grid.SetRow(imageWrapper, 0); grid.Children.Add(imageWrapper);
            var content = new StackPanel { Margin = new Thickness(20, 16, 20, 18) };
            if (!string.IsNullOrWhiteSpace(n.Text))
            {
                var body = n.Text;
                if (!string.IsNullOrWhiteSpace(n.Title) && body.StartsWith(n.Title!)) body = body.Substring(n.Title!.Length).TrimStart('\n', '\r', ' ');
                var tb = new TextBlock { Text = body, FontSize = 13.5, Foreground = new SolidColorBrush(Color.FromRgb(220, 222, 228)), TextWrapping = TextWrapping.Wrap, LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, MaxHeight = 72, TextTrimming = TextTrimming.CharacterEllipsis };
                if (!string.IsNullOrWhiteSpace(n.FontFamily)) { try { tb.FontFamily = new FontFamily(n.FontFamily); } catch { } }
                content.Children.Add(tb);
            }
            Grid.SetRow(content, 1); grid.Children.Add(content);
            card.Child = grid;
            card.MouseLeftButtonDown += (_, _) => ShowNewsDetail(n);
            _newsList.Children.Add(card);
        }
    }

    private void ShowNewsDetail(NewsEntry n)
    {
        if (_newsDetailOverlay == null) return;
        var outer = new Border { Background = new SolidColorBrush(Color.FromRgb(18, 21, 28)), CornerRadius = new CornerRadius(16), ClipToBounds = true, Margin = new Thickness(8) };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(320) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var imageWrapper = new Border { CornerRadius = new CornerRadius(16, 16, 0, 0), ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(12, 15, 20)) };
        imageWrapper.Loaded += (s2, _) => { var b2 = (Border)s2; b2.Clip = new RectangleGeometry(new Rect(0, 0, b2.ActualWidth, b2.ActualHeight + 16), 16, 16); };
        var imageHost = new Grid { ClipToBounds = true };
        var im = new Image { Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
        RenderOptions.SetCachingHint(im, CachingHint.Cache);
        imageHost.Children.Add(im);
        if (!string.IsNullOrWhiteSpace(n.ImageUrl)) _ = LoadNewsImage(im, n.ImageUrl);
        var grad = new Border { VerticalAlignment = VerticalAlignment.Bottom, Height = 160, Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Color.FromArgb(240, 16, 19, 28), 1) } } };
        imageHost.Children.Add(grad);
        var closeBtn = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 10, 0), Background = Brushes.Transparent, Width = 38, Height = 38, Cursor = System.Windows.Input.Cursors.Hand };
        closeBtn.Child = new TextBlock { Text = "✕", FontSize = 20, FontWeight = FontWeights.Light, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.65 } };
        closeBtn.MouseLeftButtonDown += (_, _) => HideNewsDetail();
        imageHost.Children.Add(closeBtn);
        try { if (DateTime.TryParse(n.Date, out var nd) && (DateTime.Now - nd).TotalDays < 7) { var nt = new Border { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14, 14, 0, 0), Background = new SolidColorBrush(Color.FromRgb(94, 156, 200)), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 4, 10, 4) }; nt.Child = new TextBlock { Text = Localization.T("NEW"), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(10, 20, 32)) }; imageHost.Children.Add(nt); } } catch { }
        imageWrapper.Child = imageHost;
        var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 0, 24, 20) };
        var titleText = string.IsNullOrWhiteSpace(n.Title) ? n.Text.Split('\n').FirstOrDefault()?.Trim() ?? "Update" : n.Title!;
        var t1 = new TextBlock { Text = titleText.ToUpper(), FontSize = 38, FontWeight = FontWeights.ExtraBold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
        try { if (!string.IsNullOrWhiteSpace(n.FontFamily)) t1.FontFamily = new FontFamily(n.FontFamily); } catch { }
        titlePanel.Children.Add(t1);
        var subtitle = string.IsNullOrWhiteSpace(n.Subtitle) ? "GAME UPDATE" : n.Subtitle!;
        titlePanel.Children.Add(new TextBlock { Text = $"{subtitle}  •  Posted { (DateTime.TryParse(n.Date, out var d) ? d.ToString("MMMM d'th' yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-US")) : n.Date ?? "") }  •  {n.Author}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(170, 175, 185)), Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold });
        imageHost.Children.Add(titlePanel);
        Grid.SetRow(imageWrapper, 0); grid.Children.Add(imageWrapper);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(24, 18, 24, 18) };
        var bodyText = n.Text;
        if (!string.IsNullOrWhiteSpace(n.Title) && bodyText.StartsWith(n.Title!)) bodyText = bodyText.Substring(n.Title!.Length).TrimStart('\n','\r',' ');
        var tb = new TextBlock { Text = bodyText, FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(220, 222, 228)), TextWrapping = TextWrapping.Wrap, LineHeight = 22, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        if (!string.IsNullOrWhiteSpace(n.FontFamily)) { try { tb.FontFamily = new FontFamily(n.FontFamily); } catch { } }
        scroll.Content = tb;
        Grid.SetRow(scroll, 1); grid.Children.Add(scroll);
        outer.Child = grid;
        _newsDetailOverlay.Child = outer;
        _newsDetailOverlay.Visibility = Visibility.Visible;
        _newsDetailOverlay.Opacity = 0;
        _newsDetailOverlay.CacheMode = new BitmapCache { EnableClearType = false, SnapsToDevicePixels = true };
        var tt = new TranslateTransform(0, 32); _newsDetailOverlay.RenderTransform = tt;
        RenderOptions.SetBitmapScalingMode(_newsDetailOverlay, BitmapScalingMode.LowQuality);
        _newsDetailOverlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } });
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(32, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } });
    }

    private void HideNewsDetail()
    {
        if (_newsDetailOverlay == null || _newsDetailOverlay.Visibility != Visibility.Visible) return;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => { _newsDetailOverlay.Visibility = Visibility.Collapsed; _newsDetailOverlay.CacheMode = null; };
        _newsDetailOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
        if (_newsDetailOverlay.RenderTransform is TranslateTransform tt)
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 16, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
    }

    private async Task LoadNewsImage(Image img, string url)
    {
        try
        {
            // 1) Honor direct image links (github raw, imgur, cdn.*, *.png/jpg/webp/gif...)
            // 2) Discord links: extract the actual attachment (cdn.discordapp.com / media.discordapp.net)
            //    if present; otherwise try /latest for the channel's latest attachment.
            //    When Discord blocks it (plain HTML, no token), we show a clean placeholder
            //    instead of a broken blank image.
            string resolved = url;
            if (url.IndexOf("discord.com/channels/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("discordapp.com/channels/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Discord message link: only a real attachment/cdn URL can be displayed
                // without a user token. If none is present, we'll show a placeholder.
                var cdn = TryExtractDiscordAttachment(url);
                if (!string.IsNullOrEmpty(cdn))
                    resolved = cdn;
            }

            var bytes = await Http.GetByteArrayAsync(resolved);
            // If Discord returned HTML (not an image), fall back to placeholder.
            if (bytes.Length > 2 &&
                bytes[0] == '<' /* '<' */ && (bytes[1] == '!' || bytes[1] == 'h')) // '<!' or '<h'
            {
                ApplyNewsPlaceholder(img, url);
                return;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            img.Source = bmp;
        }
        catch
        {
            ApplyNewsPlaceholder(img, url);
        }
    }

    /// <summary>Extract a cdn.discordapp.com/media.discordapp.net attachment URL from a Discord page/link if one exists.</summary>
    private static string? TryExtractDiscordAttachment(string url)
    {
        // Discord message links can't be turned into image URLs without a user token.
        // We only handle direct cdn.discordapp.com / media.discordapp.net attachments,
        // which the author should put into news.json. Anything else → placeholder.
        if (url.IndexOf("cdn.discordapp.com/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            url.IndexOf("media.discordapp.net/", StringComparison.OrdinalIgnoreCase) >= 0)
            return url;
        return null;
    }

    private static void ApplyNewsPlaceholder(Image img, string url)
    {
        // A subtle frosted-gradient so the card never looks broken (Discord links are
        // often token-gated; a clear placeholder looks better than a black void).
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            using (var ms = new MemoryStream())
            {
                var b = new System.Drawing.Bitmap(2, 2);
                using (var g = System.Drawing.Graphics.FromImage(b))
                {
                    g.Clear(System.Drawing.Color.FromArgb(0x8C, 0x12, 0x17, 0x24)); // dark navy glass
                }
                b.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            img.Source = bmp;
        }
        catch { }
    }

    // ================= Game Hub =================

    private Grid BuildGameHub()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        // title + refresh
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(T("Game Hub", 26, (Brush)FindResource("Text"), true));
        var refreshBtn = new Button
        {
            Style = (Style)FindResource("PillButton"),
            Content = "\uE72C",
            FontFamily = NavIconFont,
            FontSize = 14,
            Width = 42,
            Height = 34,
            Padding = new Thickness(0),
            Margin = new Thickness(14, 2, 0, 0)
        };
        ToolTipService.SetToolTip(refreshBtn, Localization.T("Refresh"));
        refreshBtn.RenderTransformOrigin = new Point(0.5, 0.5);
        refreshBtn.RenderTransform = new ScaleTransform(1, 1);
        refreshBtn.MouseEnter += (_, _) => { AnimScaleSpring(refreshBtn, 1.12, 200); refreshBtn.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(120))); };
        refreshBtn.MouseLeave += (_, _) => { AnimScaleSpring(refreshBtn, 1, 220); refreshBtn.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(140))); };
        refreshBtn.PreviewMouseLeftButtonDown += (_, _) => AnimScale(refreshBtn, 0.95, 90);
        refreshBtn.PreviewMouseLeftButtonUp += (_, _) => AnimScaleSpring(refreshBtn, 1.12, 160);
        refreshBtn.Click += (_, _) => { _ghLastRefresh = DateTime.MinValue; if (_ghSearch != null) _ghSearch.Text = ""; RefreshGameHubLive(force: true); };
        title.Children.Add(refreshBtn);

        // random game picker
        var randomBtn = new Button
        {
            Style = (Style)FindResource("PillButton"),
            Content = "🎲",
            FontSize = 13,
            Width = 42,
            Height = 34,
            Padding = new Thickness(0),
            Margin = new Thickness(8, 2, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        ToolTipService.SetToolTip(randomBtn, Localization.T("Random game"));
        randomBtn.MouseEnter += (_, _) => AnimScaleSpring(randomBtn, 1.12, 200);
        randomBtn.MouseLeave += (_, _) => AnimScaleSpring(randomBtn, 1, 220);
        randomBtn.PreviewMouseLeftButtonDown += (_, _) => AnimScale(randomBtn, 0.95, 90);
        randomBtn.PreviewMouseLeftButtonUp += (_, _) => AnimScaleSpring(randomBtn, 1.12, 160);
        randomBtn.Click += (_, _) => PickRandomGame();
        title.Children.Add(randomBtn);

        // sort cycler (Players → Visits → Name)
        _ghSortBtn = new Button
        {
            Style = (Style)FindResource("PillButton"),
            FontSize = 12,
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(8, 2, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        UpdateSortBtnLabel();
        _ghSortBtn.MouseEnter += (_, _) => AnimScaleSpring(_ghSortBtn, 1.06, 200);
        _ghSortBtn.MouseLeave += (_, _) => AnimScaleSpring(_ghSortBtn, 1, 220);
        _ghSortBtn.Click += (_, _) => CycleGameHubSort();
        title.Children.Add(_ghSortBtn);

        // genre cycler (All → each genre → All)
        _ghGenreBtn = new Button
        {
            Style = (Style)FindResource("PillButton"),
            FontSize = 12,
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(8, 2, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        UpdateGenreBtnLabel();
        _ghGenreBtn.MouseEnter += (_, _) => AnimScaleSpring(_ghGenreBtn, 1.06, 200);
        _ghGenreBtn.MouseLeave += (_, _) => AnimScaleSpring(_ghGenreBtn, 1, 220);
        _ghGenreBtn.Click += (_, _) => CycleGameHubGenre();
        title.Children.Add(_ghGenreBtn);
        Grid.SetRow(title, 0);
        g.Children.Add(title);

        // status + ping strip
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };

        var statusCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(16, 12, 18, 12), Width = 400, VerticalAlignment = VerticalAlignment.Top };
        var statusPanel = new StackPanel { Orientation = Orientation.Horizontal };
        _ghStatusDot = new Ellipse { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 10, 0), Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x90, 0x99)) };
        statusPanel.Children.Add(_ghStatusDot);
        var statusTexts = new StackPanel();
        statusTexts.Children.Add(T("Roblox Status", 13.5, (Brush)FindResource("Text"), true));
        _ghStatusText = new TextBlock { Text = Localization.T("Checking..."), FontSize = 12, Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        statusTexts.Children.Add(_ghStatusText);
        statusPanel.Children.Add(statusTexts);
        statusCard.Child = statusPanel;
        strip.Children.Add(statusCard);

        var pingCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(16, 12, 16, 12), Width = 240, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        var pingPanel = new StackPanel();
        var pingTitle = new StackPanel { Orientation = Orientation.Horizontal };
        pingTitle.Children.Add(T("Ping to Roblox", 13.5, (Brush)FindResource("Text"), true));
        if (HasPro) pingTitle.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x26, 0x46, 0xD0, 0x7C)),
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(8, 1, 0, 0),
            Padding = new Thickness(7, 1, 7, 2),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = _config.IsMax ? "MAX" : "PRO", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x46, 0xD0, 0x7C)) }
        });
        pingPanel.Children.Add(pingTitle);
        _ghPingValue = new TextBlock { Text = Localization.T("Checking..."), FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0), Foreground = (Brush)FindResource("Muted") };
        pingPanel.Children.Add(_ghPingValue);
        _ghPingSub = new TextBlock { Text = Localization.T("Ping to Roblox servers"), FontSize = 11, Foreground = (Brush)FindResource("Muted") };
        pingPanel.Children.Add(_ghPingSub);
        pingCard.Child = pingPanel;
        strip.Children.Add(pingCard);

        Grid.SetRow(strip, 1);
        g.Children.Add(strip);

        // search row
        var searchWrap = new Border { Style = (Style)FindResource("Card"), CornerRadius = new CornerRadius(12), Width = 440, Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        var searchGrid = new Grid();
        _ghSearch = new TextBox { Style = (Style)FindResource("InputBox"), Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 13, Margin = new Thickness(0, 0, 0, 1) };
        _ghSearch.KeyDown += GhSearch_KeyDown;
        _ghSearchHint = new TextBlock { Text = Localization.T("Search games or paste a Roblox link..."), FontSize = 13, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(13, 0, 0, 0), IsHitTestVisible = false };
        _ghSearch.TextChanged += (_, _) => { if (_ghSearchHint != null) _ghSearchHint.Visibility = _ghSearch!.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
        searchGrid.Children.Add(_ghSearch);
        searchGrid.Children.Add(_ghSearchHint);
        searchWrap.Child = searchGrid;
        searchWrap.RenderTransformOrigin = new Point(0.5, 0.5);
        searchWrap.RenderTransform = new ScaleTransform(1, 1);
        searchWrap.MouseEnter += (_, _) => { AnimScale(searchWrap, 1.02, 140); searchWrap.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.88, TimeSpan.FromMilliseconds(120))); };
        searchWrap.MouseLeave += (_, _) => { AnimScale(searchWrap, 1, 160); searchWrap.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(140))); };
        Grid.SetRow(searchWrap, 2);
        g.Children.Add(searchWrap);

        // folders row — chip bar of "All games" + user folders (right-click a folder chip to rename / delete)
        var folderRow = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        var folderTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        folderTitle.Children.Add(T("Folders", 12, (Brush)FindResource("Muted"), true));
        var newFolderBtn = new Button { Style = (Style)FindResource("PillButton"), Content = "+ " + Localization.T("New folder"), FontSize = 11.5, Height = 27, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1) };
        newFolderBtn.MouseEnter += (_, _) => AnimScale(newFolderBtn, 1.08, 140);
        newFolderBtn.MouseLeave += (_, _) => AnimScale(newFolderBtn, 1, 160);
        newFolderBtn.Click += (_, _) => ShowNewFolderDialog();
        folderTitle.Children.Add(newFolderBtn);
        folderRow.Children.Add(folderTitle);
        var folderScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _ghFolderBar, ClipToBounds = true, Margin = new Thickness(0, 10, 0, 0) };
        folderRow.Children.Add(folderScroll);
        Grid.SetRow(folderRow, 3);
        g.Children.Add(folderRow);

        // games grid
        _gamesList.Margin = new Thickness(0, 14, 0, 0);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _gamesList, ClipToBounds = true, Padding = new Thickness(0, 0, 12, 0) };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 4);
        g.Children.Add(scroll);
        _gamesList.Children.Add(T("Loading games...", 13, (Brush)FindResource("Muted"), false, 12));

        _ = LoadGameHubAsync();
        _ = RefreshGameHubStatusAsync();
        _ = RefreshGameHubPingAsync();

        // live refresh: online counts/status/ping every 60 s while the tab is open
        if (_ghLiveTimer != null) _ghLiveTimer.Stop();
        _ghLiveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _ghLiveTimer.Tick += (_, _) => { if (ReferenceEquals(PageHost.Content, _games)) RefreshGameHubLive(); };
        _ghLiveTimer.Start();
        return g;
    }

    private void RefreshGameHubLive(bool force = false)
    {
        if (!force && (DateTime.UtcNow - _ghLastRefresh).TotalSeconds < 45) return;
        _ghLastRefresh = DateTime.UtcNow;
        _ = LoadGameHubAsync();
        _ = RefreshGameHubStatusAsync();
        _ = RefreshGameHubPingAsync();
    }

    private async Task LoadGameHubAsync()
    {
        int seq = ++_ghLoadSeq;
        try
        {
            var placeIds = (await GameHubService.GetCuratedPlaceIdsAsync())
                .OrderByDescending(id => _config.FavoritePlaceIds.Contains(id)).ToList();
            var games = await GameHubService.GetGamesAsync(placeIds);
            foreach (var gm in games) gm.Favorite = _config.FavoritePlaceIds.Contains(gm.PlaceId);
            if (seq != _ghLoadSeq) return;
            _ghAllGames = games;
            RebuildFolderBar();
            ApplyHubView();
        }
        catch
        {
            if (seq == _ghLoadSeq) RenderGameCards(new List<HubGame>(), searchMode: false, failed: true);
        }
    }

    private void RenderGameCards(List<HubGame> games, bool searchMode, bool failed = false, string? emptyText = null)
    {
        _gamesList.Children.Clear();
        if (failed) { _gamesList.Children.Add(T("Could not load games. Check internet.", 13, (Brush)FindResource("Muted"), false, 12)); return; }
        if (games.Count == 0) { _gamesList.Children.Add(T(searchMode ? "No games found" : (emptyText ?? "Loading games..."), 13, (Brush)FindResource("Muted"), false, 12)); return; }

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        // the incoming list is already ordered (favorites first, then the active
        // sort); re-sorting here would destroy the PickRandomGame float-to-top.
        int cardIndex = 0;
        foreach (var game in games)
            wrap.Children.Add(BuildGameCard(game, cardIndex++));
        _gamesList.Children.Add(wrap);
    }

    private Border BuildGameCard(HubGame game, int index = 0)
    {
        var accent = (Brush)FindResource("Accent");
        var cardBorder = (Brush)FindResource("CardBorder");
        var surface = (Brush)FindResource("Surface");
        var surface2 = (Brush)FindResource("Surface2");

        var scaleXform = new ScaleTransform(1, 1);
        var liftXform = new TranslateTransform(0, 0);

        var card = new Border
        {
            Style = (Style)FindResource("Card"),
            Width = 168,
            Height = 200,
            Margin = new Thickness(0, 0, 14, 16),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true,
            Background = surface,
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { scaleXform, liftXform } }
        };

        // staggered spring pop-in
        {
            int delay = Math.Min(index * 24, 420);
            card.Opacity = 0;
            card.IsHitTestVisible = false;
            var popSx = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(360)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = BackOut(0.55) };
            var popSy = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(360)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = BackOut(0.55) };
            var popY = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(360)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = EaseOut() };
            var popO = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = EaseOut() };
            popSx.Completed += (_, _) => card.IsHitTestVisible = true;
            scaleXform.BeginAnimation(ScaleTransform.ScaleXProperty, popSx);
            scaleXform.BeginAnimation(ScaleTransform.ScaleYProperty, popSy);
            liftXform.BeginAnimation(TranslateTransform.YProperty, popY);
            card.BeginAnimation(UIElement.OpacityProperty, popO);
        }

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(112) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var imgWrap = new Border { CornerRadius = new CornerRadius(13, 13, 0, 0), ClipToBounds = true, Background = (Brush)FindResource("InputBg") };
        var img = new Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
        imgWrap.Child = img;
        Grid.SetRow(imgWrap, 0);
        grid.Children.Add(imgWrap);
        _ = LoadGameIconAsync(game, img);

        // favorite star (top-right overlay on the image)
        var star = new TextBlock
        {
            Text = game.Favorite ? "\uE735" : "\uE734",
            FontFamily = NavIconFont,
            FontSize = 14,
            Foreground = game.Favorite
                ? new SolidColorBrush(Color.FromRgb(0xE8, 0xC8, 0x60))
                : new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var starGrid = new Grid();
        starGrid.Children.Add(star);
        var starHost = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
            CornerRadius = new CornerRadius(9),
            Width = 28, Height = 28,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 7, 7, 0),
            Child = starGrid,
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        starHost.MouseLeftButtonUp += (_, e) => { e.Handled = true; ToggleGameFavorite(game, star, starHost); };
        starHost.MouseEnter += (_, _) => AnimScaleSpring(starHost, 1.2, 220);
        starHost.MouseLeave += (_, _) => AnimScaleSpring(starHost, 1, 240);
        Grid.SetRow(starHost,0);
        grid.Children.Add(starHost);

        // add-to-folder floating glass button (springs up from the corner on card hover)
        var accentColor = SolidColor(accent, Color.FromRgb(0x7C, 0xB7, 0xE0));
        var s2Color = SolidColor(surface2, Color.FromRgb(0x19, 0x1D, 0x25));
        var fbScale = new ScaleTransform(0.6, 0.6);
        var fbLift = new TranslateTransform(0, 8);
        var fbGlow = new System.Windows.Media.Effects.DropShadowEffect { Color = accentColor, BlurRadius = 14, ShadowDepth = 0, Opacity = 0 };
        var folderBtn = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0.55, 1),
                GradientStops =
                {
                    new GradientStop(LerpColor(s2Color, Colors.White, 0.24), 0),
                    new GradientStop(LerpColor(s2Color, Colors.Black, 0.22), 1)
                }
            },
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, accentColor.R, accentColor.G, accentColor.B)),
            Child = new Grid
            {
                Children =
                {
                    new System.Windows.Shapes.Ellipse
                    {
                        Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                        StrokeThickness = 1,
                        Margin = new Thickness(2),
                        IsHitTestVisible = false
                    },
                    new TextBlock
                    {
                        Text = "\uE8B7",
                        FontFamily = NavIconFont,
                        FontSize = 13,
                        Foreground = accent,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 6, 8, 8),
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { fbScale, fbLift } },
            Effect = fbGlow,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = Localization.T("Add to folder")
        };
        folderBtn.MouseEnter += (_, _) => { AnimScaleSpring(folderBtn, 1.16, 240); GlowTo(fbGlow, 0.55, 18, 180); };
        folderBtn.MouseLeave += (_, _) => { AnimScaleSpring(folderBtn, 1, 240); GlowTo(fbGlow, 0, 14, 220); };
        folderBtn.PreviewMouseLeftButtonDown += (_, _) => AnimScaleSpring(folderBtn, 0.88, 110);
        folderBtn.MouseLeftButtonUp += (_, e) => { e.Handled = true; AnimScaleSpring(folderBtn, 1.16, 160); ShowAddToFolderMenu(game, folderBtn); };
        Grid.SetRow(folderBtn,1);
        grid.Children.Add(folderBtn);
        var info = new StackPanel { Margin = new Thickness(10, 8, 10, 0) };
        var name = new TextBlock
        {
            Text = game.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Text"),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 34,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTipService.SetToolTip(name, game.Name + (game.Creator.Length > 0 ? " — " + game.Creator : ""));
        info.Children.Add(name);

        var onlineRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var liveDot = new Ellipse { Width = 7, Height = 7, Fill = (Brush)FindResource("Green"), VerticalAlignment = VerticalAlignment.Center };
        liveDot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(850)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = EaseOut() });
        onlineRow.Children.Add(liveDot);
        onlineRow.Children.Add(new TextBlock
        {
            Text = $" {FormatGameCount(game.Playing)} {Localization.T("playing")}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x46, 0xD0, 0x7C)),
            VerticalAlignment = VerticalAlignment.Center
        });
        info.Children.Add(onlineRow);

        info.Children.Add(new TextBlock
        {
            Text = $"{FormatGameCount(game.Visits)} {Localization.T("visits")}",
            FontSize = 10.5,
            Foreground = (Brush)FindResource("Muted"),
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetRow(info, 1);
        grid.Children.Add(info);

        card.Child = grid;

        // animatable colors + persistent glow → smooth color transitions, no effect swapping
        var cardGlow = new System.Windows.Media.Effects.DropShadowEffect { Color = accentColor, BlurRadius = 0, ShadowDepth = 0, Opacity = 0 };
        card.Effect = cardGlow;
        var borderColor = SolidColor(cardBorder, Color.FromRgb(0x20, 0x25, 0x2E));
        var surfaceColor = SolidColor(surface, Color.FromRgb(0x14, 0x17, 0x1E));
        var surface2Color = SolidColor(surface2, Color.FromRgb(0x19, 0x1D, 0x25));
        var borderBrushAnim = new SolidColorBrush(borderColor);
        var bgBrushAnim = new SolidColorBrush(surfaceColor);
        card.BorderBrush = borderBrushAnim;
        card.Background = bgBrushAnim;
        bool over = false;
        card.MouseEnter += (_, _) =>
        {
            over = true;
            borderBrushAnim.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(accentColor, TimeSpan.FromMilliseconds(220)) { EasingFunction = EaseOut() });
            bgBrushAnim.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(surface2Color, TimeSpan.FromMilliseconds(220)) { EasingFunction = EaseOut() });
            AnimScale(card, 1.045, 200);
            liftXform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -4, TimeSpan.FromMilliseconds(200)) { EasingFunction = EaseOut() });
            GlowTo(cardGlow, 0.45, 20, 240);
            folderBtn.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut() });
            fbScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = BackOut(0.6) });
            fbScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = BackOut(0.6) });
            fbLift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = BackOut(0.6) });
        };
        card.MouseLeave += (_, _) =>
        {
            over = false;
            borderBrushAnim.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(borderColor, TimeSpan.FromMilliseconds(260)) { EasingFunction = EaseOut() });
            bgBrushAnim.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(surfaceColor, TimeSpan.FromMilliseconds(260)) { EasingFunction = EaseOut() });
            AnimScale(card, 1, 220);
            liftXform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-4, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = EaseOut() });
            GlowTo(cardGlow, 0, 0, 260);
            folderBtn.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut() });
            fbScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.6, TimeSpan.FromMilliseconds(200)) { EasingFunction = EaseOut() });
            fbScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.6, TimeSpan.FromMilliseconds(200)) { EasingFunction = EaseOut() });
            fbLift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 8, TimeSpan.FromMilliseconds(200)) { EasingFunction = EaseOut() });
        };
        // left button: click-to-play OR drag the card onto a folder chip
        card.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _ghDragMoved = false;
            _ghDragStart = e.GetPosition(this);
            if (over) AnimScale(card, 0.97, 90);
        };
        card.PreviewMouseLeftButtonUp += (_, _) => { if (over) AnimScale(card, 1.045, 130); };
        card.MouseMove += (_, e) =>
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || _ghDragMoved) return;
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _ghDragStart.X) < 10 && Math.Abs(pos.Y - _ghDragStart.Y) < 10) return;
            _ghDragMoved = true;
            try { DragDrop.DoDragDrop(card, new System.Windows.DataObject("naxi-place", game.PlaceId), System.Windows.DragDropEffects.Move); } catch { }
        };
        card.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (_ghDragMoved) { _ghDragMoved = false; return; } // the click was consumed by a drag
            LaunchGame(game);
        };
        card.ContextMenu = BuildGameContextMenu(game);
        return card;
    }

    // right-click menu on a game card: play / favorite / folders / copy link / website
    private ContextMenu BuildGameContextMenu(HubGame game)
    {
        var menu = new ContextMenu();

        var play = new MenuItem { Header = Localization.T("Play") };
        play.Click += (_, _) => LaunchGame(game);
        menu.Items.Add(play);

        var hop = new MenuItem { Header = Localization.T("Server hopper") + (_config.IsMax ? "" : "  🔒") };
        hop.Click += (_, _) => ShowServerHopper(game);
        menu.Items.Add(hop);

        var fav = new MenuItem { Header = Localization.T("Favorite"), IsChecked = game.Favorite };
        fav.Click += (_, _) =>
        {
            if (_config.FavoritePlaceIds.Contains(game.PlaceId)) _config.FavoritePlaceIds.Remove(game.PlaceId);
            else _config.FavoritePlaceIds.Add(game.PlaceId);
            game.Favorite = _config.FavoritePlaceIds.Contains(game.PlaceId);
            var cached = _ghAllGames?.FirstOrDefault(x => x.PlaceId == game.PlaceId);
            if (cached != null) cached.Favorite = game.Favorite;
            RobloxLauncher.SaveConfig(_config);
            RebuildFolderBar();
            ApplyHubView();
        };
        menu.Items.Add(fav);

        var foldersMi = new MenuItem { Header = Localization.T("Add to folder") };
        foreach (var f in _config.GameFolders)
        {
            var fLocal = f;
            var mi = new MenuItem { Header = f.Name, IsChecked = f.PlaceIds.Contains(game.PlaceId) };
            mi.Click += (_, _) =>
            {
                if (fLocal.PlaceIds.Contains(game.PlaceId)) fLocal.PlaceIds.Remove(game.PlaceId);
                else fLocal.PlaceIds.Add(game.PlaceId);
                RobloxLauncher.SaveConfig(_config);
                RebuildFolderBar();
                ApplyHubView();
            };
            foldersMi.Items.Add(mi);
        }
        if (_config.GameFolders.Count > 0) foldersMi.Items.Add(new Separator());
        var newMi = new MenuItem { Header = Localization.T("New folder") };
        newMi.Click += (_, _) => ShowNewFolderDialogFor(game);
        foldersMi.Items.Add(newMi);
        menu.Items.Add(foldersMi);

        menu.Items.Add(new Separator());

        var copy = new MenuItem { Header = Localization.T("Copy link") };
        copy.Click += (_, _) =>
        {
            try { System.Windows.Clipboard.SetText($"https://www.roblox.com/games/{game.PlaceId}"); } catch { }
            if (_ghSearchHint != null)
            {
                var original = _ghSearchHint.Text;
                _ghSearchHint.Text = Localization.T("Copied to clipboard");
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
                t.Tick += (_, _) => { t.Stop(); _ghSearchHint.Text = original; };
                t.Start();
            }
        };
        menu.Items.Add(copy);

        var web = new MenuItem { Header = Localization.T("Open in browser") };
        web.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = $"https://www.roblox.com/games/{game.PlaceId}", UseShellExecute = true }); } catch { }
        };
        menu.Items.Add(web);
        return menu;
    }
    private void ToggleGameFavorite(HubGame game, TextBlock star, Border starHost)
    {
        if (_config.FavoritePlaceIds.Contains(game.PlaceId)) _config.FavoritePlaceIds.Remove(game.PlaceId);
        else _config.FavoritePlaceIds.Add(game.PlaceId);
        game.Favorite = _config.FavoritePlaceIds.Contains(game.PlaceId);
        RobloxLauncher.SaveConfig(_config);
        star.Text = game.Favorite ? "\uE735" : "\uE734";
        star.Foreground = game.Favorite
            ? new SolidColorBrush(Color.FromRgb(0xE8, 0xC8, 0x60))
            : new SolidColorBrush(Color.FromArgb(225, 255, 255, 255));

        // springy pop + burst ring when favoriting
        if (game.Favorite)
        {
            AnimScaleSpring(starHost, 1.38, 190);
            var settle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(210) };
            settle.Tick += (_, _) => { settle.Stop(); AnimScaleSpring(starHost, 1, 260); };
            settle.Start();
            PlayBurst((System.Windows.Controls.Grid)starHost.Child, Color.FromRgb(0xE8, 0xC8, 0x60));
        }
        else
        {
            AnimScaleSpring(starHost, 1, 240);
        }

        // favorites always float to the top of the visible grid
        var cached = _ghAllGames?.FirstOrDefault(x => x.PlaceId == game.PlaceId);
        if (cached != null) cached.Favorite = game.Favorite;
        RebuildFolderBar();
        ApplyHubView();
    }

    // ---------- folder bar / filtering ----------

    private void RebuildFolderBar()
    {
        if (_ghFolderBar == null) return;
        _ghFolderBar.Children.Clear();

        var accent = (Brush)FindResource("Accent");
        var cardBorder = (Brush)FindResource("CardBorder");
        var surface = (Brush)FindResource("Surface");
        var surface2 = (Brush)FindResource("Surface2");
        var text = (Brush)FindResource("Text");
        var muted = (Brush)FindResource("Muted");
        int delay = 0;

        void AddChip(string label, string glyph, string? filterKey)
        {
            bool active = _ghFolderFilter == filterKey;
            int count = filterKey switch
            {
                null => _ghAllGames?.Count ?? 0,
                "__fav__" => (_ghAllGames ?? new List<HubGame>()).Count(x => _config.FavoritePlaceIds.Contains(x.PlaceId)),
                _ => _config.GameFolders.FirstOrDefault(f => f.Name == filterKey) is GameFolder cf && _ghAllGames != null
                        ? _ghAllGames.Count(x => cf.PlaceIds.Contains(x.PlaceId))
                        : 0
            };

            var chip = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(13, 6, 13, 6),
                Margin = new Thickness(0, 0, 8, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1),
                BorderBrush = active ? accent : cardBorder,
                Background = active ? surface2 : surface,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = glyph, FontFamily = NavIconFont, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = active ? accent : muted });
            row.Children.Add(new TextBlock
            {
                Text = "  " + label,
                FontSize = 12,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = active ? accent : text
            });
            row.Children.Add(new TextBlock { Text = "  " + count, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Foreground = muted });
            chip.Child = row;

            chip.MouseEnter += (_, _) => AnimScaleSpring(chip, 1.07, 200);
            chip.MouseLeave += (_, _) => AnimScaleSpring(chip, 1, 220);
            chip.PreviewMouseLeftButtonDown += (_, _) => AnimScale(chip, 0.94, 90);
            chip.MouseLeftButtonUp += (_, e) => { e.Handled = true; AnimScaleSpring(chip, 1.07, 160); _ghFolderFilter = filterKey; RebuildFolderBar(); ApplyHubView(); };

            if (filterKey != null && filterKey != "__fav__")
            {
                // drop target: drag a game card onto the chip to add it to the folder
                chip.AllowDrop = true;
                chip.DragEnter += (_, e) =>
                {
                    if (!e.Data.GetDataPresent("naxi-place")) return;
                    e.Effects = System.Windows.DragDropEffects.Move;
                    e.Handled = true;
                    chip.BorderBrush = accent;
                    chip.Background = surface2;
                    AnimScaleSpring(chip, 1.1, 160);
                };
                chip.DragLeave += (_, _) =>
                {
                    chip.BorderBrush = active ? accent : cardBorder;
                    chip.Background = active ? surface2 : surface;
                    AnimScaleSpring(chip, 1, 180);
                };
                chip.Drop += (_, e) =>
                {
                    chip.BorderBrush = active ? accent : cardBorder;
                    chip.Background = active ? surface2 : surface;
                    AnimScaleSpring(chip, active ? 1.07 : 1, 180);
                    if (e.Data.GetData("naxi-place") is not long pid) return;
                    var folder = _config.GameFolders.FirstOrDefault(x => x.Name == filterKey);
                    if (folder == null) return;
                    if (!folder.PlaceIds.Contains(pid)) folder.PlaceIds.Add(pid);
                    RobloxLauncher.SaveConfig(_config);
                    RebuildFolderBar();
                    ApplyHubView();
                };

                var name = filterKey;
                var miRename = new MenuItem { Header = Localization.T("Rename") };
                miRename.Click += (_, _) => RenameFolderDialog(name);
                var miDelete = new MenuItem { Header = Localization.T("Delete") };
                miDelete.Click += (_, _) => DeleteFolder(name);
                var menu = new ContextMenu();
                menu.Items.Add(miRename);
                menu.Items.Add(miDelete);
                chip.ContextMenu = menu;
            }

            FadeSlideIn(chip, delay, 8);
            delay += 30;
            _ghFolderBar.Children.Add(chip);
        }

        AddChip(Localization.T("All games"), "\uE71D", null);
        AddChip(Localization.T("Favorites"), "\uE735", "__fav__");
        foreach (var f in _config.GameFolders) AddChip(f.Name, "\uE8B7", f.Name);
    }

    private List<HubGame> CurrentHubQuery()
    {
        var all = _ghAllGames ?? new List<HubGame>();
        IEnumerable<HubGame> q = all;
        if (_ghFolderFilter == "__fav__") q = all.Where(x => _config.FavoritePlaceIds.Contains(x.PlaceId));
        else if (_ghFolderFilter != null)
        {
            var f = _config.GameFolders.FirstOrDefault(x => x.Name == _ghFolderFilter);
            q = f == null ? Enumerable.Empty<HubGame>() : all.Where(x => f.PlaceIds.Contains(x.PlaceId));
        }
        if (_ghGenreFilter != null) q = q.Where(x => string.Equals(x.Genre, _ghGenreFilter, StringComparison.OrdinalIgnoreCase));

        // favorites always float to the top, then the chosen sort
        IEnumerable<HubGame> sorted = _config.GameHubSort switch
        {
            "visits" => q.OrderByDescending(x => x.Favorite).ThenByDescending(x => x.Visits),
            "name" => q.OrderByDescending(x => x.Favorite).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => q.OrderByDescending(x => x.Favorite).ThenByDescending(x => x.Playing),
        };
        var list = sorted.ToList();

        // 🎲 random pick floats to the top for this render only
        if (_ghLucky != 0)
        {
            var lucky = list.FirstOrDefault(x => x.PlaceId == _ghLucky);
            if (lucky != null) { list.Remove(lucky); list.Insert(0, lucky); }
        }
        return list;
    }

    private void ApplyHubView()
    {
        var list = CurrentHubQuery();
        RenderGameCards(list, searchMode: false, emptyText: Localization.T("Nothing here yet."));
        var recent = BuildRecentStrip();
        if (recent != null) _gamesList.Children.Insert(0, recent);
        _ghLucky = 0;
    }

    private string SortLabel(string key) => key switch
    {
        "visits" => Localization.T("Visits"),
        "name" => Localization.T("Name"),
        _ => Localization.T("Players")
    };

    private void UpdateSortBtnLabel()
    {
        if (_ghSortBtn != null) _ghSortBtn.Content = Localization.T("Sort") + ": " + SortLabel(_config.GameHubSort) + "  ▾";
    }

    private void CycleGameHubSort()
    {
        _config.GameHubSort = _config.GameHubSort switch { "players" => "visits", "visits" => "name", _ => "players" };
        RobloxLauncher.SaveConfig(_config);
        UpdateSortBtnLabel();
        ApplyHubView();
    }

    private void UpdateGenreBtnLabel()
    {
        if (_ghGenreBtn != null) _ghGenreBtn.Content = Localization.T("Genre") + ": " + (_ghGenreFilter ?? Localization.T("All")) + "  ▾";
    }

    private void CycleGameHubGenre()
    {
        var genres = (_ghAllGames ?? new List<HubGame>())
            .Select(x => x.Genre).Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
        if (genres.Count == 0) return;
        int idx = _ghGenreFilter == null ? -1
            : genres.FindIndex(g => string.Equals(g, _ghGenreFilter, StringComparison.OrdinalIgnoreCase));
        _ghGenreFilter = idx + 1 >= genres.Count ? null : genres[idx + 1];
        UpdateGenreBtnLabel();
        ApplyHubView();
    }

    private void PickRandomGame()
    {
        var list = CurrentHubQuery();
        if (list.Count == 0) return;
        _ghLucky = list[Random.Shared.Next(list.Count)].PlaceId;
        ApplyHubView();
    }

    // ---------- recently played (Naxi MAX) ----------

    private FrameworkElement? BuildRecentStrip()
    {
        if (!_config.IsMax || _config.RecentPlaceIds.Count == 0) return null;
        var host = new StackPanel { Margin = new Thickness(0, 2, 0, 10) };
        host.Children.Add(T(Localization.T("Recently played"), 12, (Brush)FindResource("Muted"), true));
        var scroller = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        int idx = 0;
        foreach (var pid in _config.RecentPlaceIds.Take(8))
        {
            var g = (_ghAllGames ?? new List<HubGame>()).FirstOrDefault(x => x.PlaceId == pid)
                    ?? new HubGame { PlaceId = pid, Name = "Place " + pid };
            panel.Children.Add(BuildRecentCard(g, idx++));
        }
        scroller.Content = panel;
        host.Children.Add(scroller);
        return host;
    }

    private Border BuildRecentCard(HubGame game, int index)
    {
        var cardBorder = (Brush)FindResource("CardBorder");
        var surface = (Brush)FindResource("Surface");
        var card = new Border
        {
            Width = 132,
            Height = 130,
            CornerRadius = new CornerRadius(11),
            Background = surface,
            BorderBrush = cardBorder,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ClipToBounds = true,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        var sp = new StackPanel();
        var imgWrap = new Border { Height = 72, CornerRadius = new CornerRadius(10, 10, 0, 0), ClipToBounds = true, Background = (Brush)FindResource("InputBg") };
        var img = new Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
        imgWrap.Child = img;
        _ = LoadGameIconAsync(game, img);
        sp.Children.Add(imgWrap);
        var info = new StackPanel { Margin = new Thickness(8, 5, 8, 0) };
        info.Children.Add(new TextBlock { Text = game.Name, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Text"), TextTrimming = TextTrimming.CharacterEllipsis });
        var secs = _config.PlaySeconds.TryGetValue(game.PlaceId.ToString(System.Globalization.CultureInfo.InvariantCulture), out var s) ? s : 0;
        if (secs > 0)
            info.Children.Add(new TextBlock { Text = FormatPlayTime(secs), FontSize = 10, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 2, 0, 0) });
        sp.Children.Add(info);
        card.Child = sp;
        card.MouseEnter += (_, _) => AnimScaleSpring(card, 1.06, 180);
        card.MouseLeave += (_, _) => AnimScaleSpring(card, 1, 200);
        card.MouseLeftButtonUp += (_, e) => { e.Handled = true; JoinServerAsync(game, null); };
        FadeSlideIn(card, Math.Min(index * 40, 240), 8);
        return card;
    }

    private static string FormatPlayTime(long seconds)
    {
        if (seconds >= 3600) return $"{seconds / 3600} h {(seconds % 3600) / 60} m";
        if (seconds >= 60) return $"{seconds / 60} m";
        return "< 1 m";
    }

    // ---------- folder dialogs ----------

    private void ShowPromptDialog(string title, string initial, string okTextKey, Action<string> onConfirm)
    {
        var dialog = new Window
        {
            Title = title,
            Owner = this,
            Width = 430,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var shell = new Border
        {
            CornerRadius = new CornerRadius(20),
            BorderBrush = new SolidColorBrush(Color.FromRgb(50, 66, 84)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.85, 0.85),
            Background = new LinearGradientBrush(Color.FromRgb(10, 18, 31), Color.FromRgb(13, 25, 42), new Point(0, 0), new Point(1, 1))
        };
        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(T(title, 17, (Brush)FindResource("Text"), true));

        var box = new TextBox { Style = (Style)FindResource("InputBox"), Margin = new Thickness(0, 14, 0, 0), FontSize = 13.5, Text = initial };
        root.Children.Add(box);

        var err = new TextBlock { Text = Localization.T("This folder already exists."), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x6A, 0x6A)), Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed, Opacity = 0 };
        root.Children.Add(err);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Style = (Style)FindResource("PillButton"), Content = Localization.T("Cancel"), MinWidth = 92, Height = 32, Cursor = System.Windows.Input.Cursors.Hand };
        var ok = new Button { Style = (Style)FindResource("PillButton"), Content = okTextKey, MinWidth = 92, Height = 32, Margin = new Thickness(8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
        btnRow.Children.Add(cancel);
        btnRow.Children.Add(ok);
        root.Children.Add(btnRow);
        shell.Child = root;
        dialog.Content = shell;

        var closing = false;
        void CloseAnimated()
        {
            if (closing) return;
            closing = true;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            var scale = new DoubleAnimation(1, 0.9, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += (_, _) => dialog.Close();
            shell.BeginAnimation(UIElement.OpacityProperty, fade);
            if (shell.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
            }
        }

        void Confirm()
        {
            var name = box.Text.Trim();
            if (name.Length == 0) return;
            if (_config.GameFolders.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                err.Visibility = Visibility.Visible;
                err.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
                return;
            }
            onConfirm(name);
            CloseAnimated();
        }

        ok.Click += (_, _) => Confirm();
        cancel.Click += (_, _) => CloseAnimated();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Confirm(); } };
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) CloseAnimated(); };
        dialog.Loaded += (_, _) =>
        {
            shell.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = EaseOut() });
            if (shell.RenderTransform is ScaleTransform st)
            {
                var sx = new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = EaseOut() };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, sx);
            }
            box.Focus();
            box.SelectAll();
        };
        dialog.ShowDialog();
    }

    private async Task LoadGameIconAsync(HubGame game, Image img)
    {
        try
        {
            var bytes = await GameHubService.GetIconBytesAsync(game.UniverseId, game.IconUrl);
            if (bytes == null || bytes.Length == 0) return;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            img.Source = bmp;
            img.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        }
        catch { }
    }

    private bool _ghSearching;

    private async void GhSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _ghSearch == null || _ghSearching) return;
        var q = _ghSearch.Text.Trim();
        if (q.Length == 0) { _ = LoadGameHubAsync(); return; }

        // Roblox link pasted? (roblox.com/games/{id}/... or ?placeid=) -> launch directly
        var m = System.Text.RegularExpressions.Regex.Match(q, @"games/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) m = System.Text.RegularExpressions.Regex.Match(q, @"placeid=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
        {
            LaunchGame(new HubGame { PlaceId = long.Parse(m.Groups[1].Value), Name = "Roblox" });
            return;
        }

        _ghSearching = true;
        int seq = ++_ghLoadSeq;
        _gamesList.Children.Clear();
        _gamesList.Children.Add(T("Loading games...", 13, (Brush)FindResource("Muted"), false, 12));
        try
        {
            var games = await GameHubService.SearchAsync(q);
            foreach (var gm in games) gm.Favorite = _config.FavoritePlaceIds.Contains(gm.PlaceId);
            games = games.OrderByDescending(g => g.Favorite).ThenByDescending(g => g.Playing).ToList();
            if (seq != _ghLoadSeq) return;
            RenderGameCards(games, searchMode: true);
        }
        finally { _ghSearching = false; }
    }
    private async void LaunchGame(HubGame game) => _ = JoinServerAsync(game, null);

    private async Task JoinServerAsync(HubGame game, string? serverId)
    {
        var player = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        if (player == null)
        {
            MessageBox.Show("Roblox is not installed yet. Install it from roblox.com first.", "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "play_debug.log");
        RobloxLauncher.LogDebug(logFile, $"Game Hub launch: place {game.PlaceId}");

        RobloxLauncher.RefreshTokensFromDat();
        var accounts = AccountStore.Load();
        var acc = accounts.FirstOrDefault(a => a.UserId == _config.LastUsedAccountId) ?? accounts.FirstOrDefault();
        if (acc != null)
        {
            var token = AccountStore.Unprotect(acc.ProtectedToken);
            RobloxLauncher.SetRobloxCookie(token);
            RobloxLauncher.SetModernCookie(token);
        }

        // NB: a self-minted roblox-player: URL cannot join a place — the client
        // only honors gameinfo/placelauncherurl tickets signed by the website and
        // silently ignores keys like placi: (it just opens the app home instead).
        // The official deep link roblox://experiences/start?placeId= makes the
        // client resolve the join itself with its own session (verified live:
        // the client logs "! Joining game ... place <id> at <server-ip>").
        // A running client swallows the deep link, so close it first — this also
        // lets the freshly written cookies above be picked up on a clean start.
        var running = Process.GetProcessesByName("RobloxPlayerBeta");
        if (running.Length > 0)
        {
            RobloxLauncher.LogDebug(logFile, $"Game Hub: closing {running.Length} running client(s)");
            foreach (var p in running) try { p.Kill(); } catch { }
            await Task.Delay(1500);
        }

        var url = $"roblox://experiences/start?placeId={game.PlaceId}";

        if (serverId != null)
        {
            // Naxi MAX server hopper: join the exact instance the user picked
            url += $"&gameInstanceId={serverId}";
            RobloxLauncher.LogDebug(logFile, $"Game Hub: joining chosen server {serverId}");
        }
        else if (HasPro)
        {
            // Pro: join a concrete server instead of a random match. Roblox does not
            // expose per-server ping, so we pick one of the eight least loaded public
            // instances (fewer players → less sim load, fewer latency spikes).
            RobloxLauncher.LogDebug(logFile, "Game Hub: Pro best-server lookup");
            var srv = await GameHubService.GetBestServerAsync(game.PlaceId);
            if (srv != null)
            {
                url += $"&gameInstanceId={srv.Id}";
                RobloxLauncher.LogDebug(logFile, $"Game Hub: picked server {srv.Id} ({srv.Playing}/{srv.MaxPlayers} players)");
            }
            else
            {
                RobloxLauncher.LogDebug(logFile, "Game Hub: no server list, falling back to auto-match");
            }
        }

        // LaunchFromUrlAsync applies the game profile/flags and starts the
        // player executable directly with the URL — no roblox-player: shell
        // round-trip through this app. The placeid= regex in GetPlaceInfoAsync
        // matches the deep link's placeId= too, so the profile still resolves.
        await RobloxLauncher.LaunchFromUrlAsync(url, _config);

        // auto-rejoin tracking (start the watcher before hiding the window)
        _lastJoinPlaceId = game.PlaceId;
        _lastJoinName = game.Name;
        _joinStartedUtc = DateTime.UtcNow;
        _robloxWasRunning = true;
        _robloxLastSeen = DateTime.UtcNow;
        _rejoinTries = 0;
        EnsureRejoinWatcher();

        // Naxi MAX: remember this launch for the "Recently played" strip
        if (_config.IsMax)
        {
            _config.RecentPlaceIds.Remove(game.PlaceId);
            _config.RecentPlaceIds.Insert(0, game.PlaceId);
            if (_config.RecentPlaceIds.Count > 8) _config.RecentPlaceIds.RemoveRange(8, _config.RecentPlaceIds.Count - 8);
            RobloxLauncher.SaveConfig(_config);
        }

        Close();
    }

    // ---------- auto-rejoin ----------

    // Roblox clients regularly drop with error 277/268/6 (network hiccups,
    // server restarts). When AutoRejoin is on we watch the player process:
    // if it dies soon after we launched it, a small overlay offers a 10-second
    // countdown that rejoins the same place automatically.
    private void EnsureRejoinWatcher()
    {
        if (_rejoinTimer != null) return;
        _rejoinTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _rejoinTimer.Tick += (_, _) => RejoinTick();
        _rejoinTimer.Start();
    }

    // Naxi MAX: drive Discord Rich Presence from the live Roblox session
    private void UpdateAutoRpc(bool inGame)
    {
        try
        {
            if (!_config.IsMax || !_config.RpcAutoGame) return;
            string? name = inGame && _lastJoinPlaceId != 0
                ? (string.IsNullOrWhiteSpace(_lastJoinName) ? "Roblox" : _lastJoinName)
                : null;
            if (DiscordRpcService.AutoGameName == name) return;
            DiscordRpcService.AutoGameName = name;
            DiscordRpcService.Update(_config);
        }
        catch { }
    }

    private void RejoinTick()
    {
        bool running = false;
        try { running = Process.GetProcessesByName("RobloxPlayerBeta").Length > 0; } catch { }
        if (running)
        {
            _robloxWasRunning = true;
            _robloxLastSeen = DateTime.UtcNow;
            // Naxi MAX: playtime tracking + live Discord presence
            if (_lastJoinPlaceId != 0)
            {
                var key = _lastJoinPlaceId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _config.PlaySeconds[key] = (_config.PlaySeconds.TryGetValue(key, out var sec) ? sec : 0) + 2;
                if (++_playSaveCounter >= 15) { _playSaveCounter = 0; RobloxLauncher.SaveConfig(_config); }
                UpdateAutoRpc(true);
            }
            return;
        }

        UpdateAutoRpc(false);

        bool gracePassed = (DateTime.UtcNow - _robloxLastSeen).TotalSeconds > 4;
        bool wasInSession = _robloxWasRunning && (DateTime.UtcNow - _joinStartedUtc).TotalSeconds > 25;
        if (!gracePassed || !wasInSession || _lastJoinPlaceId == 0) return;

        _robloxWasRunning = false;
        if (!_config.AutoRejoin) return;
        ShowRejoinOffer();
    }

    // ---------- server hopper (Naxi MAX) ----------

    private async void ShowServerHopper(HubGame game)
    {
        if (!_config.IsMax) { ShowProInviteDialog(allowWhenActive: true); return; }
        var surface = (Brush)FindResource("Surface");
        var dlg = new Window
        {
            Title = "Naxi Bootstrap",
            Width = 470,
            Height = 540,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize,
            Background = surface,
            Opacity = 0
        };
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var head = new StackPanel();
        head.Children.Add(T(Localization.T("Server hopper"), 18, (Brush)FindResource("Text"), true));
        head.Children.Add(T(game.Name, 12.5, (Brush)FindResource("Muted"), false, 2));
        Grid.SetRow(head, 0); root.Children.Add(head);

        var listScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0), ClipToBounds = true };
        SmoothScroll.SetEnabled(listScroll, true);
        var list = new StackPanel();
        listScroll.Content = list;
        Grid.SetRow(listScroll, 1); root.Children.Add(listScroll);

        var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var refresh = Btn(Localization.T("Refresh"), false, 120);
        var status = T("", 12, (Brush)FindResource("Muted"), false);
        status.VerticalAlignment = VerticalAlignment.Center;
        status.Margin = new Thickness(12, 0, 0, 0);
        foot.Children.Add(refresh);
        foot.Children.Add(status);
        Grid.SetRow(foot, 2); root.Children.Add(foot);
        dlg.Content = root;

        bool loading = false;
        async Task Load()
        {
            if (loading) return;
            loading = true;
            refresh.IsEnabled = false;
            status.Text = Localization.T("Loading servers...");
            list.Children.Clear();
            var servers = await GameHubService.GetServersAsync(game.PlaceId, 3);
            list.Children.Clear();
            status.Text = servers.Count == 0 ? "" : Localization.T("# servers").Replace("#", servers.Count.ToString());
            if (servers.Count == 0)
                list.Children.Add(T(Localization.T("No servers found"), 12.5, (Brush)FindResource("Muted"), false, 8));
            int i = 0;
            foreach (var s in servers.OrderBy(x => x.Playing).Take(60))
                list.Children.Add(BuildServerRow(game, s, ++i, dlg));
            refresh.IsEnabled = true;
            loading = false;
        }
        refresh.Click += (_, _) => _ = Load();
        dlg.Loaded += (_, _) =>
        {
            dlg.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            _ = Load();
        };
        dlg.Show();
    }

    private FrameworkElement BuildServerRow(HubGame game, RobloxServerEntry s, int index, Window dlg)
    {
        var accent = (Brush)FindResource("Accent");
        var row = new Border
        {
            Padding = new Thickness(12, 9, 12, 9),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 2, 0, 2),
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var idx = new TextBlock { Text = "#" + index, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(idx, 0); grid.Children.Add(idx);

        var mid = new StackPanel { Margin = new Thickness(12, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        mid.Children.Add(new TextBlock { Text = $"{s.Playing} / {s.MaxPlayers} {Localization.T("players")}", FontSize = 13, Foreground = (Brush)FindResource("Text") });
        var load = s.Playing / Math.Max(1d, (double)s.MaxPlayers);
        var barBg = new Border { Height = 5, CornerRadius = new CornerRadius(3), Background = (Brush)FindResource("InputBg"), Margin = new Thickness(0, 5, 0, 0) };
        barBg.Child = new Border { Height = 5, CornerRadius = new CornerRadius(3), Background = accent, Width = Math.Max(6, 200 * load), HorizontalAlignment = HorizontalAlignment.Left };
        mid.Children.Add(barBg);
        Grid.SetColumn(mid, 1); grid.Children.Add(mid);

        var join = SmallBtn(Localization.T("Join"), true, 90);
        join.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(join, 2); grid.Children.Add(join);

        row.Child = grid;
        row.MouseEnter += (_, _) => row.Background = (Brush)FindResource("NavHover");
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        async void DoJoin() { dlg.Close(); await JoinServerAsync(new HubGame { PlaceId = game.PlaceId, Name = game.Name }, s.Id); }
        join.Click += (_, _) => DoJoin();
        row.MouseLeftButtonUp += (_, e) => { e.Handled = true; DoJoin(); };
        FadeSlideIn(row, Math.Min(index * 24, 300), 6);
        return row;
    }

    // ---------- quick launch overlay (Naxi MAX, Alt+R) ----------

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private const int HotkeyId = 0xA11C;
    private Window? _quickLaunch;

    private void ApplyQuickHotkey()
    {
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero) return;
            UnregisterHotKey(helper.Handle, HotkeyId);
            if (_config.IsMax && _config.QuickHotkey)
                RegisterHotKey(helper.Handle, HotkeyId, 1 /* MOD_ALT */, 0x52 /* R */);
        }
        catch { }
    }

    private async void ShowQuickLaunch()
    {
        if (!_config.IsMax) { ShowProInviteDialog(allowWhenActive: true); return; }
        if (_quickLaunch != null) { _quickLaunch.Activate(); return; }

        var dlg = new Window
        {
            Title = "Quick Launch — Naxi Bootstrap",
            Width = 640,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            Opacity = 0
        };
        _quickLaunch = dlg;

        // Outer shadow + card — выглядит как встроенная панель лаунчера
        var shadow = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = Brushes.Transparent,
            Margin = new Thickness(14),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 28,
                ShadowDepth = 8,
                Opacity = 0.55,
                Direction = 270
            }
        };

        var card = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("CardBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true
        };
        // top accent line like launcher
        var accentLine = new Border
        {
            Height = 1,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(Color.FromRgb(0x8F, 0xC4, 0xEA), Color.FromRgb(0x8B, 0x5C, 0xF6), new Point(0, 0), new Point(1, 0)),
            Opacity = 0.9
        };

        var root = new Grid();
        root.Children.Add(card);
        root.Children.Add(accentLine);

        var body = new StackPanel { Margin = new Thickness(18) };
        card.Child = body;
        shadow.Child = root;

        // ——— Header: бренд лаунчера + бейдж MAX ———
        var header = new Grid { Margin = new Thickness(2, 2, 2, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var logo = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(11),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush(Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x5B, 0x21, 0xB6), new Point(0, 0), new Point(1, 1))
        };
        logo.Child = new TextBlock { Text = "N", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(logo, 0);
        header.Children.Add(logo);

        var titleStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock { Text = "Быстрый запуск", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Text") });
        titleStack.Children.Add(new TextBlock { Text = "Naxi Bootstrap", FontSize = 11, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var maxBadge = new Border
        {
            Background = new LinearGradientBrush(Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x5B, 0x21, 0xB6), new Point(0, 0), new Point(1, 1)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 4, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock { Text = "MAX  •  Alt+R", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
        };
        right.Children.Add(maxBadge);
        var closeBtn = new Button { Content = "✕", Width = 30, Height = 30, Style = (Style)FindResource("WindowButton"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Esc" };
        closeBtn.Click += (_, _) => CloseQuick();
        right.Children.Add(closeBtn);
        Grid.SetColumn(right, 2);
        header.Children.Add(right);

        body.Children.Add(header);

        // ——— Search box: большой, красивый, с иконкой ———
        var searchWrap = new Border
        {
            Background = (Brush)FindResource("InputBg"),
            BorderBrush = (Brush)FindResource("InputBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(0),
            ClipToBounds = true
        };
        var searchGrid = new Grid { Height = 48 };
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition());
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var mag = new TextBlock { Text = "⌕", FontSize = 18, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0), Opacity = 0.95 };
        Grid.SetColumn(mag, 0);
        searchGrid.Children.Add(mag);
        var box = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = (Brush)FindResource("Text"),
            CaretBrush = (Brush)FindResource("Text"),
            BorderThickness = new Thickness(0),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 0, 0, 0),
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(box, 1);
        searchGrid.Children.Add(box);
        // placeholder
        var placeholder = new TextBlock
        {
            Text = "Поиск игр или вставьте Roblox-ссылку…",
            FontSize = 14,
            Foreground = (Brush)FindResource("Muted"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            IsHitTestVisible = false,
            Opacity = 0.95
        };
        Grid.SetColumn(placeholder, 1);
        searchGrid.Children.Add(placeholder);

        var hintEnter = new Border
        {
            Background = (Brush)FindResource("NavShellBg"),
            BorderBrush = (Brush)FindResource("PillBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4, 8, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock { Text = "↵", FontSize = 11, Foreground = (Brush)FindResource("Muted"), FontWeight = FontWeights.SemiBold }
        };
        Grid.SetColumn(hintEnter, 2);
        searchGrid.Children.Add(hintEnter);

        searchWrap.Child = searchGrid;
        body.Children.Add(searchWrap);

        void UpdatePlaceholder() => placeholder.Visibility = string.IsNullOrWhiteSpace(box.Text) ? Visibility.Visible : Visibility.Collapsed;
        box.TextChanged += (_, _) => UpdatePlaceholder();

        // focus accent
        box.GotFocus += (_, _) => { searchWrap.BorderBrush = (Brush)FindResource("Accent"); searchWrap.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x7C, 0xB7, 0xE0), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.22 }; };
        box.LostFocus += (_, _) => { searchWrap.BorderBrush = (Brush)FindResource("InputBorder"); searchWrap.Effect = null; };

        // ——— Results ———
        var scroll = new ScrollViewer
        {
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 12, 0, 0),
            ClipToBounds = true
        };
        SmoothScroll.SetEnabled(scroll, true);
        var results = new StackPanel();
        scroll.Content = results;
        body.Children.Add(scroll);

        var footer = new Border
        {
            BorderBrush = (Brush)FindResource("CardBorder"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(-18, 14, -18, -18),
            Padding = new Thickness(18, 10, 18, 12),
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x13, 0x1A)),
            CornerRadius = new CornerRadius(0, 0, 18, 18)
        };
        footer.Child = new TextBlock { Text = "↵ Enter — запустить   •   Esc — закрыть   •   ↑↓ — выбор", FontSize = 11, Foreground = (Brush)FindResource("Muted"), HorizontalAlignment = HorizontalAlignment.Center };
        body.Children.Add(footer);

        dlg.Content = shadow;

        int selected = -1;
        void RefreshSelection()
        {
            for (int i = 0; i < results.Children.Count; i++)
                if (results.Children[i] is Border b)
                {
                    bool sel = i == selected;
                    b.Background = sel ? (Brush)FindResource("NavHover") : Brushes.Transparent;
                    b.BorderBrush = sel ? new SolidColorBrush(Color.FromRgb(0x3A, 0x41, 0x4E)) : Brushes.Transparent;
                    b.BorderThickness = sel ? new Thickness(1) : new Thickness(1);
                }
        }

        var debounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        void CloseQuick() { _quickLaunch = null; dlg.Close(); }

        async Task RunSearch()
        {
            var q = box.Text.Trim();
            results.Children.Clear();
            selected = -1;
            if (q.Length == 0)
            {
                var empty = new Border { Padding = new Thickness(14, 16, 14, 16), CornerRadius = new CornerRadius(12), Background = (Brush)FindResource("PillBg"), BorderBrush = (Brush)FindResource("PillBorder"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 0, 0) };
                empty.Child = new TextBlock { Text = "Начни печатать — найдём игру мгновенно", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Text") };
                results.Children.Add(empty);
                return;
            }
            var games = await GameHubService.SearchAsync(q);
            results.Children.Clear();
            int idx = 0;
            foreach (var gm in games.Take(7))
            {
                var g2 = gm;
                int myIdx = idx;
                var row = new Border
                {
                    Padding = new Thickness(12, 10, 12, 10),
                    CornerRadius = new CornerRadius(12),
                    Margin = new Thickness(0, 6, 0, 0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = g2
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var icon = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = (Brush)FindResource("PillBg"), BorderBrush = (Brush)FindResource("PillBorder"), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
                icon.Child = new TextBlock { Text = "🎮", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(icon, 0);
                grid.Children.Add(icon);
                var sp = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock { Text = gm.Name, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Text"), TextTrimming = TextTrimming.CharacterEllipsis });
                sp.Children.Add(new TextBlock { Text = $"{gm.Creator}  ·  {FormatGameCount(gm.Playing)} {Localization.T("playing")}", FontSize = 11, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 2, 0, 0) });
                Grid.SetColumn(sp, 1);
                grid.Children.Add(sp);
                var go = new Border { Background = (Brush)FindResource("AccentGradient"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 10, 6), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "Играть", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("AccentText") } };
                Grid.SetColumn(go, 2);
                grid.Children.Add(go);
                row.Child = grid;
                row.MouseEnter += (_, _) => { selected = myIdx; RefreshSelection(); };
                row.MouseLeftButtonUp += (_, e) => { e.Handled = true; CloseQuick(); JoinServerAsync(g2, null); };
                results.Children.Add(row);
                FadeSlideIn(row, (results.Children.Count) * 28, 7);
                idx++;
            }
            if (results.Children.Count == 0)
            {
                var nf = new Border { Padding = new Thickness(14, 12, 14, 12), CornerRadius = new CornerRadius(12), Background = (Brush)FindResource("PillBg"), BorderBrush = (Brush)FindResource("PillBorder"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 6, 0, 0) };
                nf.Child = new TextBlock { Text = Localization.T("No games found"), FontSize = 12.5, Foreground = (Brush)FindResource("Muted") };
                results.Children.Add(nf);
            }
            else
            {
                selected = 0;
                RefreshSelection();
                // ensure first visible
                scroll.ScrollToTop();
            }
        }

        // initial empty state
        _ = RunSearch();

        debounce.Tick += (_, _) => { debounce.Stop(); _ = RunSearch(); };
        box.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down)
            {
                if (results.Children.Count > 0) { selected = Math.Min(selected + 1, results.Children.Count - 1); RefreshSelection(); if (selected >= 0 && results.Children[selected] is FrameworkElement fe) fe.BringIntoView(); }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (results.Children.Count > 0) { selected = Math.Max(selected - 1, 0); RefreshSelection(); if (selected >= 0 && results.Children[selected] is FrameworkElement fe2) fe2.BringIntoView(); }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) { CloseQuick(); e.Handled = true; }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Border? target = null;
                if (selected >= 0 && selected < results.Children.Count && results.Children[selected] is Border sb && sb.Tag is HubGame) target = sb;
                else if (results.Children.Count > 0 && results.Children[0] is Border fb && fb.Tag is HubGame) target = fb;
                if (target != null && target.Tag is HubGame hit) { CloseQuick(); JoinServerAsync(hit, null); }
                else _ = RunSearch();
            }
        };
        dlg.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) CloseQuick(); };
        dlg.Closed += (_, _) => { if (ReferenceEquals(_quickLaunch, dlg)) _quickLaunch = null; };
        dlg.Loaded += (_, _) =>
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            dlg.BeginAnimation(Window.OpacityProperty, anim);
            var scale = new ScaleTransform(0.96, 0.96);
            shadow.RenderTransform = scale;
            shadow.RenderTransformOrigin = new Point(0.5, 0.0);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });
            box.Focus();
            UpdatePlaceholder();
        };
        dlg.Show();
        dlg.Activate();
    }

    private void ShowRejoinOffer()
    {
        if (_rejoinWindow != null) { _rejoinWindow.Activate(); return; }
        _rejoinWindow = BuildRejoinWindow(10);
        _rejoinWindow.Show();
    }

    private Window BuildRejoinWindow(int initialSeconds)
    {
        var surface = (Brush)FindResource("Surface");
        var dlg = new Window
        {
            Title = "Naxi Bootstrap",
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            Background = surface,
            Opacity = 0
        };

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(T(Localization.T("Disconnected"), 17, (Brush)FindResource("Text"), true));
        var msg = T(
            string.IsNullOrWhiteSpace(_lastJoinName)
                ? Localization.T("Roblox closed. Rejoin in # s?").Replace("#", initialSeconds.ToString())
                : Localization.T("Roblox closed. Rejoin # in # s?").Replace("#", _lastJoinName).Replace("#", initialSeconds.ToString()),
            13, (Brush)FindResource("Muted"), false, 6);
        msg.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(msg);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var rejoin = Btn(Localization.T("Rejoin now"), true, 150);
        var cancel = Btn(Localization.T("Cancel"), false, 110);
        cancel.Margin = new Thickness(10, 0, 0, 0);
        btnRow.Children.Add(rejoin);
        btnRow.Children.Add(cancel);
        panel.Children.Add(btnRow);

        dlg.Content = panel;

        int left = initialSeconds;
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        void DoRejoin()
        {
            t.Stop();
            var g = new HubGame { PlaceId = _lastJoinPlaceId, Name = _lastJoinName };
            _rejoinWindow = null;
            dlg.Close();
            RobloxLauncher.LogDebug(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "play_debug.log"), $"Auto-rejoin: place {_lastJoinPlaceId}");
            LaunchGame(g);
        }
        rejoin.Click += (_, _) => DoRejoin();
        cancel.Click += (_, _) => { t.Stop(); _rejoinWindow = null; dlg.Close(); };
        dlg.Closed += (_, _) => t.Stop();
        t.Tick += (_, _) =>
        {
            left--;
            if (left <= 0) { DoRejoin(); return; }
            msg.Text = string.IsNullOrWhiteSpace(_lastJoinName)
                ? Localization.T("Roblox closed. Rejoin in # s?").Replace("#", left.ToString())
                : Localization.T("Roblox closed. Rejoin # in # s?").Replace("#", _lastJoinName).Replace("#", left.ToString());
        };
        t.Start();

        dlg.Loaded += (_, _) =>
        {
            dlg.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            var sc = new ScaleTransform(0.92, 0.92);
            dlg.RenderTransform = sc;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = EaseOut() });
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = EaseOut() });
        };
        return dlg;
    }

    private void ShowNewFolderDialog() => ShowPromptDialog(Localization.T("New folder"), "", Localization.T("Create"), name =>
    {
        _config.GameFolders.Add(new GameFolder { Name = name });
        RobloxLauncher.SaveConfig(_config);
        _ghFolderFilter = name;
        RebuildFolderBar();
        ApplyHubView();
    });

    private void ShowNewFolderDialogFor(HubGame game) => ShowPromptDialog(Localization.T("New folder"), "", Localization.T("Create"), name =>
    {
        var f = new GameFolder { Name = name };
        f.PlaceIds.Add(game.PlaceId);
        _config.GameFolders.Add(f);
        RobloxLauncher.SaveConfig(_config);
        _ghFolderFilter = name;
        RebuildFolderBar();
        ApplyHubView();
    });

    private void RenameFolderDialog(string oldName) => ShowPromptDialog(Localization.T("Rename"), oldName, Localization.T("Save"), name =>
    {
        var f = _config.GameFolders.FirstOrDefault(x => x.Name == oldName);
        if (f == null) return;
        f.Name = name;
        RobloxLauncher.SaveConfig(_config);
        if (_ghFolderFilter == oldName) _ghFolderFilter = name;
        RebuildFolderBar();
        ApplyHubView();
    });

    private void DeleteFolder(string name)
    {
        var r = MessageBox.Show(this,
            Localization.T("Delete folder") + " \"" + name + "\"?\n" + Localization.T("Games in it will stay in your library."),
            "Naxi Bootstrap", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _config.GameFolders.RemoveAll(x => x.Name == name);
        RobloxLauncher.SaveConfig(_config);
        if (_ghFolderFilter == name) _ghFolderFilter = null;
        RebuildFolderBar();
        ApplyHubView();
    }

    private void ShowAddToFolderMenu(HubGame game, FrameworkElement anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var f in _config.GameFolders)
        {
            var fLocal = f;
            var mi = new MenuItem
            {
                Header = f.Name,
                IsChecked = f.PlaceIds.Contains(game.PlaceId),
                Icon = new TextBlock { Text = "\uE8B7", FontFamily = NavIconFont, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Accent") }
            };
            mi.Click += (_, _) =>
            {
                if (fLocal.PlaceIds.Contains(game.PlaceId)) fLocal.PlaceIds.Remove(game.PlaceId);
                else fLocal.PlaceIds.Add(game.PlaceId);
                RobloxLauncher.SaveConfig(_config);
                RebuildFolderBar();
                ApplyHubView();
            };
            menu.Items.Add(mi);
        }

        if (_config.GameFolders.Count > 0) menu.Items.Add(new Separator());
        var containing = _config.GameFolders.Where(x => x.PlaceIds.Contains(game.PlaceId)).ToList();
        if (containing.Count > 0)
        {
            var rem = new MenuItem { Header = Localization.T("Remove from folder") };
            rem.Click += (_, _) =>
            {
                foreach (var f in containing) f.PlaceIds.Remove(game.PlaceId);
                RobloxLauncher.SaveConfig(_config);
                RebuildFolderBar();
                ApplyHubView();
            };
            menu.Items.Add(rem);
        }
        var newMi = new MenuItem { Header = Localization.T("New folder") };
        newMi.Click += (_, _) => ShowNewFolderDialogFor(game);
        menu.Items.Add(newMi);
        menu.IsOpen = true;
    }

    private async Task RefreshGameHubStatusAsync()
    {
        var dot = _ghStatusDot;
        var txt = _ghStatusText;
        if (dot == null || txt == null) return;
        var status = await GameHubService.GetStatusAsync();
        if (status == null)
        {
            SetGhDot(dot, Color.FromRgb(0x8A, 0x90, 0x99), glow: false);
            txt.Text = Localization.T("Could not check status");
            return;
        }
        var color = status.Indicator switch
        {
            "none" => Color.FromRgb(0x46, 0xD0, 0x7C),
            "maintenance" => Color.FromRgb(0x7C, 0xB7, 0xE0),
            "minor" => Color.FromRgb(0xE8, 0xC8, 0x60),
            "major" => Color.FromRgb(0xE8, 0x6A, 0x6A),
            _ => Color.FromRgb(0x8A, 0x90, 0x99)
        };
        SetGhDot(dot, color, glow: status.Indicator != "unknown");
        txt.Text = status.Description.Length > 0 ? status.Description : status.Indicator;
    }
    private static void SetGhDot(Ellipse dot, Color c, bool glow)
    {
        dot.Fill = new SolidColorBrush(c);
        dot.Effect = glow
            ? new System.Windows.Media.Effects.DropShadowEffect { Color = c, BlurRadius = 9, ShadowDepth = 0, Opacity = 0.85 }
            : null;
    }

    private async Task RefreshGameHubPingAsync()
    {
        var value = _ghPingValue;
        var sub = _ghPingSub;
        if (value == null || sub == null) return;
        var res = await GameHubService.PingRobloxAsync(deep: HasPro);
        if (res == null)
        {
            value.Text = "—";
            value.Foreground = (Brush)FindResource("Muted");
            value.Effect = null;
            sub.Text = Localization.T("Ping to Roblox servers");
            sub.Foreground = (Brush)FindResource("Muted");
            return;
        }
        value.Text = $"{res.Ms} ms";
        var c = res.Ms < 45 ? Color.FromRgb(0x46, 0xD0, 0x7C)
            : res.Ms < 100 ? Color.FromRgb(0xE8, 0xC8, 0x60)
            : Color.FromRgb(0xE8, 0x6A, 0x6A);
        value.Foreground = new SolidColorBrush(c);
        if (res.Deep)
        {
            value.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = c, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 };
            sub.Text = Localization.T("Pro deep scan · best of # nodes").Replace("#", res.Nodes.ToString());
            sub.Foreground = (Brush)FindResource("Accent");
        }
        else
        {
            value.Effect = null;
            sub.Text = Localization.T("Ping to Roblox servers");
            sub.Foreground = (Brush)FindResource("Muted");
        }
    }

    private static string FormatGameCount(long n)
    {
        if (n >= 1_000_000_000) return (n / 1_000_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "B";
        if (n >= 1_000_000) return (n / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        if (n >= 1_000) return (n / 1_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        return n.ToString("N0", CultureInfo.InvariantCulture);
    }

    private Grid BuildLegal()
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.Margin = new Thickness(72, 22, 40, 0);

        var title = new StackPanel();
        title.Children.Add(T("Legal", 26, (Brush)FindResource("Text"), true));
        title.Children.Add(T("Read what you agree to when using Naxi Bootstrap.", 13, (Brush)FindResource("Muted"), false, 4));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) };
        _licenseTab = Btn("License", true, 150); _licenseTab.Height = 44; _licenseTab.Click += (_, _) => ShowLegalDoc("LICENSE.md", true);
        _privacyTab = Btn("Privacy Policy", false, 160); _privacyTab.Height = 44; _privacyTab.Margin = new Thickness(10, 0, 0, 0); _privacyTab.Click += (_, _) => ShowLegalDoc("PRIVACY_POLICY.md", false);
        tabs.Children.Add(_licenseTab); tabs.Children.Add(_privacyTab);

        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(28), Margin = new Thickness(0, 18, 0, 0) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Content = _legalContent;
        card.Child = scroll;
        Grid.SetRow(card, 1); g.Children.Add(card);

        ShowLegalDoc("LICENSE.md", true);
        return g;
    }

    private void ShowLegalDoc(string fileName, bool isLicense)
    {
        _licenseTab.Style = (Style)FindResource(isLicense ? "AccentPillButton" : "PillButton");
        _privacyTab.Style = (Style)FindResource(!isLicense ? "AccentPillButton" : "PillButton");
        LoadLegalDoc(fileName);
    }

    private void LoadLegalDoc(string fileName)
    {
        _legalContent.Children.Clear();
        var asm = typeof(MainWindow).Assembly;
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name == null)
        {
            _legalContent.Children.Add(T("Document not found.", 13, (Brush)FindResource("Muted"), false));
            return;
        }

        var textBrush = (Brush)FindResource("Text");
        var bodyBrush = new SolidColorBrush(Color.FromRgb(201, 201, 206));
        var mutedBrush = (Brush)FindResource("Muted");

        using var stream = asm.GetManifestResourceStream(name);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var t = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(t))
            {
                _legalContent.Children.Add(new Border { Height = 8 });
                continue;
            }
            if (t == "---")
            {
                _legalContent.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)), Margin = new Thickness(0, 10, 0, 10) });
                continue;
            }
            if (t.StartsWith("### "))
            {
                _legalContent.Children.Add(T(t[4..].Replace("**", ""), 14.5, textBrush, true, 10));
                continue;
            }
            if (t.StartsWith("## "))
            {
                _legalContent.Children.Add(T(t[3..].Replace("**", ""), 17, textBrush, true, 18));
                continue;
            }
            if (t.StartsWith("# "))
            {
                _legalContent.Children.Add(T(t[2..].Replace("**", ""), 21, textBrush, true, 0));
                continue;
            }
            if (t.StartsWith("- "))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                row.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 10, 0) });
                row.Children.Add(T(t[2..].Replace("**", "").Replace("`", ""), 13, bodyBrush, false));
                _legalContent.Children.Add(row);
                continue;
            }
            if (char.IsDigit(t[0]) && t.Length > 2 && t[1] == '.' && t[2] == ' ')
            {
                _legalContent.Children.Add(T(t.Replace("**", ""), 13, bodyBrush, false, 4));
                continue;
            }
            var isBold = t.StartsWith("**") && t.Contains("**", StringComparison.Ordinal) && t.LastIndexOf("**") > 2;
            var clean = t.Replace("**", "").Replace("`", "");
            _legalContent.Children.Add(T(clean, isBold ? 13.5 : 13, isBold ? textBrush : bodyBrush, isBold, 6));
        }
    }

    private TextBlock SectionLabel(string text)
        => new() { Text = Localization.T(text), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 4) };

    private Grid ToggleRow(string title, string desc, bool value, Action<bool> onChange)
    {
        var grid = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var s = new StackPanel();
        s.Children.Add(T(title, 14.5, (Brush)FindResource("Text"), true));
        s.Children.Add(T(desc, 12, (Brush)FindResource("Muted"), false, 3));
        Grid.SetColumn(s, 0); grid.Children.Add(s);
        var tg = new ToggleButton { Style = (Style)FindResource("ToggleSwitch"), IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
        tg.Checked += (_, _) => onChange(true);
        tg.Unchecked += (_, _) => onChange(false);
        Grid.SetColumn(tg, 1); grid.Children.Add(tg);
        return grid;
    }

    private TextBlock T(string text, double size, Brush color, bool bold, double top = 0)
        => new() { Text = Localization.T(text), FontSize = size, Foreground = color, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, top, 0, 0), TextWrapping = TextWrapping.Wrap };

    private Button Btn(string text, bool accent, int width)
        => new() { Content = Localization.T(text), Style = (Style)FindResource(accent ? "AccentPillButton" : "PillButton"), Width = width, Height = 46, VerticalAlignment = VerticalAlignment.Top };
}

internal static class TextBlockExtensions
{
    public static TextBlock With(this TextBlock tb, Thickness margin = default)
    {
        tb.Margin = margin;
        return tb;
    }
}
