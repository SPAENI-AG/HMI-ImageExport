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

    /// <summary>Neue Seite. Bei mehrsprachiger Aufnahme gleiche number/groupId für alle Sprachen der Seite.</summary>
    public PageEntry AddPage(string name, byte[] png, DateTime now, string language = "", int? number = null, Guid groupId = default)
    {
        var nr = number ?? Pages.NextNumber();
        var fileName = FileNaming.MakeUnique(Path, FileNaming.BuildPageFileName(nr, name, now, language));
        File.WriteAllBytes(System.IO.Path.Combine(Path, fileName), png);
        var e = Pages.Add(name, fileName, now, nr, language, groupId);
        Save();
        return e;
    }

    /// <summary>Neu aufnehmen: neues Bild, neuer Zeitstempel, gleiche Position; altes Bild wird gelöscht.</summary>
    public void ReplacePage(PageEntry e, byte[] png, DateTime now)
    {
        var old = FullPath(e);
        var fileName = FileNaming.MakeUnique(Path, FileNaming.BuildPageFileName(e.Number, e.Name, now, e.Language));
        File.WriteAllBytes(System.IO.Path.Combine(Path, fileName), png);
        if (File.Exists(old)) File.Delete(old);
        e.FileName = fileName;
        e.CapturedAt = now;
        Save();
    }

    /// <summary>Benennt Seite und Datei um (Nummer, Sprache und Zeitstempel bleiben). Gilt für alle Sprachen derselben Seite.</summary>
    public void RenamePage(PageEntry e, string newName)
    {
        var targets = e.GroupId == Guid.Empty ? new List<PageEntry> { e } : Pages.Items.Where(p => p.GroupId == e.GroupId).ToList();
        foreach (var t in targets)
        {
            var old = FullPath(t);
            var wanted = FileNaming.BuildPageFileName(t.Number, newName, t.CapturedAt, t.Language);
            if (!string.Equals(wanted, t.FileName, StringComparison.Ordinal))
            {
                var fileName = FileNaming.MakeUnique(Path, wanted);
                if (File.Exists(old)) File.Move(old, System.IO.Path.Combine(Path, fileName));
                t.FileName = fileName;
            }
            t.Name = newName;
        }
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
