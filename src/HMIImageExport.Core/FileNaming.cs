using System.Text;

namespace HMIImageExport.Core;

/// <summary>Dateinamen für Screenshots und Word-Export (Windows-tauglich, auch unter Linux testbar).</summary>
public static class FileNaming
{
    private const string InvalidChars = "\\/:*?\"<>|";
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public const string FallbackName = "Seite";
    public const int MaxNameLength = 60;

    /// <summary>Macht aus freiem Text einen gültigen Dateinamen-Teil (Leerzeichen → _, verbotene Zeichen entfernt).</summary>
    public static string Sanitize(string? name, string fallback = FallbackName)
    {
        var sb = new StringBuilder();
        var lastUnderscore = false;
        foreach (var c in (name ?? "").Trim())
        {
            if (char.IsControl(c) || InvalidChars.Contains(c)) continue;
            if (char.IsWhiteSpace(c) || c == '_')
            {
                if (!lastUnderscore) sb.Append('_');
                lastUnderscore = true;
                continue;
            }
            sb.Append(c);
            lastUnderscore = false;
        }
        var s = sb.ToString().Trim('_', '.', ' ');
        if (s.Length > MaxNameLength) s = s[..MaxNameLength].TrimEnd('_', '.', ' ');
        if (s.Length == 0) return fallback;
        if (Reserved.Contains(s)) s += "_";
        return s;
    }

    /// <summary>z. B. 01_Start_2026-11-26_1432.png</summary>
    public static string BuildPageFileName(int number, string pageName, DateTime capturedAt, string? language = null)
    {
        var lang = Sanitize(language, "");
        return $"{number:00}_{Sanitize(pageName)}{(lang.Length > 0 ? "_" + lang : "")}_{capturedAt:yyyy-MM-dd_HHmm}.png";
    }

    /// <summary>Hängt _2, _3 … an, falls die Datei im Ordner schon existiert.</summary>
    public static string MakeUnique(string folder, string fileName)
    {
        if (!File.Exists(Path.Combine(folder, fileName))) return fileName;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem}_{i}{ext}";
            if (!File.Exists(Path.Combine(folder, candidate))) return candidate;
        }
    }

    /// <summary>z. B. Anlage1_Touchpanel_2026-11-26.docx</summary>
    public static string DefaultWordFileName(string? anlage, DateTime date) =>
        $"{Sanitize(anlage, "Anlage")}_Touchpanel_{date:yyyy-MM-dd}.docx";
}
