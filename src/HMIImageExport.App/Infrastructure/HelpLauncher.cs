using System.Diagnostics;
using System.IO;
using System.Windows;
using HMIImageExport.Core;

namespace HMIImageExport.App.Infrastructure;

/// <summary>Startet den lokalen Hilfe-Server (nur localhost) und öffnet die Hilfe im Standardbrowser in der Oberflächensprache.</summary>
public static class HelpLauncher
{
    private static HelpServer? _server;

    public static void Open(string? anchor = null)
    {
        try
        {
            if (_server == null)
            {
                var root = Path.Combine(AppContext.BaseDirectory, "Help");
                if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
                var s = new HelpServer(root);
                s.Start();
                _server = s;
            }
            var url = $"{_server.BaseUrl}{Loc.Instance.Language}.html" + (string.IsNullOrEmpty(anchor) ? "" : "#" + anchor);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.T("Help_Failed", ex.Message), Loc.T("Err_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static void Shutdown()
    {
        _server?.Dispose();
        _server = null;
    }
}
