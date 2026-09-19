using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace NaxiBootstrap;

internal sealed class ProResult
{
    public bool Ok { get; set; }
    public bool Pro { get; set; }
    public bool Max { get; set; }
    public bool Already { get; set; }
    public string? Error { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }
}

internal static class ProLicense
{
    private const string Url = "https://kjvxulkfsyvmedanbsxg.supabase.co/functions/v1/naxi-pro";
    private const string Key = "sb_publishable_5G1es8032uoTtGZcj9Ulnw_3DeUaDoz";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static string DeviceId()
    {
        var guid = "unknown";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            guid = key?.GetValue("MachineGuid")?.ToString() ?? Environment.MachineName;
        }
        catch
        {
            guid = Environment.MachineName;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("naxi-pro:" + guid)));
    }

    public static Task<ProResult> CheckAsync()
        => PostAsync(new { action = "status", device_id = DeviceId() });

    public static Task<ProResult> RedeemAsync(string code)
        => PostAsync(new { action = "redeem", code, device_id = DeviceId() });

    private static async Task<ProResult> PostAsync(object body)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
            req.Headers.TryAddWithoutValidation("apikey", Key);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var res = await Http.SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ProResult>(text, JsonOpts)
                   ?? new ProResult { Ok = false, Error = "invalid" };
        }
        catch
        {
            return new ProResult { Ok = false, Error = "network" };
        }
    }
}
