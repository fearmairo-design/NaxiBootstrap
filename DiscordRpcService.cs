using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NaxiBootstrap;

internal static class DiscordRpcService
{
    private static NamedPipeClientStream? _pipe;
    private static CancellationTokenSource? _cts;
    private static Task? _loopTask;
    private static long _startTimestamp;
    private static readonly object _lock = new();

    public static bool IsRunning => _pipe != null && _pipe.IsConnected;

    // Naxi MAX: when set, Rich Presence shows the live Roblox game instead of
    // the user's custom text. Null = custom fields behave as before.
    public static string? AutoGameName;

    public static void Start(AppConfig cfg)
    {
        Stop();
        if (!cfg.DiscordRpcEnabled) return;
        if (string.IsNullOrWhiteSpace(cfg.DiscordRpcClientId)) return;
        _startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(cfg, _cts.Token));
    }

    public static void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _pipe?.Dispose(); } catch { }
        _pipe = null;
        _cts = null;
    }

    public static void Update(AppConfig cfg)
    {
        if (!cfg.DiscordRpcEnabled) { Stop(); return; }
        if (IsRunning) _ = SendActivityAsync(cfg);
        else Start(cfg);
    }

    private static async Task LoopAsync(AppConfig cfg, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await TryConnectAsync(cfg.DiscordRpcClientId, ct)) { await Task.Delay(5000, ct); continue; }
                await SendActivityAsync(cfg);
                // keep alive + handle disconnect
                while (!ct.IsCancellationRequested && _pipe != null && _pipe.IsConnected)
                {
                    await Task.Delay(15000, ct);
                    // refresh activity periodically
                    await SendActivityAsync(cfg);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(5000, ct); }
        }
    }

    private static async Task<bool> TryConnectAsync(string clientId, CancellationToken ct)
    {
        for (int i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(1000, ct);
                var handshake = JsonSerializer.Serialize(new { v = 1, client_id = clientId });
                await WriteFrameAsync(pipe, 0, handshake, ct);
                // read handshake response (ignore)
                await ReadFrameAsync(pipe, ct);
                lock (_lock) _pipe = pipe;
                return true;
            }
            catch { try { pipe.Dispose(); } catch { } }
        }
        return false;
    }

    private static async Task SendActivityAsync(AppConfig cfg)
    {
        NamedPipeClientStream? pipe;
        lock (_lock) pipe = _pipe;
        if (pipe == null || !pipe.IsConnected) return;
        try
        {
            var activity = new Dictionary<string, object?>();
            // map from your C++ snippet: details = Competitive, state = Playing Solo, but use config values
            if (AutoGameName != null)
            {
                activity["details"] = "🎮 " + AutoGameName;
                activity["state"] = "via Naxi Bootstrap";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(cfg.DiscordRpcDetails)) activity["details"] = cfg.DiscordRpcDetails;
                if (!string.IsNullOrWhiteSpace(cfg.DiscordRpcState)) activity["state"] = cfg.DiscordRpcState;
            }
            if (cfg.DiscordRpcShowElapsed) activity["timestamps"] = new Dictionary<string, object> { ["start"] = _startTimestamp };
            // do not send assets unless they exist in dev portal — empty assets is safer for button display
            if (!string.IsNullOrWhiteSpace(cfg.DiscordRpcLargeImage) && cfg.DiscordRpcLargeImage.Trim().ToLower() != "naxi")
            {
                var assets = new Dictionary<string, object> { ["large_image"] = cfg.DiscordRpcLargeImage.Trim() };
                if (!string.IsNullOrWhiteSpace(cfg.DiscordRpcLargeText)) assets["large_text"] = cfg.DiscordRpcLargeText;
                if (!string.IsNullOrWhiteSpace(cfg.DiscordRpcSmallImage)) assets["small_image"] = cfg.DiscordRpcSmallImage.Trim();
                if (!string.IsNullOrWhiteSpace(cfg.DiscordRpcSmallText)) assets["small_text"] = cfg.DiscordRpcSmallText;
                activity["assets"] = assets;
            }
            activity["instance"] = false;

            var payload = new Dictionary<string, object>
            {
                ["cmd"] = "SET_ACTIVITY",
                ["args"] = new Dictionary<string, object> { ["pid"] = Environment.ProcessId, ["activity"] = activity },
                ["nonce"] = Guid.NewGuid().ToString()
            };
            var json = JsonSerializer.Serialize(payload);
            await WriteFrameAsync(pipe, 1, json, CancellationToken.None);
            // read response for debug
            using var cts = new CancellationTokenSource(1500);
            try
            {
                var resp = await ReadFrameAsync(pipe, cts.Token);
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "NaxiRPC.log"), $"[{DateTime.Now:HH:mm:ss}] REQ: {json}\nRESP: {resp}\n\n"); } catch { }
            } catch (Exception ex) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "NaxiRPC.log"), $"[{DateTime.Now:HH:mm:ss}] REQ: {json}\nERR: {ex.Message}\n\n"); } catch { } }
        }
        catch { }
    }

    private static async Task WriteFrameAsync(Stream s, int opcode, string json, CancellationToken ct)
    {
        var data = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(data.Length).CopyTo(header, 4);
        await s.WriteAsync(header, ct);
        await s.WriteAsync(data, ct);
        await s.FlushAsync(ct);
    }

    private static async Task<string> ReadFrameAsync(Stream s, CancellationToken ct)
    {
        var header = new byte[8];
        int read = 0;
        while (read < 8) read += await s.ReadAsync(header, read, 8 - read, ct);
        int len = BitConverter.ToInt32(header, 4);
        var data = new byte[len];
        read = 0;
        while (read < len) read += await s.ReadAsync(data, read, len - read, ct);
        return Encoding.UTF8.GetString(data);
    }
}
