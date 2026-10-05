using System.Net.Http;
using System.Text.Json;

namespace ZCinemaSound.Core;

public sealed record UpdateInfo(string Version, string Url);

/// <summary>Checks GitHub Releases for a newer version.</summary>
public static class Updater
{
    public const string LatestApi = "https://api.github.com/repos/Hydraxon91/zcinema-sound/releases/latest";
    public const string ReleasesPage = "https://github.com/Hydraxon91/zcinema-sound/releases";

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
            return new UpdateInfo(ver, url);
        }
        catch { return null; }
    }

    public static bool IsNewer(string candidate, string current)
        => Version.TryParse(Clean(candidate), out var a)
        && Version.TryParse(Clean(current), out var b)
        && a > b;

    public static async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ZCinemaSound");
            string json = await http.GetStringAsync(LatestApi, ct).ConfigureAwait(false);
            return ParseLatest(json, currentVersion);
        }
        catch { return null; }
    }

    private static string Clean(string v)
        => v.Trim().TrimStart('v', 'V').Split('-', '+')[0].Trim();
}
