using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HMIImageExport.Core;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace HMIImageExport.Export;

public sealed record WordExportResult(int ImagesWritten, IReadOnlyList<string> MissingFiles);

/// <summary>Erstellt ein .docx ohne Word-Installation (DocumentFormat.OpenXml): ein Bild pro Seite, auf Seitenbreite skaliert.</summary>
public static class WordExporter
{
    public static WordExportResult Export(string outputPath, IReadOnlyList<WordImage> images, WordExportOptions o)
    {
        var missing = new List<string>();
        var valid = new List<(WordImage Img, int W, int H)>();
        foreach (var im in images)
        {
            if (File.Exists(im.FilePath) && PngInfo.TryReadSize(im.FilePath, out var w, out var h)) valid.Add((im, w, h));
            else missing.Add(im.Name);
        }
        if (valid.Count == 0) throw new InvalidOperationException("Keine Bilder zum Exportieren.");

        var tmp = outputPath + ".tmp";
        try
        {
            using (var doc = WordprocessingDocument.Create(tmp, WordprocessingDocumentType.Document))
                Build(doc, valid, o);
            File.Move(tmp, outputPath, true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        return new WordExportResult(valid.Count, missing);
    }

    private static void Build(WordprocessingDocument doc, List<(WordImage Img, int W, int H)> images, WordExportOptions o)
    {
        var main = doc.AddMainDocumentPart();
        var body = new Body();
        main.Document = new Document(body);

        var needBreak = false;
        if (o.IncludeCoverPage)
        {
            body.Append(TextPara(o.CoverTitle, 56, bold: true, color: null, Before: 3000, After: 600));
            foreach (var line in o.CoverLines.Where(l => !string.IsNullOrWhiteSpace(l)))
                body.Append(TextPara(line, 28, bold: false, color: null, Before: 0, After: 120));
            needBreak = true;
        }

        var maxW = WordLayout.ContentWidthEmu(o.Landscape);
        var maxH = WordLayout.ContentHeightEmu(o.Landscape, o.IncludeCaption);
        var id = 1;
        foreach (var (img, w, h) in images)
        {
            var (cx, cy) = WordLayout.Fit(w, h, maxW, maxH);
            var part = main.AddImagePart(ImagePartType.Png);
            using (var fs = File.OpenRead(img.FilePath)) part.FeedData(fs);
            var rel = main.GetIdOfPart(part);

            var ppr = new ParagraphProperties();
            if (o.IncludeCaption) ppr.Append(new KeepNext());
            if (needBreak) ppr.Append(new PageBreakBefore());
            ppr.Append(new SpacingBetweenLines { Before = "0", After = "60" });
            ppr.Append(new Justification { Val = JustificationValues.Center });
            body.Append(new Paragraph(ppr, new Run(CreateDrawing(rel, id, img, cx, cy))));

            if (o.IncludeCaption)
                body.Append(TextPara(WordLayout.CaptionText(img.Name, o.Anlage, img.CapturedAt), 20, bold: false,
                    color: "6B7280", Before: 0, After: 0, center: true));

            needBreak = true;
            id++;
        }

        body.Append(new SectionProperties(
            new PageSize
            {
                Width = (uint)WordLayout.PageWidthTwips(o.Landscape),
                Height = (uint)WordLayout.PageHeightTwips(o.Landscape),
                Orient = o.Landscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait,
            },
            new PageMargin
            {
                Top = WordLayout.MarginTwips, Bottom = WordLayout.MarginTwips,
                Left = (uint)WordLayout.MarginTwips, Right = (uint)WordLayout.MarginTwips,
                Header = 720U, Footer = 720U, Gutter = 0U,
            }));
        main.Document.Save();
    }

    private static Paragraph TextPara(string text, int halfPoints, bool bold, string? color, int Before, int After, bool center = false)
    {
        var ppr = new ParagraphProperties(new SpacingBetweenLines { Before = Before.ToString(), After = After.ToString() });
        if (center) ppr.Append(new Justification { Val = JustificationValues.Center });
        var rpr = new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
        if (bold) rpr.Append(new Bold());
        if (color != null) rpr.Append(new Color { Val = color });
        rpr.Append(new FontSize { Val = halfPoints.ToString() });
        return new Paragraph(ppr, new Run(rpr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Drawing CreateDrawing(string relId, int id, WordImage img, long cx, long cy)
    {
        var fileName = Path.GetFileName(img.FilePath);
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = (uint)id, Name = $"Bild {id}", Description = img.Name },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = fileName },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(new A.Blip { Embed = relId }, new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
            });
    }
}
