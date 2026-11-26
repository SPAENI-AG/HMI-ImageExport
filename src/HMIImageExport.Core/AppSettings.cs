using System.Text.Json;
using System.Text.Json.Serialization;

namespace HMIImageExport.Core;

/// <summary>Verschlüsselt/entschlüsselt Geheimnisse (App: DPAPI). Protect liefert "dpapi:…".</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    /// <summary>Wirft bei defektem Wert eine Exception.</summary>
    string Unprotect(string protectedValue);
}

public sealed class ConnectionProfile
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 5900;

    /// <summary>Nur im Speicher; wird nie als Klartext geschrieben.</summary>
    [JsonIgnore] public string Password { get; set; } = "";

    /// <summary>In der Datei: "dpapi:…".</summary>
    public string PasswordProtected { get; set; } = "";

    /// <summary>Nur zum Einlesen alter Klartext-Dateien (wird beim Laden migriert, nie geschrieben).</summary>
    [JsonPropertyName("Password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPassword { get; set; }
}

public sealed class AppSettings
{
    public string Language { get; set; } = "de";
    public string LastProjectFolder { get; set; } = "";
    public List<ConnectionProfile> Profiles { get; set; } = new();
    public bool WordIncludeCaption { get; set; } = true;
    public bool WordIncludeCoverPage { get; set; } = false;
    public bool WordLandscape { get; set; } = true;
    public string LastProfile { get; set; } = "";
    /// <summary>Mehrsprachige Aufnahme: pro Seite alle Sprachen durchschalten.</summary>
    public bool MultiLanguage { get; set; }
    public string Languages { get; set; } = "DE, EN";
    /// <summary>Panel-Pixel der Sprach-Taste (-1 = nicht festgelegt). Ein Klick schaltet zur nächsten Sprache.</summary>
    /// <summary>Verfahren für den Sprachwechsel am Panel; bisher nur "cycle" (Klick = nächste Sprache).</summary>
    public string LangSwitchMode { get; set; } = LangSwitchModes.Cycle;
    public int LangButtonX { get; set; } = -1;
    public int LangButtonY { get; set; } = -1;
    /// <summary>Ruhezeit (ms) ohne Bildänderung nach dem Umschalten.</summary>
    public int LangWaitMs { get; set; } = 600;
    /// <summary>Zeitpunkt (UTC) der letzten Update-Prüfung.</summary>
    public DateTime? LastUpdateCheck { get; set; }
    public string SkippedVersion { get; set; } = "";
}

/// <summary>Lädt/speichert settings.json in %APPDATA%\HMIImageExport. Passwörter nur geschützt.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly ISecretProtector _protector;

    public string Path { get; }

    public SettingsStore(ISecretProtector protector, string? path = null)
    {
        _protector = protector;
        Path = path ?? DefaultPath;
    }

    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HMIImageExport", "settings.json");

    public AppSettings Load()
    {
        AppSettings? s = null;
        if (File.Exists(Path))
        {
            try { s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json); }
            catch (JsonException) { /* defekte Datei → Standard */ }
        }
        s ??= new AppSettings();
        s.Profiles ??= new();
        foreach (var p in s.Profiles)
        {
            if (!string.IsNullOrEmpty(p.LegacyPassword))
            {
                p.Password = p.LegacyPassword;          // Migration Klartext → geschützt (beim nächsten Save)
            }
            else if (!string.IsNullOrEmpty(p.PasswordProtected))
            {
                try { p.Password = _protector.Unprotect(p.PasswordProtected); }
                catch (Exception) { p.Password = ""; p.PasswordProtected = ""; }   // defekt → leer
            }
            p.LegacyPassword = null;
        }
        return s;
    }

    public void Save(AppSettings s)
    {
        foreach (var p in s.Profiles)
        {
            p.PasswordProtected = string.IsNullOrEmpty(p.Password) ? "" : _protector.Protect(p.Password);
            p.LegacyPassword = null;
        }
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
        File.Move(tmp, Path, true);
    }
}
