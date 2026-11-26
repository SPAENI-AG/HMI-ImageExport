using System.Net.Http.Headers;
using System.Text.Json;

namespace HMIImageExport.Core;

public sealed record UpdateInfo(string Version, string ReleaseUrl, string DownloadUrl, string Notes);

/// <summary>Update-Hinweis über GitHub Releases (kein automatisches Installieren, Download im Browser).</summary>
public static class UpdateChecker
{
    /// <summary>Platzhalter OWNER/… in Directory.Build.props = Update-Prüfung abgeschaltet.</summary>
    public static bool IsEnabled(string? repo) =>
        !string.IsNullOrWhiteSpace(repo) && repo.Contains('/') && !repo.StartsWith("OWNER/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Höchstens einmal pro Intervall prüfen, nicht bei jedem Start.</summary>
    public static bool ShouldCheck(DateTime? lastUtc, DateTime nowUtc, TimeSpan interval) =>
        lastUtc == null || lastUtc > nowUtc || nowUtc - lastUtc.Value >= interval;

    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().TrimStart('v', 'V');
        var cut = t.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0) t = t[..cut];
        if (!t.Contains('.')) t += ".0";
        return Version.TryParse(t, out version!);
    }

    public static bool IsNewer(string current, string latest) =>
        TryParseVersion(current, out var c) && TryParseVersion(latest, out var l) && l > c;

    /// <summary>Nur https-Adressen auf github.com werden geöffnet.</summary>
    public static bool IsSafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        (u.Host == "github.com" || u.Host.EndsWith(".github.com", StringComparison.Ordinal));

    public static UpdateInfo? ParseRelease(string json, string repo)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("tag_name", out var tagEl) || tagEl.GetString() is not { Length: > 0 } tag) return null;
        var fallback = $"https://github.com/{repo}/releases";
        var html = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
        var release = IsSafeUrl(html) ? html! : fallback;
        var download = release;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                var url = a.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && IsSafeUrl(url)) { download = url!; break; }
            }
        }
        var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        return new UpdateInfo(tag.TrimStart('v', 'V'), release, download, notes);
    }

    /// <summary>Liefert die neuere Version oder null, wenn die laufende aktuell ist. Wirft bei Netzwerkfehlern.</summary>
    public static async Task<UpdateInfo?> CheckAsync(HttpClient http, string repo, string currentVersion, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        req.Headers.UserAgent.ParseAdd("HMI-ImageExport");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;   // noch kein Release
        resp.EnsureSuccessStatusCode();
        var info = ParseRelease(await resp.Content.ReadAsStringAsync(ct), repo);
        return info != null && IsNewer(currentVersion, info.Version) ? info : null;
    }
}
