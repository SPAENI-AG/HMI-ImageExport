using System.Windows;
using HMIImageExport.Core;

namespace HMIImageExport.App;

/// <summary>Optionen für den Word-Export (Anlage, Projektnummer, Beschreibungszeile, Deckblatt, Format).</summary>
public partial class WordExportWindow : Window
{
    private WordExportOptions? _result;

    private WordExportWindow(WordExportOptions o)
    {
        InitializeComponent();
        tb_Anlage.Text = o.Anlage;
        tb_Project.Text = o.ProjectNumber;
        chk_Caption.IsChecked = o.IncludeCaption;
        chk_Cover.IsChecked = o.IncludeCoverPage;
        chk_Landscape.IsChecked = o.Landscape;
    }

    /// <summary>Liefert die Optionen oder null bei Abbruch.</summary>
    public static WordExportOptions? Ask(Window? owner, WordExportOptions current)
    {
        var w = new WordExportWindow(current) { Owner = owner };
        return w.ShowDialog() == true ? w._result : null;
    }

    private void bt_Ok_Click(object sender, RoutedEventArgs e)
    {
        _result = new WordExportOptions
        {
            Anlage = tb_Anlage.Text.Trim(),
            ProjectNumber = tb_Project.Text.Trim(),
            IncludeCaption = chk_Caption.IsChecked == true,
            IncludeCoverPage = chk_Cover.IsChecked == true,
            Landscape = chk_Landscape.IsChecked == true,
        };
        DialogResult = true;
    }
}
