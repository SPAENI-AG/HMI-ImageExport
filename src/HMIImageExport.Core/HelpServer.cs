using System.Net;
using System.Net.Sockets;

namespace HMIImageExport.Core;

/// <summary>Anker-IDs der Hilfe (müssen in Help/de.html und Help/en.html vorkommen).</summary>
public static class HelpAnchors
{
    public const string Quickstart = "quickstart";
    public const string Connect = "connect";
    public const string Folder = "folder";
    public const string Capture = "capture";
    public const string Pages = "pages";
    public const string Word = "word";
    public const string Settings = "settings";
    public const string Troubleshooting = "troubleshooting";
    public const string Install = "install";
    public const string Security = "security";
    public const string Updates = "updates";

    public static readonly string[] All =
        { Quickstart, Connect, Folder, Capture, Pages, Word, Settings, Troubleshooting, Install, Security, Updates };
}

/// <summary>Lokaler Webserver für die Hilfe: nur 127.0.0.1, nur Dateien unterhalb des Hilfe-Ordners.</summary>
public sealed class HelpServer : IDisposable
{
    private readonly string _root;
    private HttpListener? _listener;

    public string BaseUrl { get; private set; } = "";

    public HelpServer(string root) => _root = Path.GetFullPath(root);

    /// <summary>Löst einen URL-Pfad sicher auf; null bei ../, fremden Ordnern oder fehlender Datei.</summary>
    public static string? ResolvePath(string root, string urlPath)
    {
        var rel = Uri.UnescapeDataString(urlPath ?? "").TrimStart('/', '\\');
        if (rel.Length == 0 || rel.Contains('\0')) return null;
        var rootFull = Path.GetFullPath(root);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar)) rootFull += Path.DirectorySeparatorChar;
        string full;
        try { full = Path.GetFullPath(Path.Combine(rootFull, rel)); }
        catch (Exception) { return null; }
        if (!full.StartsWith(rootFull, StringComparison.Ordinal)) return null;
        return File.Exists(full) ? full : null;
    }

    public void Start()
    {
        if (_listener != null) return;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var l = new HttpListener();
            l.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                l.Start();
                _listener = l;
                BaseUrl = $"http://127.0.0.1:{port}/";
                _ = Task.Run(LoopAsync);
                return;
            }
            catch (HttpListenerException) { l.Close(); }
        }
        throw new IOException("Kein freier Port für die Hilfe gefunden.");
    }

    private async Task LoopAsync()
    {
        var l = _listener;
        while (l is { IsListening: true })
        {
            HttpListenerContext ctx;
            try { ctx = await l.GetContextAsync(); }
            catch (Exception) { return; }
            try { Handle(ctx); }
            catch (Exception) { try { ctx.Response.Abort(); } catch (Exception) { } }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var res = ctx.Response;
        if (ctx.Request.HttpMethod != "GET" && ctx.Request.HttpMethod != "HEAD") { res.StatusCode = 405; res.Close(); return; }
        var path = ResolvePath(_root, ctx.Request.Url?.AbsolutePath ?? "");
        if (path == null) { res.StatusCode = 404; res.Close(); return; }
        var bytes = File.ReadAllBytes(path);
        res.ContentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".js" => "text/javascript; charset=utf-8",
            _ => "application/octet-stream",
        };
        res.Headers["Cache-Control"] = "no-cache";
        res.ContentLength64 = bytes.Length;
        if (ctx.Request.HttpMethod == "GET") res.OutputStream.Write(bytes, 0, bytes.Length);
        res.Close();
    }

    public void Dispose()
    {
        try { _listener?.Close(); } catch (Exception) { }
        _listener = null;
    }
}
