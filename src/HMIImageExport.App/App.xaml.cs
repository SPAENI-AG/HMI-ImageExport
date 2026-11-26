using System.IO;
using System.Windows;
using HMIImageExport.App.Infrastructure;
using HMIImageExport.Core;

namespace HMIImageExport.App;

public partial class App : Application
{
    public static SettingsStore Store { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        Store = new SettingsStore(new DpapiSecretProtector());
        Settings = Store.Load();
        Loc.Instance.Language = Settings.Language;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        HelpLauncher.Shutdown();
        base.OnExit(e);
    }

    /// <summary>Speichert die Einstellungen (Passwörter verschlüsselt). Fehler beim Schreiben stören die Bedienung nicht.</summary>
    public static void SaveSettings()
    {
        try { Store.Save(Settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string anchor }) HelpLauncher.Open(anchor);
    }
}
