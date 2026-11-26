using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using HMIImageExport.App.Infrastructure;
using HMIImageExport.App.Views;
using HMIImageExport.Core;

namespace HMIImageExport.App;

public partial class MainWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private UpdateInfo? _update;

    public MainWindow()
    {
        InitializeComponent();
        tb_FooterCompany.Text = AppInfo.Company;
        tb_FooterVersion.Text = AppInfo.VersionText;
        SyncLanguageMenu();
        Loc.Instance.LanguageChanged += (_, _) =>
        {
            SyncLanguageMenu();
            tb_FooterVersion.Text = AppInfo.VersionText;
            ApplyBannerText();
            App.Settings.Language = Loc.Instance.Language;
            App.SaveSettings();
        };
        Loaded += async (_, _) => await CheckUpdatesAsync(manual: false);
    }

    private void SyncLanguageMenu()
    {
        mi_LangDe.IsChecked = Loc.Instance.Language == "de";
        mi_LangEn.IsChecked = Loc.Instance.Language == "en";
    }

    private void mi_Lang_Click(object sender, RoutedEventArgs e)
    {
        Loc.Instance.Language = (string)((MenuItem)sender).Tag;
        SyncLanguageMenu();
    }

    private void mi_Settings_Click(object sender, RoutedEventArgs e) => tc_Main.SelectedItem = ti_Settings;

    private void mi_Open_Click(object sender, RoutedEventArgs e) => view_Capture.ChooseFolder();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F1) { HelpLauncher.Open(); e.Handled = true; return; }
        if (view_Capture.HandleKey(e)) e.Handled = true;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => view_Capture.Shutdown();

    private void mi_Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void mi_About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void mi_Help_Click(object sender, RoutedEventArgs e) =>
        HelpLauncher.Open((sender as MenuItem)?.Tag as string);

    private void mi_SettingsFolder_Click(object sender, RoutedEventArgs e) => SettingsView.OpenSettingsFolder();

    // ---------- Update-Hinweis ----------

    private async void mi_CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(manual: true);

    private async Task CheckUpdatesAsync(bool manual)
    {
        var repo = AppInfo.GitHubRepository;
        if (!UpdateChecker.IsEnabled(repo))
        {
            if (manual) Info(Loc.T("Update_Disabled"));
            return;
        }
        if (!manual && !UpdateChecker.ShouldCheck(App.Settings.LastUpdateCheck, DateTime.UtcNow, TimeSpan.FromHours(24))) return;
        try
        {
            var info = await UpdateChecker.CheckAsync(Http, repo, AppInfo.Version, CancellationToken.None);
            App.Settings.LastUpdateCheck = DateTime.UtcNow;
            App.SaveSettings();
            if (info == null)
            {
                if (manual) Info(Loc.T("Update_UpToDate", AppInfo.Version));
                return;
            }
            if (!manual && info.Version == App.Settings.SkippedVersion) return;
            _update = info;
            ApplyBannerText();
            bd_UpdateBanner.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            if (manual) MessageBox.Show(this, Loc.T("Update_Failed", ex.Message), Loc.T("Err_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyBannerText() =>
        tb_UpdateText.Text = _update == null ? "" : Loc.T("Update_Available", _update.Version, AppInfo.Version);

    private void bt_UpdateDownload_Click(object sender, RoutedEventArgs e) => OpenUrl(_update?.DownloadUrl);
    private void bt_UpdateNotes_Click(object sender, RoutedEventArgs e) => OpenUrl(_update?.ReleaseUrl);
    private void bt_UpdateClose_Click(object sender, RoutedEventArgs e) => bd_UpdateBanner.Visibility = Visibility.Collapsed;

    private void bt_UpdateSkip_Click(object sender, RoutedEventArgs e)
    {
        if (_update != null) { App.Settings.SkippedVersion = _update.Version; App.SaveSettings(); }
        bd_UpdateBanner.Visibility = Visibility.Collapsed;
    }

    private static void OpenUrl(string? url)
    {
        if (!UpdateChecker.IsSafeUrl(url)) return;
        Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
    }

    private void Info(string text) =>
        MessageBox.Show(this, text, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
