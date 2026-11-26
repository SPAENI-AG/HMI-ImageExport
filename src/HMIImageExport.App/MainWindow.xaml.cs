using System.Windows;
using System.Windows.Controls;
using HMIImageExport.App.Infrastructure;
using HMIImageExport.Core;

namespace HMIImageExport.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        tb_FooterCompany.Text = AppInfo.Company;
        tb_FooterVersion.Text = AppInfo.VersionText;
        SyncLanguageMenu();
        Loc.Instance.LanguageChanged += (_, _) => { SyncLanguageMenu(); tb_FooterVersion.Text = AppInfo.VersionText; };
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
        if (view_Capture.HandleKey(e)) e.Handled = true;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => view_Capture.Shutdown();

    private void mi_Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void mi_About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();
}
