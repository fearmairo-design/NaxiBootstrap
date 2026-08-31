using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
    public string RobloxVersion { get; set; } = "";
    public string FontName { get; set; } = "Segoe UI";
    public string BackgroundUrl { get; set; } = "";
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

    public static async Task<string?> GetPlaceNameAsync(string url)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(url);
            var m = System.Text.RegularExpressions.Regex.Match(decoded, @"placeid=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var placeId = m.Groups[1].Value;

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(4));
            var universeJson = await Http.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{placeId}/universe", cts.Token);
            using var u = JsonDocument.Parse(universeJson);
            var universeId = u.RootElement.GetProperty("universeId").GetInt64();

            var gamesJson = await Http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={universeId}", cts.Token);
            using var g = JsonDocument.Parse(gamesJson);
            return g.RootElement.GetProperty("data")[0].GetProperty("name").GetString();
        }
        catch
        {
            return null;
        }
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
        if (config.PerfMode) flags["DFIntDebugFRMQualityLevelOverride"] = "1";
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

    public static void LaunchFromUrl(string url, AppConfig config)
    {
        try
        {
            if (config.OpenRobloxLinks) RegisterProtocol(true);
            ApplyFlags(config);

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
    private const string AppVersion = "v1.7.5";

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

    private TextBox _fpsBox = new();
    private TextBlock _flagsStatus = new();
    private TextBlock _verdictText = new();

    private Button _fontBtn = new();
    private TextBox _bgUrlBox = new();
    private TextBlock _bgStatus = new();
    private TextBlock _skyStatus = new();
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
        Localization.Changed += () =>
        {
            _updateList = new StackPanel();
            _newsList = new StackPanel();
            _legalContent = new StackPanel();
            _accountsList = new StackPanel();
            _homeLeft = new StackPanel();
            _logCard = new Border();
            _licenseTab = new Button();
            _privacyTab = new Button();
            _home = BuildHome(); _news = BuildNews(); _accounts = BuildAccounts(); _fastFlags = BuildFastFlags(); _cursors = BuildCursors(); _emotes = BuildEmotes(); _maintenance = BuildMaintenance(); _settings = BuildSettings(); _about = BuildAbout(); _legal = BuildLegal();
            RenderLog(_log); _logCard.Visibility = _config.HideUpdateLog ? Visibility.Collapsed : Visibility.Visible;
            HomeNav.Content = Localization.T("Home");
            NewsNav.Content = Localization.T("News");
            AccountsNav.Content = Localization.T("Accounts");
            FlagsNav.Content = Localization.T("FastFlags");
            CursorsNav.Content = Localization.T("Cursors");
            EmotesNav.Content = Localization.T("Emotes");
            MaintenanceNav.Content = Localization.T("Maintenance");
            SettingsNav.Content = Localization.T("Settings");
            AboutNav.Content = Localization.T("About us");
            LegalNav.Content = Localization.T("Legal");
            PageHost.Content = _home; SetActive(HomeNav);
            _ = CheckForUpdatesAsync();
            _ = LoadNewsAsync();
        };
        if (_config.OpenRobloxLinks) RobloxLauncher.RegisterProtocol(true);
        FontFamily = new FontFamily(string.IsNullOrWhiteSpace(_config.FontName) ? "Segoe UI" : _config.FontName);

        _log = LoadLog();
        _home = BuildHome();
        _news = BuildNews();
        _accounts = BuildAccounts();
        _fastFlags = BuildFastFlags();
        _cursors = BuildCursors();
        _emotes = BuildEmotes();
        _maintenance = BuildMaintenance();
        _settings = BuildSettings();
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
        _ = CheckForUpdatesAsync();
        if (_config.DiscordRpcEnabled) DiscordRpcService.Start(_config);

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
                    _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); _trayIcon!.Visible = false; _minimizedToTray = false; };
                    // hook for second instance to restore
                    var helper = new System.Windows.Interop.WindowInteropHelper(this);
                    var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                    source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                    {
                        if (msg == 0x0401) { Show(); WindowState = WindowState.Normal; Activate(); if (_trayIcon != null) _trayIcon.Visible = false; _minimizedToTray = false; }
                        return IntPtr.Zero;
                    });
                } catch { }
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
        foreach (var b in new[] { HomeNav, NewsNav, AccountsNav, FlagsNav, CursorsNav, EmotesNav, MaintenanceNav, SettingsNav, AboutNav, LegalNav })
            b.Style = (Style)FindResource(b == active ? "NavButtonActive" : "NavButton");
    }

    private void Home_Click(object sender, RoutedEventArgs e) => SwitchPage(_home, HomeNav);
    private void News_Click(object sender, RoutedEventArgs e) { _ = LoadNewsAsync(); SwitchPage(_news, NewsNav); }
    private void Accounts_Click(object sender, RoutedEventArgs e) { RenderAccounts(); SwitchPage(_accounts, AccountsNav); }
    private void Flags_Click(object sender, RoutedEventArgs e) { CheckRobloxVersion(); SwitchPage(_fastFlags, FlagsNav); }
    private void Cursors_Click(object sender, RoutedEventArgs e) { RefreshCursorPreviews(); SwitchPage(_cursors, CursorsNav); }
    private void Emotes_Click(object sender, RoutedEventArgs e) { SwitchPage(_emotes, EmotesNav); }
    private void Maintenance_Click(object sender, RoutedEventArgs e) { RefreshSizes(); RefreshInstallInfo(); SwitchPage(_maintenance, MaintenanceNav); }
    private void Settings_Click(object sender, RoutedEventArgs e) => SwitchPage(_settings, SettingsNav);
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
        var studio = Btn("Studio", true, 110); studio.Margin = new Thickness(10, 0, 0, 0); studio.Click += Studio_Click;
        var discord = Btn("Discord", false, 110); discord.Margin = new Thickness(10, 0, 0, 0); discord.Click += (_, _) => OpenLink(DiscordUrl);
        buttons.Children.Add(_updateBtn); buttons.Children.Add(launch); buttons.Children.Add(studio); buttons.Children.Add(discord);
        left.Children.Add(buttons);

        Grid.SetColumn(left, 0); g.Children.Add(left);
        _homeLeft = left;

        _logCard = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(22), Margin = new Thickness(0, 8, 24, 0) };
        var logGrid = new Grid();
        logGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        logGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        logGrid.Children.Add(T("UPDATE LOG", 11.5, (Brush)FindResource("Muted"), true));
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 14, 0, 0) };
        scroll.Content = _updateList;
        Grid.SetRow(scroll, 1); logGrid.Children.Add(scroll);
        _logCard.Child = logGrid;
        Grid.SetColumn(_logCard, 1); g.Children.Add(_logCard);

        return g;
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var remote = await FetchRemoteLogAsync();
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
            var (tag, asset) = await GetLatestReleaseAsync();
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

        CheckRobloxVersion();
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
            var card = new Border { Style = (Style)FindResource("UpdateCard"), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 10) };
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
        ps.Children.Add(ToggleRow("Performance Mode", "Force the lowest render quality level.", _config.PerfMode, v => { _config.PerfMode = v; SaveAndApply(); }));
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
        var apply = Btn("Apply to Roblox", true, 170); apply.Click += (_, _) => SaveAndApply();
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
            if (string.IsNullOrEmpty(_config.BackgroundUrl) || !File.Exists(BgCacheFile)) return;
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = File.OpenRead(BgCacheFile);
            img.EndInit();
            img.Freeze();
            SetBackground(img);
            _bgUrlBox.Text = _config.BackgroundUrl;
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

    private static List<string> FindAllRobloxCursorsFolders()
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

    private static void EnsureMigratedBackups()
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

    private static bool TryExtractEmbeddedDefault(string resourceName, string destPath)
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

        var title = new StackPanel();
        title.Children.Add(T("Emote Wheel", 26, (Brush)FindResource("Text"), true));
        Grid.SetRow(title, 0); g.Children.Add(title);

        var host = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        host.Children.Add(T("Replace Roblox emote wheel textures with custom PNG images. Upload your own base image — @2x and @3x variants are applied automatically.", 13, (Brush)FindResource("Muted"), false));

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
        var applyBtn = Btn("Apply Emotes", true, 180);
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

    private void ApplyEmotes_Click(object sender, RoutedEventArgs e)
    {
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

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = host, ClipToBounds = true };
        SmoothScroll.SetEnabled(scroll, true);
        Grid.SetRow(scroll, 1); g.Children.Add(scroll);
        _settingsScroll = scroll;
        return g;
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
        info.Children.Add(T("Создатель Naxi Bootstrap", 13.5, (Brush)FindResource("Muted"), false, 4));
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
                newTag.Child = new TextBlock { Text = "NEW", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(10, 20, 32)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
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
        var closeBtn = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 14, 14, 0), Background = new SolidColorBrush(Color.FromArgb(160, 24, 28, 38)), CornerRadius = new CornerRadius(16), Width = 32, Height = 32, Cursor = System.Windows.Input.Cursors.Hand };
        closeBtn.Child = new TextBlock { Text = "✕", FontSize = 14, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        closeBtn.MouseLeftButtonDown += (_, _) => HideNewsDetail();
        imageHost.Children.Add(closeBtn);
        try { if (DateTime.TryParse(n.Date, out var nd) && (DateTime.Now - nd).TotalDays < 7) { var nt = new Border { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14, 14, 0, 0), Background = new SolidColorBrush(Color.FromRgb(94, 156, 200)), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 4, 10, 4) }; nt.Child = new TextBlock { Text = "NEW", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(10, 20, 32)) }; imageHost.Children.Add(nt); } } catch { }
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
            var bytes = await Http.GetByteArrayAsync(url);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            img.Source = bmp;
        } catch { }
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
