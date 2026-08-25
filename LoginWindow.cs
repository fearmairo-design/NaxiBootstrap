using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace NaxiBootstrap;

internal class LoginWindow : Window
{
    private readonly WebView2 _webView = new();
    private readonly TextBlock _barText = new();
    private readonly StackPanel _barActions = new();
    private readonly Button _continueBtn;
    private readonly HttpClient _http = new();
    private readonly HashSet<long> _existingIds;
    private readonly DispatcherTimer _poll;
    private bool _captured;
    private bool _barShown;
    private bool _sessionDecided;
    private string? _pendingToken;
    private string? _lastSeenToken;

    public string? SecurityToken { get; private set; }

    public LoginWindow(HashSet<long> existingIds)
    {
        _existingIds = existingIds;
        Log("Login window opened");
        Title = "Naxi Bootstrap — Roblox Login";
        Width = 1000;
        Height = 780;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _http.DefaultRequestHeaders.Add("User-Agent", "NaxiBootstrap");
        _http.Timeout = TimeSpan.FromSeconds(12);

        var continueBtn = new Button
        {
            Content = "Continue",
            Padding = new Thickness(14, 5, 14, 5),
            Background = new SolidColorBrush(Color.FromRgb(124, 183, 224)),
            Foreground = new SolidColorBrush(Color.FromRgb(11, 20, 27)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 8, 0)
        };
        continueBtn.Click += (_, _) => Capture();
        _continueBtn = continueBtn;

        var logoutBtn = new Button
        {
            Content = "Use another account",
            Padding = new Thickness(12, 5, 12, 5),
            Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        logoutBtn.Click += (_, _) => UseAnotherAccount();

        _barText.Text = "Log in to the account you want to add";
        _barText.VerticalAlignment = VerticalAlignment.Center;
        _barActions.Orientation = Orientation.Horizontal;
        _barActions.VerticalAlignment = VerticalAlignment.Center;
        _barActions.Children.Add(continueBtn);
        _barActions.Children.Add(logoutBtn);

        var barGrid = new Grid();
        barGrid.ColumnDefinitions.Add(new ColumnDefinition());
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        barGrid.Children.Add(_barText);
        Grid.SetColumn(_barActions, 1);
        barGrid.Children.Add(_barActions);

        var topBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(18, 18, 18)),
            Padding = new Thickness(14, 8, 14, 8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = barGrid
        };

        var layout = new DockPanel();
        DockPanel.SetDock(topBar, Dock.Top);
        layout.Children.Add(topBar);
        layout.Children.Add(_webView);
        Content = layout;

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _poll.Tick += (_, _) => _ = CheckCookie();

        Loaded += async (_, _) =>
        {
            try
            {
                var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await _webView.EnsureCoreWebView2Async(environment);
                Log("WebView2 ready, opening roblox.com/login");
                _webView.CoreWebView2.NavigationCompleted += (_, _) => _ = CheckCookie();
                _poll.Start();
                _webView.Source = new Uri("https://www.roblox.com/login");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "WebView2 runtime is not installed on this PC.\n\nInstall it from:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703\n\n" + ex.Message,
                    "Naxi Bootstrap", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
            }
        };
    }

    private void SetBar(string text, bool showContinue, bool showLogout)
    {
        _barText.Text = text;
        _continueBtn.Visibility = showContinue ? Visibility.Visible : Visibility.Collapsed;
        _barActions.Visibility = showLogout ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task CheckCookie()
    {
        if (_captured) return;
        try
        {
            var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
            var cookie = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY");
            if (cookie == null || cookie.Value.Length <= 50) return;

            var token = cookie.Value;
            if (token == _lastSeenToken && _barShown) return;
            _lastSeenToken = token;
            Log($"Cookie detected (len {token.Length}), sessionDecided={_sessionDecided}");

            var (id, name) = await GetAccountInfoAsync(token);
            Log($"Account: {(name ?? "?")} (id {id})");
            var already = id != 0 && _existingIds.Contains(id);
            _pendingToken = token;

            if (already)
                SetBar($"{name} is already in the launcher — press \"Use another account\" and log in with a different account", false, true);
            else
                SetBar(name == null ? "You're logged in — press Continue to add this account" : $"You're logged in as {name} — press Continue to add it", true, true);
            _barShown = true;
        }
        catch (Exception ex)
        {
            Log("CheckCookie error: " + ex.Message);
        }
    }

    private void Capture()
    {
        if (_pendingToken == null) return;
        Log("Continue pressed — capturing token, closing window");
        _captured = true;
        SecurityToken = _pendingToken;
        Close();
    }

    private void UseAnotherAccount()
    {
        _pendingToken = null;
        _sessionDecided = true;
        _barShown = false;
        _lastSeenToken = null;
        SetBar("Clearing session...", false, false);
        Log("Use another account: clearing cookies");
        _ = UseAnotherAccountAsync();
    }

    private async Task UseAnotherAccountAsync()
    {
        try
        {
            var manager = _webView.CoreWebView2.CookieManager;
            manager.DeleteAllCookies();
            var leftovers = await manager.GetCookiesAsync("https://www.roblox.com");
            foreach (var c in leftovers.Where(c => c.Name == ".ROBLOSECURITY"))
                manager.DeleteCookie(c);
            Log("Cookies cleared");
            _webView.CoreWebView2.Navigate("https://www.roblox.com/login");
            Log("Navigated to login page");
        }
        catch (Exception ex)
        {
            Log("UseAnotherAccount error: " + ex.Message);
        }
    }

    private async Task<(long Id, string? Name)> GetAccountInfoAsync(string token)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
                req.Headers.Add("Cookie", $".ROBLOSECURITY={token}");
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var resp = await _http.SendAsync(req, cts.Token);
                if (!resp.IsSuccessStatusCode) return (0, null);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
                var id = doc.RootElement.GetProperty("id").GetInt64();
                var name = doc.RootElement.GetProperty("name").GetString();
                return (id, name);
            }
            catch
            {
                if (attempt == 0) await Task.Delay(800);
            }
        }
        return (0, null);
    }

    private void Log(string message)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "login_debug.log"), $"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        }
        catch { }
    }
}
