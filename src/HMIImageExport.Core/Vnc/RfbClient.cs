using System.Net.Sockets;
using System.Text;

namespace HMIImageExport.Core.Vnc;

/// <summary>
/// Schlanker RFB-Client (VNC) für Siemens-Panels (Sm@rtServer): RFB 3.3/3.7/3.8, Sicherheitsart None und VNC-Passwort,
/// Encodings Raw, CopyRect, Hextile, DesktopSize. Pixelformat fest 32 bpp (BGRA im Speicher).
/// </summary>
public sealed class RfbClient : IVncSession
{
    private const int MaxDimension = 16384;

    private Stream? _stream;
    private TcpClient? _tcp;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly object _writeLock = new();
    private readonly object _frontLock = new();
    private byte[] _work = Array.Empty<byte>();
    private byte[] _front = Array.Empty<byte>();
    private int _w, _h;
    private volatile bool _connected;

    /// <summary>Zeit zwischen Maustaste drücken und loslassen beim Tippen.</summary>
    public int PressDelayMs { get; set; } = 80;
    public string ServerName { get; private set; } = "";

    public bool IsConnected => _connected;
    public event EventHandler? FrameUpdated;
    public event EventHandler<string?>? Disconnected;

    public async Task ConnectAsync(string host, int port, string password, CancellationToken ct)
    {
        var tcp = new TcpClient { NoDelay = true };
        try { await tcp.ConnectAsync(host, port, ct); }
        catch (OperationCanceledException) { tcp.Dispose(); throw; }
        catch (Exception ex)
        {
            tcp.Dispose();
            throw new VncException($"Verbindung zu {host}:{port} nicht möglich: {ex.Message}", ex);
        }
        _tcp = tcp;
        try { await ConnectAsync(tcp.GetStream(), password, ct); }
        catch { tcp.Dispose(); _tcp = null; throw; }
    }

    /// <summary>Handshake über einen bestehenden Stream (auch für Tests).</summary>
    public async Task ConnectAsync(Stream stream, string password, CancellationToken ct)
    {
        _stream = stream;
        await HandshakeAsync(password, ct);
        _connected = true;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    // ---------- Handshake ----------

    private async Task HandshakeAsync(string password, CancellationToken ct)
    {
        var ver = Encoding.ASCII.GetString(await ReadAsync(12, ct));
        if (!ver.StartsWith("RFB ", StringComparison.Ordinal) || !int.TryParse(ver.AsSpan(8, 3), out var minor))
            throw new VncException("Der Server spricht kein VNC (RFB).");
        var clientMinor = minor >= 8 ? 8 : minor == 7 ? 7 : 3;
        Send(Encoding.ASCII.GetBytes($"RFB 003.00{clientMinor}\n"));

        byte chosen;
        if (clientMinor >= 7)
        {
            var n = (await ReadAsync(1, ct))[0];
            if (n == 0) throw new VncException("Verbindung abgelehnt: " + await ReadReasonAsync(ct));
            var types = await ReadAsync(n, ct);
            var hasNone = types.Contains((byte)1);
            var hasVnc = types.Contains((byte)2);
            if (hasVnc && (!string.IsNullOrEmpty(password) || !hasNone)) chosen = 2;
            else if (hasNone) chosen = 1;
            else throw new VncException("Der Server bietet keine unterstützte Anmeldeart (nur None und VNC-Passwort).");
            Send(new[] { chosen });
        }
        else
        {
            var t = ReadU32(await ReadAsync(4, ct));
            if (t == 0) throw new VncException("Verbindung abgelehnt: " + await ReadReasonAsync(ct));
            if (t != 1 && t != 2) throw new VncException("Der Server bietet keine unterstützte Anmeldeart.");
            chosen = (byte)t;
        }

        if (chosen == 2)
        {
            var challenge = await ReadAsync(16, ct);
            Send(VncAuth.Response(challenge, password));
        }
        if (chosen == 2 || clientMinor >= 8)
        {
            if (ReadU32(await ReadAsync(4, ct)) != 0)
            {
                var reason = clientMinor >= 8 ? await ReadReasonAsync(ct) : "";
                throw new VncException("Anmeldung fehlgeschlagen (Passwort falsch oder Zugriff verweigert)."
                    + (string.IsNullOrWhiteSpace(reason) ? "" : " " + reason));
            }
        }

        Send(new byte[] { 1 }); // ClientInit: Zugriff teilen
        var init = await ReadAsync(24, ct);
        _w = (init[0] << 8) | init[1];
        _h = (init[2] << 8) | init[3];
        var nameLen = ReadU32(init.AsSpan(20, 4).ToArray());
        if (nameLen > 65536) throw new VncException("Ungültige Antwort des Servers (Name zu lang).");
        ServerName = Encoding.Latin1.GetString(await ReadAsync((int)nameLen, ct));
        Allocate(_w, _h);

        // Pixelformat: 32 bpp, Tiefe 24, little-endian, true colour, R<<16 | G<<8 | B  → Bytes B,G,R,X
        Send(new byte[]
        {
            0, 0, 0, 0,
            32, 24, 0, 1, 0, 255, 0, 255, 0, 255, 16, 8, 0, 0, 0, 0,
        });
        // Encodings: Hextile, CopyRect, Raw, DesktopSize
        var enc = new List<byte> { 2, 0, 0, 4 };
        foreach (var e in new[] { 5, 1, 0, -223 }) enc.AddRange(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(e)));
        Send(enc.ToArray());
        SendUpdateRequest(false);
    }

    private async Task<string> ReadReasonAsync(CancellationToken ct)
    {
        var len = ReadU32(await ReadAsync(4, ct));
        if (len > 65536) return "";
        return Encoding.UTF8.GetString(await ReadAsync((int)len, ct));
    }

    // ---------- Empfangsschleife ----------

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        string? error = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var type = (await ReadAsync(1, ct))[0];
                switch (type)
                {
                    case 0: await ReadUpdateAsync(ct); break;
                    case 1:
                    {
                        var h = await ReadAsync(5, ct);
                        await ReadAsync(((h[3] << 8) | h[4]) * 6, ct);
                        break;
                    }
                    case 2: break; // Bell
                    case 3:
                    {
                        var h = await ReadAsync(7, ct);
                        var len = ReadU32(h.AsSpan(3, 4).ToArray());
                        if (len > 10_000_000) throw new VncException("Ungültige Zwischenablage-Nachricht.");
                        await ReadAsync((int)len, ct);
                        break;
                    }
                    default: throw new VncException($"Unbekannte Servernachricht ({type}).");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { error = ex.Message; }
        finally { _connected = false; }
        Disconnected?.Invoke(this, error);
    }

    private async Task ReadUpdateAsync(CancellationToken ct)
    {
        var head = await ReadAsync(3, ct);
        var rects = (head[1] << 8) | head[2];
        var resized = false;
        for (var i = 0; i < rects; i++)
        {
            var r = await ReadAsync(12, ct);
            int x = (r[0] << 8) | r[1], y = (r[2] << 8) | r[3], w = (r[4] << 8) | r[5], h = (r[6] << 8) | r[7];
            var enc = unchecked((int)ReadU32(r.AsSpan(8, 4).ToArray()));
            switch (enc)
            {
                case 0: await ReadRawAsync(x, y, w, h, ct); break;
                case 1: await ReadCopyRectAsync(x, y, w, h, ct); break;
                case 5: await ReadHextileAsync(x, y, w, h, ct); break;
                case -223:
                    if (w <= 0 || h <= 0 || w > MaxDimension || h > MaxDimension) throw new VncException("Ungültige Panel-Grösse.");
                    Allocate(w, h); resized = true; break;
                case -239: // Cursor (RichCursor): Pixel + Bitmaske – wird nicht gezeichnet, nur überlesen
                    if (w > 0 && h > 0)
                    {
                        var n = (long)w * h * 4 + (long)((w + 7) / 8) * h;
                        if (n > 16_000_000) throw new VncException("Ungültiger Mauszeiger vom Server.");
                        await ReadAsync((int)n, ct);
                    }
                    break;
                case -240: // XCursor: 6 Byte Farben + zwei Bitmasken
                    if (w > 0 && h > 0) await ReadAsync(6 + 2 * ((w + 7) / 8) * h, ct);
                    break;
                case -232: break;      // PointerPos: keine Nutzdaten
                case -224: i = rects; break;   // LastRect: Ende der Rechtecke
                default: throw new VncException($"Nicht unterstütztes Encoding ({enc}).");
            }
        }
        lock (_frontLock)
        {
            Buffer.BlockCopy(_work, 0, _front, 0, _work.Length);
            for (var i = 3; i < _front.Length; i += 4) _front[i] = 255;   // Füllbyte des Servers → deckend
        }
        SendUpdateRequest(!resized);
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void Allocate(int w, int h)
    {
        if (w <= 0 || h <= 0 || w > MaxDimension || h > MaxDimension) throw new VncException("Ungültige Panel-Grösse.");
        lock (_frontLock)
        {
            _w = w; _h = h;
            _work = new byte[w * h * 4];
            _front = new byte[w * h * 4];
        }
    }

    private void CheckRect(int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || w < 0 || h < 0 || x + w > _w || y + h > _h)
            throw new VncException("Ungültiges Rechteck vom Server.");
    }

    private async Task ReadRawAsync(int x, int y, int w, int h, CancellationToken ct)
    {
        CheckRect(x, y, w, h);
        var data = await ReadAsync(w * h * 4, ct);
        for (var row = 0; row < h; row++)
            Buffer.BlockCopy(data, row * w * 4, _work, ((y + row) * _w + x) * 4, w * 4);
    }

    private async Task ReadCopyRectAsync(int x, int y, int w, int h, CancellationToken ct)
    {
        var s = await ReadAsync(4, ct);
        int sx = (s[0] << 8) | s[1], sy = (s[2] << 8) | s[3];
        CheckRect(x, y, w, h);
        CheckRect(sx, sy, w, h);
        var tmp = new byte[w * h * 4];
        for (var row = 0; row < h; row++)
            Buffer.BlockCopy(_work, ((sy + row) * _w + sx) * 4, tmp, row * w * 4, w * 4);
        for (var row = 0; row < h; row++)
            Buffer.BlockCopy(tmp, row * w * 4, _work, ((y + row) * _w + x) * 4, w * 4);
    }

    private async Task ReadHextileAsync(int x, int y, int w, int h, CancellationToken ct)
    {
        CheckRect(x, y, w, h);
        var bg = new byte[4];
        var fg = new byte[4];
        for (var ty = y; ty < y + h; ty += 16)
        {
            var th = Math.Min(16, y + h - ty);
            for (var tx = x; tx < x + w; tx += 16)
            {
                var tw = Math.Min(16, x + w - tx);
                var sub = (await ReadAsync(1, ct))[0];
                if ((sub & 1) != 0) // Raw-Kachel
                {
                    var data = await ReadAsync(tw * th * 4, ct);
                    for (var row = 0; row < th; row++)
                        Buffer.BlockCopy(data, row * tw * 4, _work, ((ty + row) * _w + tx) * 4, tw * 4);
                    continue;
                }
                if ((sub & 2) != 0) bg = await ReadAsync(4, ct);
                if ((sub & 4) != 0) fg = await ReadAsync(4, ct);
                Fill(tx, ty, tw, th, bg);
                if ((sub & 8) != 0)
                {
                    var n = (await ReadAsync(1, ct))[0];
                    for (var i = 0; i < n; i++)
                    {
                        var color = fg;
                        if ((sub & 16) != 0) color = await ReadAsync(4, ct);
                        var xy = await ReadAsync(2, ct);
                        int sx = xy[0] >> 4, sy = xy[0] & 15, sw = (xy[1] >> 4) + 1, sh = (xy[1] & 15) + 1;
                        if (sx + sw > tw) sw = tw - sx;
                        if (sy + sh > th) sh = th - sy;
                        if (sw > 0 && sh > 0) Fill(tx + sx, ty + sy, sw, sh, color);
                    }
                }
            }
        }
    }

    private void Fill(int x, int y, int w, int h, byte[] px)
    {
        for (var row = 0; row < h; row++)
        {
            var o = ((y + row) * _w + x) * 4;
            for (var col = 0; col < w; col++, o += 4)
            {
                _work[o] = px[0]; _work[o + 1] = px[1]; _work[o + 2] = px[2]; _work[o + 3] = 255;
            }
        }
    }

    // ---------- Senden ----------

    private void SendUpdateRequest(bool incremental)
    {
        Send(new byte[]
        {
            3, (byte)(incremental ? 1 : 0), 0, 0, 0, 0,
            (byte)(_w >> 8), (byte)_w, (byte)(_h >> 8), (byte)_h,
        });
    }

    public void Tap(int x, int y)
    {
        if (!_connected) return;
        x = Math.Clamp(x, 0, Math.Max(0, _w - 1));
        y = Math.Clamp(y, 0, Math.Max(0, _h - 1));
        _ = Task.Run(async () =>
        {
            try
            {
                SendPointer(1, x, y);
                await Task.Delay(PressDelayMs);
                SendPointer(0, x, y);
            }
            catch (Exception) { /* Verbindung weg: Disconnected meldet es */ }
        });
    }

    private void SendPointer(byte mask, int x, int y) =>
        Send(new byte[] { 5, mask, (byte)(x >> 8), (byte)x, (byte)(y >> 8), (byte)y });

    private void Send(byte[] data)
    {
        lock (_writeLock)
        {
            _stream!.Write(data, 0, data.Length);
            _stream.Flush();
        }
    }

    // ---------- Lesen / Hilfen ----------

    private async Task<byte[]> ReadAsync(int count, CancellationToken ct)
    {
        var buf = new byte[count];
        var o = 0;
        while (o < count)
        {
            var r = await _stream!.ReadAsync(buf.AsMemory(o, count - o), ct);
            if (r == 0) throw new EndOfStreamException("Das Panel hat die Verbindung getrennt.");
            o += r;
        }
        return buf;
    }

    private static uint ReadU32(byte[] b) => (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);

    public FrameSnapshot? GetSnapshot()
    {
        lock (_frontLock)
        {
            if (!_connected && _front.Length == 0) return null;
            return new FrameSnapshot(_w, _h, (byte[])_front.Clone());
        }
    }

    public async Task DisconnectAsync()
    {
        _connected = false;
        _cts?.Cancel();
        try { _stream?.Dispose(); } catch (Exception) { }
        try { _tcp?.Dispose(); } catch (Exception) { }
        if (_loop != null) { try { await _loop; } catch (Exception) { } }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
