using System.Net.Http;
using System.Text.Json;

namespace ZCinemaSound.Core;

public sealed record UpdateInfo(string Version, string Url, string AssetUrl);

/// <summary>Checks GitHub Releases for a newer version and downloads the installer.</summary>
public static class Updater
{
    public const string LatestApi = "https://api.github.com/repos/Hydraxon91/zcinema-sound/releases/latest";
    public const string ReleasesPage = "https://github.com/Hydraxon91/zcinema-sound/releases";
    public const string InstallerAsset = "ZCinemaSound-Setup.exe";

    /// <summary>Parse the "latest release" JSON; null if it is not newer (or is a prerelease).</summary>
    public static UpdateInfo? ParseLatest(string json, string currentVersion)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;
            if (!root.TryGetProperty("tag_name", out var tagEl)) return null;
            string ver = Clean(tagEl.GetString() ?? "");
            if (ver.Length == 0 || !IsNewer(ver, currentVersion)) return null;

            string url = root.TryGetProperty("html_url", out var h) ? (h.GetString() ?? ReleasesPage) : ReleasesPage;
            string asset = FindInstallerAsset(root);
            return new UpdateInfo(ver, url, asset);
        }
        catch { return null; }
    }

    private static string FindInstallerAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return "";
        foreach (var a in assets.EnumerateArray())
        {
            if (a.TryGetProperty("name", out var n)
                && string.Equals(n.GetString(), InstallerAsset, StringComparison.OrdinalIgnoreCase)
                && a.TryGetProperty("browser_download_url", out var u))
                return u.GetString() ?? "";
        }
        return "";
    }

    public static bool IsNewer(string candidate, string current)
        => Version.TryParse(Clean(candidate), out var a)
        && Version.TryParse(Clean(current), out var b)
        && a > b;

    public static async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        try
        {
            using var http = NewClient();
            string json = await http.GetStringAsync(LatestApi, ct).ConfigureAwait(false);
            return ParseLatest(json, currentVersion);
        }
        catch { return null; }
    }

    /// <summary>Download a file, reporting percent progress (0..100).</summary>
    public static async Task DownloadAsync(string url, string dest, IProgress<int>? progress, CancellationToken ct = default)
    {
        using var http = NewClient();
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        long? total = resp.Content.Headers.ContentLength;

        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = File.Create(dest);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total is > 0) progress?.Report((int)(read * 100 / total.Value));
        }
    }

    private static HttpClient NewClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ZCinemaSound");
        return http;
    }

    private static string Clean(string v)
        => v.Trim().TrimStart('v', 'V').Split('-', '+')[0].Trim();
}
