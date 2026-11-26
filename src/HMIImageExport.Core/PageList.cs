namespace HMIImageExport.Core;

/// <summary>Eine aufgenommene Panel-Seite. Die Reihenfolge steht in der Liste, nicht im Dateinamen.</summary>
public sealed class PageEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Laufnummer zum Zeitpunkt der Aufnahme (Dateinamen-Präfix).</summary>
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime CapturedAt { get; set; }
}

/// <summary>Seitenliste einer Anlage inkl. Reihenfolge.</summary>
public sealed class PageList
{
    public string Anlage { get; set; } = "";
    public List<PageEntry> Items { get; set; } = new();

    public int NextNumber() => Items.Count == 0 ? 1 : Items.Max(p => p.Number) + 1;

    public PageEntry Add(string name, string fileName, DateTime capturedAt, int number)
    {
        var e = new PageEntry { Number = number, Name = name, FileName = fileName, CapturedAt = capturedAt };
        Items.Add(e);
        return e;
    }

    public bool Remove(Guid id) => Items.RemoveAll(p => p.Id == id) > 0;

    public int IndexOf(Guid id) => Items.FindIndex(p => p.Id == id);

    /// <summary>Verschiebt einen Eintrag; Zielindex wird auf den gültigen Bereich begrenzt.</summary>
    public bool Move(int from, int to)
    {
        if (from < 0 || from >= Items.Count) return false;
        to = Math.Clamp(to, 0, Items.Count - 1);
        if (from == to) return false;
        var e = Items[from];
        Items.RemoveAt(from);
        Items.Insert(to, e);
        return true;
    }

    public bool MoveUp(int index) => index > 0 && Move(index, index - 1);
    public bool MoveDown(int index) => index >= 0 && index < Items.Count - 1 && Move(index, index + 1);
}
