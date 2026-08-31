using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace NaxiBootstrap;

public partial class App : Application
{
    [DllImport("kernel32.dll")] static extern bool IsDebuggerPresent();

    public App()
    {
        try
        {
            if (Debugger.IsAttached || Debugger.IsLogging() || IsDebuggerPresent()) Environment.Exit(0);
            var self = Process.GetCurrentProcess().MainModule?.FileName;
            if (self != null)
            {
                var bytes = File.ReadAllBytes(self);
                using var sha = SHA256.Create();
                var hash = Convert.ToBase64String(sha.ComputeHash(bytes));
                if (hash.Contains("AAAA") || hash.Contains("BBBB"))
                    Environment.Exit(0);
            }
            var bad = new[] { "dnspy", "decompiler", "ildasm", "ilspy", "dotpeek" };
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var name = p.ProcessName.ToLower();
                    foreach (var b in bad) if (name.Contains(b)) Environment.Exit(0);
                } catch { }
            }
        } catch { }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "Naxi Bootstrap - startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    private static Mutex? _singleMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleMutex = new Mutex(true, "NaxiBootstrap_SingleInstance_9f3c1a", out bool createdNew);
        if (!createdNew)
        {
            try { using var ev = EventWaitHandle.OpenExisting("NaxiBootstrap_ShowEvent"); ev.Set(); } catch { }
            try
            {
                var cur = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(cur.ProcessName))
                {
                    if (p.Id == cur.Id) continue;
                    try
                    {
                        NativeMethods.ShowWindow(p.MainWindowHandle, 5);
                        NativeMethods.ShowWindow(p.MainWindowHandle, 9);
                        NativeMethods.SetForegroundWindow(p.MainWindowHandle);
                        NativeMethods.PostMessage(p.MainWindowHandle, 0x0400 + 1, IntPtr.Zero, IntPtr.Zero);
                    } catch { }
                    break;
                }
            } catch { }
            Environment.Exit(0);
            return;
        }
        // first instance: listen for show event from second instance
        try
        {
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "NaxiBootstrap_ShowEvent");
            Task.Run(async () =>
            {
                while (true)
                {
                    try { await Task.Run(() => showEvent.WaitOne()); } catch { break; }
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (Current.MainWindow is MainWindow w)
                            {
                                w.Show();
                                w.WindowState = WindowState.Normal;
                                w.Activate();
                                // also clear tray flag via reflection if needed
                                var fld = typeof(MainWindow).GetField("_minimizedToTray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (fld != null) fld.SetValue(w, false);
                                var tray = typeof(MainWindow).GetField("_trayIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(w) as System.Windows.Forms.NotifyIcon;
                                if (tray != null) tray.Visible = false;
                            }
                            else if (Current.MainWindow != null)
                            {
                                Current.MainWindow.Show();
                                Current.MainWindow.WindowState = WindowState.Normal;
                                Current.MainWindow.Activate();
                            }
                        });
                    } catch { }
                }
            });
        } catch { }

        var url = e.Args.FirstOrDefault(a => a.StartsWith("roblox-player:", System.StringComparison.OrdinalIgnoreCase));
        if (url != null)
        {
            HandleProtocolLaunch(url);
            return;
        }

        StartApp();
    }

    private async void StartApp()
    {
        var loading = new LoadingWindow();
        loading.Show();
        loading.SetProgress(8, "Инициализация...");
        await Task.Delay(300);

        loading.SetProgress(25, "Загрузка настроек...");
        var config = RobloxLauncher.LoadConfig();
        if (config.OpenRobloxLinks) RobloxLauncher.RegisterProtocol(true);
        await Task.Delay(300);

        loading.SetProgress(50, "Построение интерфейса...");
        var main = new MainWindow();
        await Task.Delay(400);

        loading.SetProgress(80, "Проверка обновлений...");
        await Task.Delay(500);

        loading.SetProgress(100, "Готово");
        await Task.Delay(300);

        main.Show();
        Application.Current.MainWindow = main;
        loading.FadeOut();
    }

    private async void HandleProtocolLaunch(string url)
    {
        var splash = new SplashWindow();
        splash.SetStatus("Запускаем Roblox...");
        splash.Show();

        var config = RobloxLauncher.LoadConfig();

        var place = await RobloxLauncher.GetPlaceNameAsync(url);
        splash.SetStatus(string.IsNullOrWhiteSpace(place) ? "Запускаем Roblox..." : $"Открываем {place}");

        await Task.Delay(500);
        RobloxLauncher.LaunchFromUrl(url, config);
        await Task.Delay(1800);
        splash.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        Environment.Exit(e.ApplicationExitCode);
    }

    static class NativeMethods
    {
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
