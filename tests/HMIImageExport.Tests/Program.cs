using System.Text.RegularExpressions;
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
