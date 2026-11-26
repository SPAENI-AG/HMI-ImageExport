namespace HMIImageExport.Core;

/// <summary>Sprachkürzel für die mehrsprachige Aufnahme (z. B. "DE, EN, FR").</summary>
public static class LangSwitchModes
{
    /// <summary>Ein Klick auf die Sprach-Taste schaltet zur nächsten Sprache (nach der letzten wieder zur ersten).</summary>
    public const string Cycle = "cycle";
}

public static class LanguageList
{
    /// <summary>Trennt an Komma/Semikolon, bereinigt für Dateinamen, entfernt leere und doppelte Einträge.</summary>
    public static List<string> Parse(string? text)
    {
        var result = new List<string>();
        foreach (var part in (text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var l = FileNaming.Sanitize(part, "");
            if (l.Length > 0 && !result.Contains(l, StringComparer.OrdinalIgnoreCase)) result.Add(l);
        }
        return result;
    }
}
