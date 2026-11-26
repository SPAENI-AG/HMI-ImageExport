using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HMIImageExport.Core;

namespace HMIImageExport.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshList(null);
        UpdateEnabled();
    }

    private ConnectionProfile? Selected => lb_Profiles.SelectedItem as ConnectionProfile;

    private void RefreshList(ConnectionProfile? select)
    {
        lb_Profiles.ItemsSource = null;
        lb_Profiles.ItemsSource = App.Settings.Profiles;
        if (select != null) lb_Profiles.SelectedItem = select;
        else if (App.Settings.Profiles.Count > 0 && lb_Profiles.SelectedItem == null) lb_Profiles.SelectedIndex = 0;
    }

    private void lb_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var p = Selected;
        tb_Name.Text = p?.Name ?? "";
        tb_Host.Text = p?.Host ?? "";
        tb_Port.Text = p?.Port.ToString() ?? "5900";
        pb_Password.Password = p?.Password ?? "";
        tb_Status.Text = "";
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        var has = Selected != null;
        grid_Profile.IsEnabled = has;
        bt_Delete.IsEnabled = has;
    }

    private void bt_New_Click(object sender, RoutedEventArgs e)
    {
        var p = new ConnectionProfile { Name = Loc.T("Set_NewName"), Port = 5900 };
        App.Settings.Profiles.Add(p);
        RefreshList(p);
        tb_Name.Focus();
        tb_Name.SelectAll();
    }

    private void bt_Save_Click(object sender, RoutedEventArgs e)
    {
        var p = Selected;
        if (p == null) return;
        var name = tb_Name.Text.Trim();
        if (name.Length == 0) { Warn(Loc.T("Set_NameMissing")); return; }
        if (!int.TryParse(tb_Port.Text.Trim(), out var port) || port is < 1 or > 65535) { Warn(Loc.T("Conn_PortInvalid")); return; }
        p.Name = name;
        p.Host = tb_Host.Text.Trim();
        p.Port = port;
        p.Password = pb_Password.Password;
        App.SaveSettings();
        RefreshList(p);
        tb_Status.Text = Loc.T("Set_Saved");
    }

    private void bt_Delete_Click(object sender, RoutedEventArgs e)
    {
        var p = Selected;
        if (p == null) return;
        var r = MessageBox.Show(Window.GetWindow(this)!, Loc.T("Set_DeleteConfirm", p.Name), Loc.T("Set_Delete"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        App.Settings.Profiles.Remove(p);
        if (App.Settings.LastProfile == p.Name) App.Settings.LastProfile = "";
        App.SaveSettings();
        RefreshList(null);
        if (App.Settings.Profiles.Count == 0) lb_Profiles_SelectionChanged(lb_Profiles, null!);
    }

    private void cb_LangMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cb_LangMode.SelectedItem is ComboBoxItem { Tag: string mode } && App.Settings.LangSwitchMode != mode)
        {
            App.Settings.LangSwitchMode = mode;
            App.SaveSettings();
        }
    }

    private void bt_SettingsFolder_Click(object sender, RoutedEventArgs e) => OpenSettingsFolder();

    public static void OpenSettingsFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsStore.DefaultPath)!;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, Loc.T("Err_Title"), MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Warn(string message) =>
        MessageBox.Show(Window.GetWindow(this)!, message, Loc.T("Err_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
}
