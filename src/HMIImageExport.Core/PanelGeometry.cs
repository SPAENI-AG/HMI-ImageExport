namespace HMIImageExport.Core;

/// <summary>Rechnet Mauskoordinaten im Anzeigefenster (Stretch=Uniform, zentriert) in Panel-Pixel um.</summary>
public static class PanelGeometry
{
    /// <summary>
    /// Liefert false, wenn der Punkt ausserhalb des Panel-Bildes liegt (Rand der Letterbox).
    /// </summary>
    public static bool TryMapToPanel(double x, double y, double viewWidth, double viewHeight,
        int panelWidth, int panelHeight, out int panelX, out int panelY)
    {
        panelX = panelY = 0;
        if (viewWidth <= 0 || viewHeight <= 0 || panelWidth <= 0 || panelHeight <= 0) return false;

        var scale = Math.Min(viewWidth / panelWidth, viewHeight / panelHeight);
        var drawnW = panelWidth * scale;
        var drawnH = panelHeight * scale;
        var offX = (viewWidth - drawnW) / 2;
        var offY = (viewHeight - drawnH) / 2;

        var rx = (x - offX) / scale;
        var ry = (y - offY) / scale;
        if (rx < 0 || ry < 0 || rx >= panelWidth || ry >= panelHeight) return false;

        panelX = (int)rx;
        panelY = (int)ry;
        return true;
    }
}
