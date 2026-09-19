using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace NaxiBootstrap;

// One game card in the Game Hub.
internal sealed class HubGame
{
    public long PlaceId { get; set; }
    public long UniverseId { get; set; }
    public string Name { get; set; } = "";
    public string Creator { get; set; } = "";
    public long Playing { get; set; }
    public long Visits { get; set; }
    public string IconUrl { get; set; } = "";
    public string Genre { get; set; } = "";
    public bool Favorite { get; set; }
}

// One public server instance of a place (from the games.roblox.com server list).
internal sealed class RobloxServerEntry
{
    public string Id { get; set; } = "";
    public long Playing { get; set; }
    public long MaxPlayers { get; set; }
}

// Roblox platform status. status.roblox.com is a Status.io page with no public
// JSON API — the overall state is embedded directly in the page HTML
// (<strong id="statusbar_text">All Systems Operational</strong>), so we parse that.
internal sealed class RobloxStatusInfo
{
    public string Indicator { get; set; } = "unknown"; // none | minor | major | maintenance | unknown
    public string Description { get; set; } = "";
}

// Outcome of a Roblox server ping: best round-trip in ms, how many nodes were
// probed and whether the Pro deep scan ran.
internal sealed class PingResult
{
    public long Ms;
    public int Nodes;
    public bool Deep;
}

// Data layer for the Game Hub: curated + search game discovery, live player
// counts, game icons with a local disk cache, platform status and a real ICMP
// ping to Roblox game servers.
internal static class GameHubService
{
    private const string RepoB64 = "ZmVhcm1haXJvLWRlc2lnbi9OYXhpQm9vdHN0cmFw";

    private static string Repo => Encoding.UTF8.GetString(Convert.FromBase64String(RepoB64));

    private static string GamesUrl =>
        $"https://raw.githubusercontent.com/{Repo}/main/games.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static readonly string IconCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NaxiBootstrap", "GameIcons");

    private static readonly Dictionary<long, long> UniverseCache = new();
    private static readonly SemaphoreSlim ApiGate = new(4);

    // Roblox owns 128.116.0.0/15 for game servers. Different ISPs answer
    // different nodes, so we probe several in parallel and keep the best
    // round-trip. These six are known-responsive anchors used by the free scan.
    private static readonly string[] ServerIps =
    {
        "128.116.97.3", "128.116.96.3", "128.116.96.1",
        "128.116.97.1", "128.116.98.3", "128.116.99.3"
    };

    // Uniform sample across the whole 128.116.0.0/15 block (128.116.0.0 —
    // 128.117.255.255) used by the Pro deep scan.
    private static List<string> SampleServerIps(int count)
    {
        const int total = 2 * 256 * 256;
        var list = new List<string>();
        for (int i = 0; i < count; i++)
        {
            int off = (i + 1) * total / (count + 1);
            int b4 = off & 0xFF; if (b4 == 0) b4 = 1;
            list.Add($"128.{116 + ((off >> 16) & 0xFF)}.{(off >> 8) & 0xFF}.{b4}");
        }
        return list;
    }

    // Well-known popular games used when games.json cannot be fetched.
    // All verified to resolve (placeId -> universe) as of v2.0.0.
    private static readonly long[] DefaultPlaceIds =
    {
        4924922222, 2753915549, 920587237, 109983668079237, 126884695634066,
        142823291, 15101393044, 6516141723, 13775256536, 286090429,
        8737899170, 13772394625, 606849621, 1962086868, 189707,
        16732694052, 17625359962, 185655149, 116495829188952, 16146832113
    };

    static GameHubService()
    {
        try { Http.DefaultRequestHeaders.Add("User-Agent", "NaxiBootstrap"); } catch { }
    }

    // ---------- curated list ----------

    public static async Task<List<long>> GetCuratedPlaceIdsAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(GamesUrl);
            using var doc = JsonDocument.Parse(json);
            var list = new List<long>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var n)) list.Add(n);
                    else if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("placeId", out var p) && p.TryGetInt64(out var pid)) list.Add(pid);
                }
            }
            if (list.Count > 0) return list;
        }
        catch { }
        return DefaultPlaceIds.ToList();
    }

    // ---------- curated pipeline: placeIds -> full cards ----------

    public static async Task<List<HubGame>> GetGamesAsync(IEnumerable<long> placeIds)
    {
        var result = new List<HubGame>();
        var universes = await ResolveUniversesAsync(placeIds);
        if (universes.Count == 0) return result;

        foreach (var batch in Chunk(universes, 40))
        {
            try
            {
                var ids = string.Join(",", batch.Select(u => u.UniverseId));
                var json = await Http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={ids}");
                using var doc = JsonDocument.Parse(json);
                foreach (var d in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    try
                    {
                        long uid = d.GetProperty("id").GetInt64();
                        var place = batch.First(b => b.UniverseId == uid);
                        long visits = 0;
                        if (d.TryGetProperty("visits", out var vs) && vs.TryGetInt64(out var v1)) visits = v1;
                        else if (d.TryGetProperty("placeVisits", out var pv) && pv.TryGetInt64(out var v2)) visits = v2;
                        result.Add(new HubGame
                        {
                            PlaceId = place.PlaceId,
                            UniverseId = uid,
                            Name = d.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                            Creator = d.TryGetProperty("creator", out var cr) && cr.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "",
                            Playing = d.TryGetProperty("playing", out var pl) && pl.TryGetInt64(out var p) ? p : 0,
                            Visits = visits,
                            Genre = d.TryGetProperty("genre", out var ge) ? ge.GetString() ?? "" : "",
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        var urls = await GetIconUrlsAsync(result.Select(g => g.UniverseId));
        foreach (var g in result)
            if (urls.TryGetValue(g.UniverseId, out var u)) g.IconUrl = u;
        return result;
    }

    // ---------- search (Roblox omni-search, same API the website uses) ----------

    public static async Task<List<HubGame>> SearchAsync(string query)
    {
        try
        {
            var url = $"https://apis.roblox.com/search-api/omni-search?searchQuery={Uri.EscapeDataString(query)}&sessionId={Guid.NewGuid()}&pageToken=&pageType=all";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("searchResults", out var groups)) return new List<HubGame>();

            var seen = new HashSet<long>();
            var games = new List<HubGame>();
            foreach (var grp in groups.EnumerateArray())
            {
                if (!grp.TryGetProperty("contents", out var contents)) continue;
                foreach (var c in contents.EnumerateArray())
                {
                    try
                    {
                        if (!c.TryGetProperty("universeId", out var uid)) continue;
                        long u = uid.ValueKind == JsonValueKind.Number ? uid.GetInt64() : long.Parse(uid.GetString() ?? "0");
                        if (u == 0 || !seen.Add(u)) continue;
                        long place = c.TryGetProperty("rootPlaceId", out var rp) && rp.TryGetInt64(out var pid) ? pid : 0;
                        games.Add(new HubGame
                        {
                            UniverseId = u,
                            PlaceId = place,
                            Name = c.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Creator = c.TryGetProperty("creatorName", out var cr) ? cr.GetString() ?? "" : "",
                            Playing = c.TryGetProperty("playerCount", out var pc) && pc.TryGetInt64(out var p) ? p : 0,
                        });
                        if (games.Count >= 24) return await WithIconsAsync(games);
                    }
                    catch { }
                }
            }
            return await WithIconsAsync(games);
        }
        catch
        {
            return new List<HubGame>();
        }
    }

    private static async Task<List<HubGame>> WithIconsAsync(List<HubGame> games)
    {
        try
        {
            var urls = await GetIconUrlsAsync(games.Select(g => g.UniverseId));
            foreach (var g in games)
                if (urls.TryGetValue(g.UniverseId, out var u)) g.IconUrl = u;
        }
        catch { }
        return games;
    }

    // ---------- internals ----------

    private sealed record PlaceUniverse(long PlaceId, long UniverseId);

    private static async Task<List<PlaceUniverse>> ResolveUniversesAsync(IEnumerable<long> placeIds)
    {
        var list = new List<PlaceUniverse>();
        var toResolve = new List<long>();
        foreach (var pid in placeIds.Distinct())
        {
            lock (UniverseCache)
            {
                if (UniverseCache.TryGetValue(pid, out var u)) { list.Add(new PlaceUniverse(pid, u)); continue; }
            }
            toResolve.Add(pid);
        }

        var tasks = toResolve.Select(async pid =>
        {
            try
            {
                await ApiGate.WaitAsync();
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var json = await Http.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{pid}/universe", cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    var uid = doc.RootElement.GetProperty("universeId").GetInt64();
                    lock (UniverseCache) UniverseCache[pid] = uid;
                    return new PlaceUniverse(pid, uid);
                }
                finally { ApiGate.Release(); }
            }
            catch { return null; }
        });
        var resolved = await Task.WhenAll(tasks);
        list.AddRange(resolved.Where(r => r != null).Select(r => r!));
        return list;
    }

    private static async Task<Dictionary<long, string>> GetIconUrlsAsync(IEnumerable<long> universeIds)
    {
        var map = new Dictionary<long, string>();
        foreach (var batch in Chunk(universeIds.Distinct().ToList(), 50))
        {
            try
            {
                var idStr = string.Join(",", batch);
                var json = await Http.GetStringAsync($"https://thumbnails.roblox.com/v1/games/icons?universeIds={idStr}&size=512x512&format=Png&isCircular=false");
                using var doc = JsonDocument.Parse(json);
                foreach (var d in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    try
                    {
                        var state = d.TryGetProperty("state", out var st) ? st.GetString() : "";
                        var imageUrl = d.TryGetProperty("imageUrl", out var iu) ? iu.GetString() ?? "" : "";
                        if (state == "Completed" && imageUrl.Length > 0 && d.TryGetProperty("targetId", out var t) && t.TryGetInt64(out var tid))
                            map[tid] = imageUrl;
                    }
                    catch { }
                }
            }
            catch { }
        }
        return map;
    }

    // Game icon bytes with a local disk cache so the hub is instant on reopen.
    public static async Task<byte[]?> GetIconBytesAsync(long universeId, string url)
    {
        try
        {
            Directory.CreateDirectory(IconCacheDir);
            var file = Path.Combine(IconCacheDir, universeId + ".png");
            if (File.Exists(file))
            {
                try
                {
                    var cached = await File.ReadAllBytesAsync(file);
                    if (cached.Length > 0) return cached;
                }
                catch { }
            }
            if (url.Length == 0) return null;
            var bytes = await Http.GetByteArrayAsync(url);
            try { await File.WriteAllBytesAsync(file, bytes); } catch { }
            return bytes;
        }
        catch { return null; }
    }

    // ---------- Pro best-server join ----------

    // Roblox does not expose per-server ping publicly, so the practical "best
    // server" is the least loaded one (fewer players = less sim load, lower
    // latency spikes). We fetch up to two pages (200 servers) of the public
    // server list excluding full ones, then pick at random among the eight
    // lightest instances so joins spread out instead of flooding one server.
    public static async Task<List<RobloxServerEntry>> GetServersAsync(long placeId, int maxPages = 2)
    {
        var servers = new List<RobloxServerEntry>();
        try
        {
            string cursor = "";
            for (int page = 0; page < maxPages; page++)
            {
                var url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?limit=100&excludeFullGames=true";
                if (cursor.Length > 0) url += "&cursor=" + Uri.EscapeDataString(cursor);
                var json = await Http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var arr)) break;
                foreach (var s in arr.EnumerateArray())
                {
                    try
                    {
                        var id = s.GetProperty("id").GetString() ?? "";
                        long playing = s.TryGetProperty("playing", out var pl) && pl.TryGetInt64(out var p1) ? p1 : 0;
                        long max = s.TryGetProperty("maxPlayers", out var mp) && mp.TryGetInt64(out var p2) ? p2 : 0;
                        if (id.Length > 0 && (max == 0 || playing < max))
                            servers.Add(new RobloxServerEntry { Id = id, Playing = playing, MaxPlayers = max });
                    }
                    catch { }
                }
                cursor = doc.RootElement.TryGetProperty("nextPageCursor", out var nc) ? nc.GetString() ?? "" : "";
                if (cursor.Length == 0 || servers.Count == 0) break;
            }
        }
        catch { }
        return servers;
    }

    public static async Task<RobloxServerEntry?> GetBestServerAsync(long placeId, int maxPages = 2)
    {
        var servers = await GetServersAsync(placeId, maxPages);
        if (servers.Count == 0) return null;
        var lightest = servers.OrderBy(s => s.Playing).Take(8).ToList();
        return lightest[Random.Shared.Next(lightest.Count)];
    }

    // ---------- status ----------

    public static async Task<RobloxStatusInfo?> GetStatusAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var html = await Http.GetStringAsync("https://status.roblox.com/", cts.Token);
            var m = System.Text.RegularExpressions.Regex.Match(html, "id=\"statusbar_text\">([^<]+)<");
            if (!m.Success) return null;
            var text = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            var indicator = text.IndexOf("operational", StringComparison.OrdinalIgnoreCase) >= 0 ? "none"
                : text.IndexOf("maintenance", StringComparison.OrdinalIgnoreCase) >= 0 ? "maintenance"
                : text.IndexOf("minor", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("degrad", StringComparison.OrdinalIgnoreCase) >= 0 ? "minor"
                : text.IndexOf("disruption", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("outage", StringComparison.OrdinalIgnoreCase) >= 0 ? "major"
                : "unknown";
            return new RobloxStatusInfo { Indicator = indicator, Description = text };
        }
        catch { return null; }
    }

    // ---------- ping ----------

    // Real ICMP echo to Roblox game-server nodes; best of N. Falls back to a TCP
    // connect timing when ICMP is deprioritized by the ISP, else null.
    // deep=true (Pro): the deepest scan we can do — a uniform sweep of 96 nodes
    // across the whole 128.116.0.0/15 block (plus the anchors), then four more
    // rounds against the eight fastest live nodes, then a final two-round duel
    // with the single champion node — the absolute minimum latency the network
    // can offer right now.
    public static async Task<PingResult?> PingRobloxAsync(bool deep = false)
    {
        var ips = new List<string>(ServerIps);
        if (deep) ips.AddRange(SampleServerIps(96).Except(ips));
        var probed = await ProbeIps(ips, rounds: 1, timeoutMs: deep ? 900 : 1500);
        var best = probed.Values.Min();
        if (deep && best < long.MaxValue)
        {
            // Refine: four extra rounds against the eight fastest live nodes.
            var fastest = probed.Where(kv => kv.Value < long.MaxValue)
                                .OrderBy(kv => kv.Value).Take(8)
                                .Select(kv => kv.Key).ToList();
            if (fastest.Count > 0)
            {
                var refined = await ProbeIps(fastest, rounds: 4, timeoutMs: 900);
                var refinedBest = refined.Values.Min();
                if (refinedBest < best) best = refinedBest;
                if (refinedBest < long.MaxValue)
                {
                    // Final duel: two rounds against the champion node only.
                    var champion = refined.Where(kv => kv.Value < long.MaxValue)
                                          .OrderBy(kv => kv.Value).First().Key;
                    var last = await ProbeIps(new List<string> { champion }, rounds: 2, timeoutMs: 700);
                    if (last.Values.Min() < best) best = last.Values.Min();
                }
            }
        }
        if (best < long.MaxValue) return new PingResult { Ms = best, Nodes = ips.Count, Deep = deep };

        // TCP connect timing as a latency proxy (three-way handshake ≈ 1 RTT).
        foreach (var ip in ServerIps.Take(2))
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var tcp = new System.Net.Sockets.TcpClient();
                await tcp.ConnectAsync(ip, 443).WaitAsync(TimeSpan.FromSeconds(1.5));
                sw.Stop();
                return new PingResult { Ms = sw.ElapsedMilliseconds, Nodes = ips.Count, Deep = deep };
            }
            catch { }
        }
        return null;
    }

    // Pings every IP `rounds` times in parallel and returns the best RTT per IP.
    private static async Task<Dictionary<string, long>> ProbeIps(List<string> ips, int rounds, int timeoutMs)
    {
        var tasks = ips.Select(async ip =>
        {
            long best = long.MaxValue;
            for (int r = 0; r < rounds; r++)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, timeoutMs);
                    if (reply.Status == IPStatus.Success && (long)reply.RoundtripTime < best)
                        best = (long)reply.RoundtripTime;
                }
                catch { }
            }
            return (ip, best);
        });
        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.ip, r => r.best);
    }

    private static List<List<T>> Chunk<T>(List<T> source, int size)
    {
        var chunks = new List<List<T>>();
        for (int i = 0; i < source.Count; i += size)
            chunks.Add(source.GetRange(i, Math.Min(size, source.Count - i)));
        return chunks;
    }
}
