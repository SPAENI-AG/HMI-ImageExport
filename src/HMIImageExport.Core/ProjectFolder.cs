using System.Text.Json;

namespace HMIImageExport.Core;

/// <summary>
/// Anlagenordner mit PNG-Dateien und pages.json. Jede Änderung wird sofort gespeichert.
/// Dateinamen bleiben beim Umsortieren unverändert, die Reihenfolge steht in pages.json.
/// </summary>
public sealed class ProjectFolder
{
    public const string ListFileName = "pages.json";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public string Path { get; }
    public PageList Pages { get; private set; }

    private ProjectFolder(string path, PageList pages) { Path = path; Pages = pages; }

    /// <summary>Öffnet den Ordner (legt ihn an, falls nötig). Defekte pages.json → leere Liste, Original als .bad gesichert.</summary>
    public static ProjectFolder Open(string path, string? anlageName = null)
    {
        Directory.CreateDirectory(path);
        var listPath = System.IO.Path.Combine(path, ListFileName);
        PageList? pages = null;
        if (File.Exists(listPath))
        {
            try { pages = JsonSerializer.Deserialize<PageList>(File.ReadAllText(listPath), Json); }
            catch (JsonException) { File.Copy(listPath, listPath + ".bad", true); }
        }
        pages ??= new PageList();
        if (string.IsNullOrWhiteSpace(pages.Anlage))
            pages.Anlage = anlageName ?? new DirectoryInfo(path).Name;
        return new ProjectFolder(path, pages);
    }

    public string FullPath(PageEntry e) => System.IO.Path.Combine(Path, e.FileName);
    public bool FileExists(PageEntry e) => File.Exists(FullPath(e));

    public PageEntry AddPage(string name, byte[] png, DateTime now)
    {
        var number = Pages.NextNumber();
        var fileName = FileNaming.MakeUnique(Path, FileNaming.BuildPageFileName(number, name, now));
        File.WriteAllBytes(System.IO.Path.Combine(Path, fileName), png);
        var e = Pages.Add(name, fileName, now, number);
        Save();
        return e;
    }

    /// <summary>Neu aufnehmen: neues Bild, neuer Zeitstempel, gleiche Position; altes Bild wird gelöscht.</summary>
    public void ReplacePage(PageEntry e, byte[] png, DateTime now)
    {
        var old = FullPath(e);
        var fileName = FileNaming.MakeUnique(Path, FileNaming.BuildPageFileName(e.Number, e.Name, now));
        File.WriteAllBytes(System.IO.Path.Combine(Path, fileName), png);
        if (File.Exists(old)) File.Delete(old);
        e.FileName = fileName;
        e.CapturedAt = now;
        Save();
    }

    /// <summary>Benennt Seite und Datei um (Nummer und Zeitstempel bleiben).</summary>
    public void RenamePage(PageEntry e, string newName)
    {
        var old = FullPath(e);
        var wanted = FileNaming.BuildPageFileName(e.Number, newName, e.CapturedAt);
        if (!string.Equals(wanted, e.FileName, StringComparison.Ordinal))
        {
            var fileName = FileNaming.MakeUnique(Path, wanted);
            if (File.Exists(old)) File.Move(old, System.IO.Path.Combine(Path, fileName));
            e.FileName = fileName;
        }
        e.Name = newName;
        Save();
    }

    public void RemovePage(PageEntry e, bool deleteFile)
    {
        Pages.Remove(e.Id);
        if (deleteFile && File.Exists(FullPath(e))) File.Delete(FullPath(e));
        Save();
    }

    public void Save()
    {
        var target = System.IO.Path.Combine(Path, ListFileName);
        var tmp = target + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Pages, Json));
        File.Move(tmp, target, true);
    }
}
