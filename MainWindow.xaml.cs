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
    private const string AppVersion = "v1.5.3";

    private const string GitHubRepo = "fearmairo-design/NaxiBootstrap";

    private const string DiscordUrl = "https://discord.gg/";
    private const string TelegramUrl = "https://t.me/";
    private const string WebsiteUrl = "https://github.com/fearmairo-design/NaxiBootstrap";

    private static string UpdateLogUrl => $"https://raw.githubusercontent.com/{GitHubRepo}/main/updatelog.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
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

    private static readonly string[] Fonts =
    {
        "Segoe UI", "Arial", "Calibri", "Verdana", "Georgia", "Consolas",
        "Comic Sans MS", "Impact", "Trebuchet MS", "Times New Roman"
    };

    private static readonly HttpClient Http = new();

    private readonly Grid _home;
    private readonly Grid _accounts;
    private readonly Grid _fastFlags;
    private readonly Grid _maintenance;
    private readonly Grid _settings;
    private readonly Grid _about;
    private readonly Grid _legal;
    private readonly StackPanel _updateList = new();
    private readonly StackPanel _legalContent = new();
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
    private readonly StackPanel _accountsList = new();

    private TextBlock _logsSizeText = new();
    private TextBlock _storageSizeText = new();
    private TextBlock _tempSizeText = new();
    private TextBlock _cleanStatus = new();
    private TextBlock _installInfo = new();
    private TextBlock _installStatus = new();

    private bool _updating;
    private bool _switching;
    private bool _robloxNeedsReapply;

    static MainWindow()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "NaxiBootstrap");
    }

    public MainWindow()
    {
        InitializeComponent();
        try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/icon.ico")); } catch { }

        _config = RobloxLauncher.LoadConfig();
        if (_config.OpenRobloxLinks) RobloxLauncher.RegisterProtocol(true);
        FontFamily = new FontFamily(string.IsNullOrWhiteSpace(_config.FontName) ? "Segoe UI" : _config.FontName);

        _log = LoadLog();
        _home = BuildHome();
        _accounts = BuildAccounts();
        _fastFlags = BuildFastFlags();
        _maintenance = BuildMaintenance();
        _settings = BuildSettings();
        _about = BuildAbout();
        _legal = BuildLegal();

        PageHost.Content = _home;
        SetActive(HomeNav);
        RenderLog(_log);
        _logCard.Visibility = _config.HideUpdateLog ? Visibility.Collapsed : Visibility.Visible;
        RestoreBackgroundFromCache();
        RobloxLauncher.RefreshTokensFromDat();
        try { foreach (var f in Directory.GetFiles(AppContext.BaseDirectory, "*.old")) File.Delete(f); } catch { }
        _ = CheckForUpdatesAsync();

        Loaded += (_, _) => PlayOpenAnimation();
        Closed += (_, _) => Application.Current.Shutdown();
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
        foreach (var b in new[] { HomeNav, AccountsNav, FlagsNav, MaintenanceNav, SettingsNav, AboutNav, LegalNav })
            b.Style = (Style)FindResource(b == active ? "NavButtonActive" : "NavButton");
    }

    private void Home_Click(object sender, RoutedEventArgs e) => SwitchPage(_home, HomeNav);
    private void Accounts_Click(object sender, RoutedEventArgs e) { RenderAccounts(); SwitchPage(_accounts, AccountsNav); }
    private void Flags_Click(object sender, RoutedEventArgs e) { CheckRobloxVersion(); SwitchPage(_fastFlags, FlagsNav); }
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
        var discord = Btn("Discord", false, 110); discord.Margin = new Thickness(10, 0, 0, 0); discord.Click += (_, _) => OpenLink(DiscordUrl);
        buttons.Children.Add(_updateBtn); buttons.Children.Add(launch); buttons.Children.Add(discord);
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
            }

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
            _robloxNeedsReapply = true;
            _updateBtn.IsEnabled = true;
            _updateText.Text = "Roblox updated — press Update to re-apply FastFlags";
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
        await DownloadAsync(asset, UpdateZip);

        _updateText.Text = "Installing...";
        _progress.Value = 100;
        _percent.Text = "100%";

        if (Directory.Exists(UpdateDir)) Directory.Delete(UpdateDir, true);
        ZipFile.ExtractToDirectory(UpdateZip, UpdateDir);

        var source = UpdateDir;
        if (Directory.GetFiles(UpdateDir).Length == 0 && Directory.GetDirectories(UpdateDir).Length == 1)
            source = Directory.GetDirectories(UpdateDir)[0];

        var appDir = AppContext.BaseDirectory;
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExe)) currentExe = Path.Combine(appDir, "NaxiBootstrap.exe");

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

        var newExe = Path.Combine(appDir, "NaxiBootstrap.exe");
        Process.Start(new ProcessStartInfo(newExe) { UseShellExecute = true });
        Environment.Exit(0);
    }

    private async Task<(string Tag, string? AssetUrl)> GetLatestReleaseAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(ReleasesApiUrl));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        string? asset = null;
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
            }
        }
        return (tag, asset);
    }

    private async Task<List<LogEntry>?> FetchRemoteLogAsync()
    {
        var json = await Http.GetStringAsync(UpdateLogUrl);
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
                row.Children.Add(T($"[{c.Type}] {c.Text}", 13, (Brush)FindResource("Muted"), false));
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
        ss.Children.Add(T("Replace the Roblox sky with your own PNG (512x512 works best). This modifies Roblox game files — use at your own risk. Close Roblox before changing.", 12.5, (Brush)FindResource("Muted"), false, 2));
        var skyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var setSky = Btn("Set Sky Image...", false, 170); setSky.Click += SetSky_Click;
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
        fs.Children.Add(T("Replace Roblox's default font (Builder Sans) with your own .ttf file. This modifies Roblox game files — use at your own risk. Close Roblox before changing.", 12.5, (Brush)FindResource("Muted"), false, 2));
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
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _skyStatus.Text = "Close Roblox first"; return; }
        var player = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        if (player == null) { _skyStatus.Text = "Roblox not found"; return; }
        var skyDir = Path.Combine(player, "PlatformContent", "pc", "textures", "sky");
        if (!Directory.Exists(skyDir)) { _skyStatus.Text = "Sky folder not found"; return; }

        var dlg = new OpenFileDialog { Title = "Choose sky image", Filter = "PNG image|*.png" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            Directory.CreateDirectory(SkyBackupDir);
            foreach (var dat in Directory.GetFiles(skyDir, "*.dat"))
            {
                var backup = Path.Combine(SkyBackupDir, Path.GetFileName(dat));
                if (!File.Exists(backup)) File.Copy(dat, backup, false);
                File.Copy(dlg.FileName, dat, true);
            }
            _skyStatus.Text = "Sky replaced. Restart Roblox to see it.";
        }
        catch
        {
            _skyStatus.Text = "Failed to replace sky";
        }
    }

    private void ResetSky_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _skyStatus.Text = "Close Roblox first"; return; }
        var player = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        if (player == null) { _skyStatus.Text = "Roblox not found"; return; }
        var skyDir = Path.Combine(player, "PlatformContent", "pc", "textures", "sky");
        if (!Directory.Exists(SkyBackupDir) || Directory.GetFiles(SkyBackupDir).Length == 0)
        {
            _skyStatus.Text = "No sky backup found";
            return;
        }
        try
        {
            Directory.CreateDirectory(skyDir);
            foreach (var f in Directory.GetFiles(SkyBackupDir))
                File.Copy(f, Path.Combine(skyDir, Path.GetFileName(f)), true);
            _skyStatus.Text = "Original sky restored";
        }
        catch
        {
            _skyStatus.Text = "Failed to restore sky";
        }
    }

    private void SetRobloxFont_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _rbxFontStatus.Text = "Close Roblox first"; return; }
        var player = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        if (player == null) { _rbxFontStatus.Text = "Roblox not found"; return; }
        var fontsDir = Path.Combine(player, "Content", "fonts");
        if (!Directory.Exists(fontsDir)) { _rbxFontStatus.Text = "Fonts folder not found"; return; }

        var targets = Directory.GetFiles(fontsDir, "BuilderSans*.*")
            .Where(f => f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (targets.Length == 0) { _rbxFontStatus.Text = "Builder Sans files not found"; return; }

        var dlg = new OpenFileDialog { Title = "Choose font file", Filter = "Font file|*.ttf;*.otf" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            Directory.CreateDirectory(FontBackupDir);
            foreach (var ttf in targets)
            {
                var backup = Path.Combine(FontBackupDir, Path.GetFileName(ttf));
                if (!File.Exists(backup)) File.Copy(ttf, backup, false);
                File.Copy(dlg.FileName, ttf, true);
            }
            _rbxFontStatus.Text = $"Font replaced ({targets.Length} file(s)). Restart Roblox.";
        }
        catch
        {
            _rbxFontStatus.Text = "Failed to replace font";
        }
    }

    private void ResetRobloxFont_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { _rbxFontStatus.Text = "Close Roblox first"; return; }
        var player = RobloxLauncher.FindPlayerFolders().FirstOrDefault();
        if (player == null) { _rbxFontStatus.Text = "Roblox not found"; return; }
        var fontsDir = Path.Combine(player, "Content", "fonts");
        if (!Directory.Exists(FontBackupDir) || Directory.GetFiles(FontBackupDir).Length == 0)
        {
            _rbxFontStatus.Text = "No font backup found";
            return;
        }
        try
        {
            Directory.CreateDirectory(fontsDir);
            foreach (var f in Directory.GetFiles(FontBackupDir))
                File.Copy(f, Path.Combine(fontsDir, Path.GetFileName(f)), true);
            _rbxFontStatus.Text = "Original font restored";
        }
        catch
        {
            _rbxFontStatus.Text = "Failed to restore font";
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
            ? "Roblox is not installed"
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
        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0)
        {
            _installStatus.Text = "Close Roblox first";
            return;
        }

        var confirm = MessageBox.Show(
            "This deletes the Roblox client files. Roblox will automatically re-download and reinstall on next launch.\n\nContinue?",
            "Reset Roblox", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var versions = Path.Combine(RobloxRoot, "Versions");
        if (!Directory.Exists(versions)) { _installStatus.Text = "Roblox is not installed"; return; }

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

        if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0)
        {
            RobloxLauncher.LogDebug(logFile, "Roblox is running, aborting");
            _accountStatus.Text = "Close Roblox first";
            return;
        }

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
        => new() { Text = text, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 4) };

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
        => new() { Text = text, FontSize = size, Foreground = color, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, top, 0, 0), TextWrapping = TextWrapping.Wrap };

    private Button Btn(string text, bool accent, int width)
        => new() { Content = text, Style = (Style)FindResource(accent ? "AccentPillButton" : "PillButton"), Width = width, Height = 46, VerticalAlignment = VerticalAlignment.Top };
}

internal static class TextBlockExtensions
{
    public static TextBlock With(this TextBlock tb, Thickness margin = default)
    {
        tb.Margin = margin;
        return tb;
    }
}
