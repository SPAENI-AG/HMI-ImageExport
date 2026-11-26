using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using HMIImageExport.Core.Vnc;
using HMIImageExport.Core;

var failed = 0;
void Test(string name, Action body)
{
    try { body(); Console.WriteLine($"OK    {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FEHLER {name}: {ex.Message}"); }
}
void Check(bool ok, string msg) { if (!ok) throw new Exception(msg); }

Test("Strings: DE und EN vorhanden", () =>
{
    foreach (var (k, v) in Strings.All)
        Check(!string.IsNullOrWhiteSpace(v.De) && !string.IsNullOrWhiteSpace(v.En), $"Text fehlt für {k}");
});

Test("Strings: gleiche Platzhalter in beiden Sprachen", () =>
{
    static string[] Ph(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToArray();
    foreach (var (k, v) in Strings.All)
        Check(Ph(v.De).SequenceEqual(Ph(v.En)), $"Platzhalter unterschiedlich: {k}");
});

Test("Loc: Umschalten wirkt sofort und meldet LanguageChanged", () =>
{
    var n = 0;
    Loc.Instance.LanguageChanged += (_, _) => n++;
    Loc.Instance.Language = "de";
    Check(Loc.T("Tab_Settings") == "Einstellungen", "DE falsch");
    Loc.Instance.Language = "en";
    Check(Loc.T("Tab_Settings") == "Settings", "EN falsch");
    Check(n >= 1, "Ereignis fehlt");
    Loc.Instance.Language = "de";
});

Test("Loc: Platzhalter und unbekannter Schlüssel", () =>
{
    Loc.Instance.Language = "de";
    Check(Loc.T("About_Version", "1.2.3") == "Version 1.2.3", "Format falsch");
    Check(Loc.T("Gibt_Es_Nicht") == "[Gibt_Es_Nicht]", "Fallback falsch");
});

string TempDir()
{
    var d = Path.Combine(Path.GetTempPath(), "hmi-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    return d;
}
var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
var t0 = new DateTime(2026, 11, 26, 14, 32, 0);

Test("FileNaming: Seitenname und Format", () =>
{
    Check(FileNaming.BuildPageFileName(1, "Start", t0) == "01_Start_2026-11-26_1432.png", "Format falsch");
    Check(FileNaming.BuildPageFileName(12, "Haupt Menü", t0) == "12_Haupt_Menü_2026-11-26_1432.png", "Leerzeichen/Umlaut");
});

Test("FileNaming: verbotene Zeichen, leer, reserviert, lang", () =>
{
    Check(FileNaming.Sanitize("a/b:c*d?") == "abcd", "verbotene Zeichen");
    Check(FileNaming.Sanitize("  ") == "Seite", "leer");
    Check(FileNaming.Sanitize(null) == "Seite", "null");
    Check(FileNaming.Sanitize("CON") == "CON_", "reserviert");
    Check(FileNaming.Sanitize("a   b__c") == "a_b_c", "Mehrfach-Unterstrich");
    Check(FileNaming.Sanitize(new string('x', 200)).Length == FileNaming.MaxNameLength, "Länge");
});

Test("FileNaming: Word-Standardname", () =>
{
    Check(FileNaming.DefaultWordFileName("Anlage1", t0) == "Anlage1_Touchpanel_2026-11-26.docx", "Name");
    Check(FileNaming.DefaultWordFileName("", t0) == "Anlage_Touchpanel_2026-11-26.docx", "Fallback");
});

Test("FileNaming: MakeUnique", () =>
{
    var d = TempDir();
    File.WriteAllBytes(Path.Combine(d, "a.png"), png);
    Check(FileNaming.MakeUnique(d, "b.png") == "b.png", "frei");
    Check(FileNaming.MakeUnique(d, "a.png") == "a_2.png", "belegt");
    File.WriteAllBytes(Path.Combine(d, "a_2.png"), png);
    Check(FileNaming.MakeUnique(d, "a.png") == "a_3.png", "zweimal belegt");
});

Test("PageList: Reihenfolge, Move, Entfernen", () =>
{
    var l = new PageList();
    var a = l.Add("A", "a.png", t0, 1);
    var b = l.Add("B", "b.png", t0, 2);
    var c = l.Add("C", "c.png", t0, 3);
    Check(l.NextNumber() == 4, "NextNumber");
    Check(l.MoveUp(2) && l.Items[1] == c, "MoveUp");
    Check(!l.MoveUp(0), "MoveUp oben");
    Check(l.MoveDown(0) && l.Items[1] == a, "MoveDown");
    Check(!l.MoveDown(2), "MoveDown unten");
    Check(l.Move(0, 99) && l.Items[2] != null, "Move klemmt Ziel");
    Check(l.Remove(b.Id) && l.Items.Count == 2 && !l.Remove(b.Id), "Remove");
    Check(l.NextNumber() == 4, "Nummer nicht neu vergeben bei Max");
});

Test("ProjectFolder: AddPage speichert sofort und lädt wieder", () =>
{
    var d = TempDir();
    var pf = ProjectFolder.Open(d, "Anlage1");
    var e = pf.AddPage("Start", png, t0);
    Check(File.Exists(Path.Combine(d, "01_Start_2026-11-26_1432.png")), "PNG fehlt");
    Check(File.Exists(Path.Combine(d, ProjectFolder.ListFileName)), "pages.json fehlt");
    var pf2 = ProjectFolder.Open(d);
    Check(pf2.Pages.Items.Count == 1 && pf2.Pages.Items[0].Name == "Start" && pf2.Pages.Anlage == "Anlage1", "Laden");
    Check(pf2.FileExists(pf2.Pages.Items[0]), "FileExists");
});

Test("ProjectFolder: gleiche Sekunde/Name → kein Überschreiben", () =>
{
    var d = TempDir();
    var pf = ProjectFolder.Open(d);
    var a = pf.AddPage("X", png, t0);
    pf.RemovePage(a, deleteFile: false);
    var b = pf.AddPage("X", png, t0);
    Check(a.FileName != b.FileName || b.Number != a.Number, "Kollision");
});

Test("ProjectFolder: Umbenennen, Neu aufnehmen, Entfernen", () =>
{
    var d = TempDir();
    var pf = ProjectFolder.Open(d);
    var e = pf.AddPage("Start", png, t0);
    pf.RenamePage(e, "Übersicht");
    Check(e.FileName == "01_Übersicht_2026-11-26_1432.png" && pf.FileExists(e), "Rename");
    Check(Directory.GetFiles(d, "*.png").Length == 1, "alte Datei bleibt liegen");
    var t1 = t0.AddMinutes(5);
    pf.ReplacePage(e, png, t1);
    Check(e.FileName == "01_Übersicht_2026-11-26_1437.png" && Directory.GetFiles(d, "*.png").Length == 1, "Replace");
    pf.RemovePage(e, deleteFile: true);
    Check(pf.Pages.Items.Count == 0 && Directory.GetFiles(d, "*.png").Length == 0, "Remove");
});

Test("ProjectFolder: defekte pages.json → leer, .bad gesichert", () =>
{
    var d = TempDir();
    File.WriteAllText(Path.Combine(d, ProjectFolder.ListFileName), "{ kaputt");
    var pf = ProjectFolder.Open(d);
    Check(pf.Pages.Items.Count == 0 && File.Exists(Path.Combine(d, ProjectFolder.ListFileName + ".bad")), "defekt");
});

Test("PanelGeometry: gleiches Seitenverhältnis", () =>
{
    Check(PanelGeometry.TryMapToPanel(400, 240, 800, 480, 1600, 960, out var x, out var y) && x == 800 && y == 480, "Mitte");
    Check(PanelGeometry.TryMapToPanel(0, 0, 800, 480, 1600, 960, out x, out y) && x == 0 && y == 0, "Ecke");
});

Test("PanelGeometry: Letterbox (Fenster breiter als Panel)", () =>
{
    // Panel 800x480 im Fenster 1000x480 → Skala 1, links/rechts je 100 px Rand
    Check(!PanelGeometry.TryMapToPanel(50, 100, 1000, 480, 800, 480, out _, out _), "Rand links muss false sein");
    Check(PanelGeometry.TryMapToPanel(100, 0, 1000, 480, 800, 480, out var x, out var y) && x == 0 && y == 0, "Bildanfang");
    Check(PanelGeometry.TryMapToPanel(500, 240, 1000, 480, 800, 480, out x, out y) && x == 400 && y == 240, "Mitte");
    Check(!PanelGeometry.TryMapToPanel(900, 100, 1000, 480, 800, 480, out _, out _), "Rand rechts");
});

Test("PanelGeometry: Letterbox oben/unten und ungültige Grössen", () =>
{
    Check(!PanelGeometry.TryMapToPanel(100, 10, 800, 600, 800, 400, out _, out _), "Rand oben");
    Check(PanelGeometry.TryMapToPanel(100, 100, 800, 600, 800, 400, out _, out var y) && y == 0, "Bildanfang oben");
    Check(!PanelGeometry.TryMapToPanel(1, 1, 0, 0, 800, 400, out _, out _), "Fenstergrösse 0");
    Check(!PanelGeometry.TryMapToPanel(1, 1, 800, 600, 0, 0, out _, out _), "Panelgrösse 0");
});

async Task<byte[]> Rd(Stream st, int n)
{
    var b = new byte[n]; var o = 0;
    while (o < n) { var r = await st.ReadAsync(b.AsMemory(o, n - o)); if (r == 0) throw new EndOfStreamException(); o += r; }
    return b;
}
byte[] DesDecrypt(byte[] data, string pw)   // unabhängige Gegenprobe zu VncAuth
{
    var key = new byte[8]; var pb = Encoding.Latin1.GetBytes(pw);
    for (var i = 0; i < 8 && i < pb.Length; i++) { int v = pb[i], r = 0; for (var k = 0; k < 8; k++) { r = (r << 1) | (v & 1); v >>= 1; } key[i] = (byte)r; }
    using var des = System.Security.Cryptography.DES.Create();
    des.Mode = System.Security.Cryptography.CipherMode.ECB; des.Padding = System.Security.Cryptography.PaddingMode.None; des.Key = key;
    using var d = des.CreateDecryptor();
    return d.TransformFinalBlock(data, 0, 16);
}

Test("RFB: Anmeldung, Raw, Hextile und Tippen gegen simulierten Server", () => Task.Run(async () =>
{
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var gate = new SemaphoreSlim(0);
    bool authOk = false; byte[]? down = null, up = null; string? serverErr = null;
    var server = Task.Run(async () =>
    {
        try
        {
            using var c = await listener.AcceptTcpClientAsync(); var st = c.GetStream();
            await st.WriteAsync(Encoding.ASCII.GetBytes("RFB 003.008\n"));
            if (Encoding.ASCII.GetString(await Rd(st, 12)) != "RFB 003.008\n") throw new Exception("Version");
            await st.WriteAsync(new byte[] { 1, 2 });
            if ((await Rd(st, 1))[0] != 2) throw new Exception("Sicherheitsart");
            var ch = Enumerable.Range(1, 16).Select(i => (byte)(i * 7)).ToArray();
            await st.WriteAsync(ch);
            authOk = DesDecrypt(await Rd(st, 16), "geheim12").SequenceEqual(ch);
            await st.WriteAsync(new byte[] { 0, 0, 0, 0 });
            await Rd(st, 1);
            var init = new List<byte> { 0, 4, 0, 2 }; init.AddRange(new byte[16]); init.AddRange(new byte[] { 0, 0, 0, 1, (byte)'T' });
            await st.WriteAsync(init.ToArray());
            await Rd(st, 20);
            var hdr = await Rd(st, 4); await Rd(st, ((hdr[2] << 8) | hdr[3]) * 4);
            await Rd(st, 10);
            var raw = new List<byte> { 0, 0, 0, 1, 0, 0, 0, 0, 0, 4, 0, 2, 0, 0, 0, 0 };
            for (var p = 0; p < 8; p++) raw.AddRange(new byte[] { (byte)(p + 1), (byte)(p + 11), (byte)(p + 21), 0 });
            await st.WriteAsync(raw.ToArray());
            await Rd(st, 10);
            await gate.WaitAsync(TimeSpan.FromSeconds(5));
            await st.WriteAsync(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 4, 0, 2, 0, 0, 0, 5,
                26, 10, 20, 30, 0, 1, 7, 8, 9, 0, 0x11, 0x10 });
            await Rd(st, 10);
            down = await Rd(st, 6); up = await Rd(st, 6);
        }
        catch (Exception ex) { serverErr = ex.Message; }
    });

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await using var client = new RfbClient { PressDelayMs = 10 };
    var updates = new SemaphoreSlim(0);
    client.FrameUpdated += (_, _) => updates.Release();
    await client.ConnectAsync("127.0.0.1", port, "geheim12", cts.Token);
    Check(await updates.WaitAsync(TimeSpan.FromSeconds(5)), "Update 1 fehlt");
    var s1 = client.GetSnapshot()!;
    Check(s1.Width == 4 && s1.Height == 2 && s1.Bgra.Length == 32, "Grösse");
    Check(s1.Bgra.Take(4).SequenceEqual(new byte[] { 1, 11, 21, 255 }), "Raw Pixel 0");
    Check(s1.Bgra.Skip(20).Take(4).SequenceEqual(new byte[] { 6, 16, 26, 255 }), "Raw Pixel 5");
    gate.Release();
    Check(await updates.WaitAsync(TimeSpan.FromSeconds(5)), "Update 2 fehlt");
    var s2 = client.GetSnapshot()!;
    byte[] Px(int x, int y) => s2.Bgra.Skip((y * 4 + x) * 4).Take(4).ToArray();
    Check(Px(0, 0).SequenceEqual(new byte[] { 10, 20, 30, 255 }), "Hextile Hintergrund");
    Check(Px(1, 1).SequenceEqual(new byte[] { 7, 8, 9, 255 }) && Px(2, 1).SequenceEqual(new byte[] { 7, 8, 9, 255 }), "Hextile Subrect");
    Check(Px(3, 1).SequenceEqual(new byte[] { 10, 20, 30, 255 }), "Hextile ausserhalb Subrect");
    client.Tap(3, 1);
    Check(await Task.WhenAny(server, Task.Delay(5000)) == server, "Server Timeout");
    Check(serverErr == null, "Server: " + serverErr);
    Check(authOk, "Passwort-Antwort falsch");
    Check(down!.SequenceEqual(new byte[] { 5, 1, 0, 3, 0, 1 }), "Tippen: drücken");
    Check(up!.SequenceEqual(new byte[] { 5, 0, 0, 3, 0, 1 }), "Tippen: loslassen");
    listener.Stop();
}).GetAwaiter().GetResult());

Test("RFB: falsches Passwort → VncException", () => Task.Run(async () =>
{
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    _ = Task.Run(async () =>
    {
        using var c = await listener.AcceptTcpClientAsync(); var st = c.GetStream();
        await st.WriteAsync(Encoding.ASCII.GetBytes("RFB 003.008\n"));
        await Rd(st, 12);
        await st.WriteAsync(new byte[] { 1, 2 });
        await Rd(st, 1);
        await st.WriteAsync(new byte[16]);
        await Rd(st, 16);
        await st.WriteAsync(new byte[] { 0, 0, 0, 1, 0, 0, 0, 4, (byte)'n', (byte)'o', (byte)'p', (byte)'e' });
        await Task.Delay(300);
    });
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await using var client = new RfbClient();
    try { await client.ConnectAsync("127.0.0.1", port, "falsch", cts.Token); Check(false, "keine Ausnahme"); }
    catch (VncException ex) { Check(ex.Message.Contains("Anmeldung fehlgeschlagen") && ex.Message.Contains("nope"), ex.Message); }
    Check(!client.IsConnected, "darf nicht verbunden sein");
    listener.Stop();
}).GetAwaiter().GetResult());

Test("RFB: Server spricht kein VNC / Verbindung verweigert", () => Task.Run(async () =>
{
    await using var client = new RfbClient();
    try { await client.ConnectAsync("127.0.0.1", 1, "x", CancellationToken.None); Check(false, "keine Ausnahme"); }
    catch (VncException) { }
}).GetAwaiter().GetResult());

// Test-Schutz: reversibel, Format wie DPAPI-Ausgabe ("dpapi:…")
var prot = new FakeProtector();

Test("Settings: Passwort wird nie im Klartext gespeichert", () =>
{
    var d = TempDir();
    var store = new SettingsStore(prot, Path.Combine(d, "settings.json"));
    var s = new AppSettings();
    s.Profiles.Add(new ConnectionProfile { Name = "P1", Host = "panel.example", Port = 5901, Password = "geheim12" });
    store.Save(s);
    var text = File.ReadAllText(store.Path);
    Check(!text.Contains("geheim12"), "Klartext in Datei");
    Check(text.Contains("\"PasswordProtected\": \"dpapi:"), "geschützter Wert fehlt");
    var back = store.Load();
    Check(back.Profiles[0].Password == "geheim12" && back.Profiles[0].Port == 5901, "Roundtrip");
});

Test("Settings: Klartext-Datei wird migriert", () =>
{
    var d = TempDir();
    var path = Path.Combine(d, "settings.json");
    File.WriteAllText(path, "{\"Profiles\":[{\"Name\":\"P\",\"Host\":\"h\",\"Password\":\"alt12345\"}]}");
    var store = new SettingsStore(prot, path);
    var s = store.Load();
    Check(s.Profiles[0].Password == "alt12345", "nicht gelesen");
    store.Save(s);
    var text = File.ReadAllText(path);
    Check(!text.Contains("alt12345") && text.Contains("dpapi:"), "nicht migriert");
    Check(!text.Contains("\"Password\""), "Altfeld bleibt");
});

Test("Settings: defekter Schutzwert → leeres Passwort; defekte Datei → Standard", () =>
{
    var d = TempDir();
    var path = Path.Combine(d, "settings.json");
    File.WriteAllText(path, "{\"Profiles\":[{\"Name\":\"P\",\"PasswordProtected\":\"dpapi:%%%\"}]}");
    var store = new SettingsStore(prot, path);
    var s = store.Load();
    Check(s.Profiles[0].Password == "" && s.Profiles[0].PasswordProtected == "", "defekt");
    File.WriteAllText(path, "nix json");
    Check(store.Load().Language == "de", "Standard");
    Check(new SettingsStore(prot, Path.Combine(d, "fehlt.json")).Load().Profiles.Count == 0, "fehlt");
});

Test("Settings: Standardpfad liegt in %APPDATA%\\HMIImageExport", () =>
{
    Check(SettingsStore.DefaultPath.EndsWith(Path.Combine("HMIImageExport", "settings.json")), "Pfad");
});

Console.WriteLine(failed == 0 ? "Alle Tests bestanden." : $"{failed} Test(s) fehlgeschlagen.");
return failed == 0 ? 0 : 1;

sealed class FakeProtector : ISecretProtector
{
    public string Protect(string plain) => "dpapi:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plain));
    public string Unprotect(string v)
    {
        if (!v.StartsWith("dpapi:")) throw new FormatException();
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(v[6..]));
    }
}
