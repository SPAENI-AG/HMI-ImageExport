using System.Windows;

namespace HMIImageExport.App;

/// <summary>Kleiner Eingabedialog (Seitenname).</summary>
public partial class InputWindow : Window
{
    public string? Result { get; private set; }

    private InputWindow(string title, string prompt, string initial)
    {
        InitializeComponent();
        Title = title;
        tb_Prompt.Text = prompt;
        tb_Input.Text = initial;
        Loaded += (_, _) => { tb_Input.Focus(); tb_Input.SelectAll(); };
    }

    /// <summary>Liefert den eingegebenen Text oder null bei Abbruch.</summary>
    public static string? Ask(Window? owner, string title, string prompt, string initial)
    {
        var w = new InputWindow(title, prompt, initial) { Owner = owner };
        return w.ShowDialog() == true ? w.Result : null;
    }

    private void bt_Ok_Click(object sender, RoutedEventArgs e)
    {
        var t = tb_Input.Text.Trim();
        if (t.Length == 0) return;
        Result = t;
        DialogResult = true;
    }
}
