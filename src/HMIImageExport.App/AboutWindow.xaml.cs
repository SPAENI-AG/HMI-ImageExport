using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using HMIImageExport.App.Infrastructure;
using HMIImageExport.Core;

namespace HMIImageExport.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        hl_SupportUrl.Inlines.Add(AppInfo.SupportUrl);
        hl_SupportUrl.Tag = AppInfo.SupportUrl;
        hl_SupportMail.Inlines.Add(AppInfo.SupportEmail);
        hl_SupportMail.Tag = "mailto:" + AppInfo.SupportEmail;
        tb_Copyright.Text = AppInfo.Copyright;
        tb_Company.Text = AppInfo.Company;
        Loc.Instance.LanguageChanged += (_, _) => Apply();
        Apply();
    }

    private void Apply()
    {
        Title = Loc.T("About_Title", AppInfo.Title);
        tb_Title.Text = AppInfo.Title;
        tb_Version.Text = Loc.T("About_Version", AppInfo.Version);
        tb_Developer.Text = Loc.T("About_Developer", AppInfo.Developer);
    }

    private void hl_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo((string)((Hyperlink)sender).Tag) { UseShellExecute = true });
}
