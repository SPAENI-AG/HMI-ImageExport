namespace HMIImageExport.Core;

/// <summary>Ein Bild für das Word-Dokument.</summary>
public sealed record WordImage(string FilePath, string Name, DateTime CapturedAt, string Language = "");

public sealed class WordExportOptions
{
    public string Anlage { get; set; } = "";
    public string ProjectNumber { get; set; } = "";
    /// <summary>Beschreibungszeile unter jedem Bild (Seitenname · Anlage · Datum).</summary>
    public bool IncludeCaption { get; set; } = true;
    public bool IncludeCoverPage { get; set; }
    /// <summary>A4 quer (sonst hoch).</summary>
    public bool Landscape { get; set; } = true;
    public string CoverTitle { get; set; } = "Touchpanel-Dokumentation";
    /// <summary>Zeilen unter dem Titel (bereits übersetzt/formatiert).</summary>
    public List<string> CoverLines { get; set; } = new();
}

/// <summary>Seitenmasse und Bildgrössen für das Word-Dokument (A4, Ränder 2 cm).</summary>
public static class WordLayout
{
    public const long EmuPerTwip = 635;
    public const int MarginTwips = 1134;           // 2 cm
    public const int A4ShortTwips = 11906;
    public const int A4LongTwips = 16838;
    public const long CaptionAllowanceEmu = 540_000;   // 1,5 cm Platz für Beschriftung
    public const long SlackEmu = 108_000;              // 0,3 cm Reserve ohne Beschriftung

    public static int PageWidthTwips(bool landscape) => landscape ? A4LongTwips : A4ShortTwips;
    public static int PageHeightTwips(bool landscape) => landscape ? A4ShortTwips : A4LongTwips;

    public static long ContentWidthEmu(bool landscape) =>
        (PageWidthTwips(landscape) - 2 * MarginTwips) * EmuPerTwip;

    public static long ContentHeightEmu(bool landscape, bool caption) =>
        (PageHeightTwips(landscape) - 2 * MarginTwips) * EmuPerTwip - (caption ? CaptionAllowanceEmu : SlackEmu);

    /// <summary>Skaliert auf Seitenbreite; ist das Bild dafür zu hoch, wird es auf die Seitenhöhe begrenzt.</summary>
    public static (long Cx, long Cy) Fit(int imgW, int imgH, long maxW, long maxH)
    {
        if (imgW <= 0 || imgH <= 0) throw new ArgumentException("Ungültige Bildgrösse.");
        var scale = Math.Min((double)maxW / imgW, (double)maxH / imgH);
        return ((long)Math.Round(imgW * scale), (long)Math.Round(imgH * scale));
    }

    /// <summary>z. B. «Start · Anlage1 · 26.11.2026»</summary>
    public static string CaptionText(string name, string anlage, DateTime date, string? language = null)
    {
        var title = string.IsNullOrWhiteSpace(language) ? name : $"{name} ({language})";
        return string.IsNullOrWhiteSpace(anlage) ? $"{title} · {date:dd.MM.yyyy}" : $"{title} · {anlage} · {date:dd.MM.yyyy}";
    }
}

/// <summary>Liest die Bildgrösse aus dem PNG-Kopf (ohne Bibliothek).</summary>
public static class PngInfo
{
    public static bool TryReadSize(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            using var fs = File.OpenRead(path);
            var b = new byte[24];
            fs.ReadExactly(b);
            if (b[0] != 0x89 || b[1] != 0x50 || b[2] != 0x4E || b[3] != 0x47) return false;
            width = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            height = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
            return width > 0 && height > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
