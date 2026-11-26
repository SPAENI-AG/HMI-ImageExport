using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HMIImageExport.Core;

namespace HMIImageExport.App;

/// <summary>Grosse Ansicht einer aufgenommenen Seite (Doppelklick in der Seitenliste). ◄ ► blättern, Esc schliesst.</summary>
public partial class ImageViewWindow : Window
{
    private readonly IReadOnlyList<(string Path, string Title)> _items;
    private int _index;

    private ImageViewWindow(IReadOnlyList<(string Path, string Title)> items, int index)
    {
        InitializeComponent();
        _items = items;
        _index = Math.Clamp(index, 0, items.Count - 1);
        Loc.Instance.LanguageChanged += (_, _) => Dispatcher.BeginInvoke(() => ShowCurrent());
        ShowCurrent();
    }

    public static void ShowView(Window? owner, IReadOnlyList<(string Path, string Title)> items, int index)
    {
        if (items.Count == 0) return;
        var w = new ImageViewWindow(items, index) { Owner = owner };
        w.ShowDialog();
    }

    private void ShowCurrent()
    {
        var (path, title) = _items[_index];
        Title = title;
        tb_Caption.Text = $"{title}   ({_index + 1}/{_items.Count})   {Loc.T("View_Hint")}";
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            bi.Freeze();
            img_View.Source = bi;
            tb_Missing.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or InvalidOperationException)
        {
            img_View.Source = null;
            tb_Missing.Text = Loc.T("View_Missing");
            tb_Missing.Visibility = Visibility.Visible;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Right or Key.Down or Key.PageDown:
                _index = (_index + 1) % _items.Count; ShowCurrent(); break;
            case Key.Left or Key.Up or Key.PageUp:
                _index = (_index - 1 + _items.Count) % _items.Count; ShowCurrent(); break;
            default: return;
        }
        e.Handled = true;
    }
}
