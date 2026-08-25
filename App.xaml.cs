using System.Threading.Tasks;
using System.Windows;

namespace NaxiBootstrap;

public partial class App : Application
{
    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "Naxi Bootstrap - startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
}
