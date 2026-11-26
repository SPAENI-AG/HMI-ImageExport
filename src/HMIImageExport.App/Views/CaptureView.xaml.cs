using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HMIImageExport.Core;
using HMIImageExport.Core.Vnc;
using HMIImageExport.Export;
using Microsoft.Win32;

namespace HMIImageExport.App.Views;

/// <summary>Zeile der Seitenliste (Anzeige).</summary>
public sealed class PageItem
{
    public required PageEntry Entry { get; init; }
    public string Name => Entry.Name;
    public string Info { get; init; } = "";
    public ImageSource? Thumbnail { get; init; }
}

public partial class CaptureView : UserControl
{
    private RfbClient? _session;
    private WriteableBitmap? _bmp;
    private ProjectFolder? _folder;
    private readonly ObservableCollection<PageItem> _items = new();
    private readonly Dictionary<string, ImageSource?> _thumbs = new();
    private int _renderQueued;
    private string _statusKey = "Status_Disconnected";
    private object[] _statusArgs = Array.Empty<object>();
    private Point _dragStart;
    private PageItem? _dragItem;
    private WordExportOptions _word = new();

    public CaptureView()
    {
        InitializeComponent();
        lb_Pages.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => tb_PagesEmpty.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Loc.Instance.LanguageChanged += (_, _) => Dispatcher.BeginInvoke(() => { ApplyStatus(); UpdateFolderText(); Rebuild(SelectedId()); });
        ApplyStatus();
        UpdateFolderText();
        UpdateButtons();
    }

    private Window? Owner => Window.GetWindow(this);

    // ---------- Verbindung ----------

    private async void bt_Connect_Click(object sender, RoutedEventArgs e)
    {
        var host = tb_Host.Text.Trim();
        if (host.Length == 0) { ShowError(Loc.T("Conn_HostMissing")); return; }
        if (!int.TryParse(tb_Port.Text.Trim(), out var port) || port is < 1 or > 65535) { ShowError(Loc.T("Conn_PortInvalid")); return; }

        bt_Connect.IsEnabled = false;
        SetStatus("Status_Connecting");
        var session = new RfbClient();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await session.ConnectAsync(host, port, pb_Password.Password, cts.Token);
        }
        catch (OperationCanceledException)
        {
            await session.DisposeAsync();
            SetStatus("Status_Disconnected");
            ShowError(Loc.T("Conn_Timeout"));
            bt_Connect.IsEnabled = true;
            return;
        }
        catch (Exception ex)
        {
            await session.DisposeAsync();
            SetStatus("Status_Disconnected");
            ShowError(ex.Message);
            bt_Connect.IsEnabled = true;
            return;
        }

        _session = session;
        session.FrameUpdated += OnFrameUpdated;
        session.Disconnected += OnDisconnected;
        var snap = session.GetSnapshot();
        SetStatus("Status_Connected", host, snap?.Width ?? 0, snap?.Height ?? 0);
        bt_Connect.IsEnabled = true;
        UpdateButtons();
        Render();
    }

    private async void bt_Disconnect_Click(object sender, RoutedEventArgs e) => await DisconnectAsync(null);

    private async Task DisconnectAsync(string? error)
    {
        var s = _session;
        _session = null;
        if (s != null)
        {
            s.FrameUpdated -= OnFrameUpdated;
            s.Disconnected -= OnDisconnected;
            await s.DisposeAsync();
        }
        if (error != null) SetStatus("Status_Lost", error); else SetStatus("Status_Disconnected");
        UpdateButtons();
    }

    private void OnDisconnected(object? sender, string? error)
    {
        if (error == null) return;   // gewollte Trennung wird in DisconnectAsync behandelt
        Dispatcher.BeginInvoke(async () =>
        {
            if (!ReferenceEquals(sender, _session)) return;
            await DisconnectAsync(error);
        });
    }

    /// <summary>Beim Schliessen des Fensters.</summary>
    public void Shutdown()
    {
        var s = _session;
        _session = null;
        if (s != null) Task.Run(() => s.DisposeAsync().AsTask()).Wait(1500);
    }

    // ---------- Anzeige und Klick ----------

    private void OnFrameUpdated(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _renderQueued, 1) == 1) return;   // Bilder zusammenfassen
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            Interlocked.Exchange(ref _renderQueued, 0);
            Render();
        });
    }

    private void Render()
    {
        var f = _session?.GetSnapshot();
        if (f == null) return;
        if (_bmp == null || _bmp.PixelWidth != f.Width || _bmp.PixelHeight != f.Height)
        {
            _bmp = new WriteableBitmap(f.Width, f.Height, 96, 96, PixelFormats.Bgr32, null);
            img_Panel.Source = _bmp;
        }
        _bmp.WritePixels(new Int32Rect(0, 0, f.Width, f.Height), f.Bgra, f.Width * 4, 0);
        tb_PanelHint.Visibility = Visibility.Collapsed;
    }

    private void img_Panel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session is not { IsConnected: true } || _bmp == null) return;
        var p = e.GetPosition(img_Panel);
        if (PanelGeometry.TryMapToPanel(p.X, p.Y, img_Panel.ActualWidth, img_Panel.ActualHeight,
                _bmp.PixelWidth, _bmp.PixelHeight, out var x, out var y))
            _session.Tap(x, y);
        e.Handled = true;
    }

    // ---------- Anlagenordner ----------

    private void bt_Folder_Click(object sender, RoutedEventArgs e) => ChooseFolder();

    public bool ChooseFolder()
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("Folder_ChooseTitle") };
        if (dlg.ShowDialog(Owner) != true) return false;
        try
        {
            _folder = ProjectFolder.Open(dlg.FolderName);
            UpdateFolderText();
            Rebuild(null);
            return true;
        }
        catch (Exception ex) { ShowError(ex.Message); return false; }
    }

    private void UpdateFolderText() =>
        tb_Folder.Text = _folder == null ? Loc.T("Folder_None") : _folder.Path;

    // ---------- Seiten ----------

    private void bt_Save_Click(object sender, RoutedEventArgs e) => CaptureCurrentPage();

    public void CaptureCurrentPage()
    {
        var snap = _session is { IsConnected: true } ? _session.GetSnapshot() : null;
        if (snap == null) { ShowError(Loc.T("Pages_NeedConnection")); return; }   // Bild jetzt sichern, bevor der Dialog öffnet
        if (_folder == null && !ChooseFolder()) return;

        var name = InputWindow.Ask(Owner, Loc.T("Pages_Save"), Loc.T("Input_PageName"),
            Loc.T("Pages_Default", _folder!.Pages.NextNumber()));
        if (name == null) return;
        try
        {
            var entry = _folder.AddPage(name, EncodePng(snap), DateTime.Now);
            Rebuild(entry.Id);
            SetStatus("Status_Saved", entry.FileName);
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void bt_Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_folder == null || lb_Pages.SelectedItem is not PageItem item) return;
        var name = InputWindow.Ask(Owner, Loc.T("Input_RenameTitle"), Loc.T("Input_PageName"), item.Entry.Name);
        if (name == null) return;
        try { _folder.RenamePage(item.Entry, name); Rebuild(item.Entry.Id); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void bt_Retake_Click(object sender, RoutedEventArgs e)
    {
        if (_folder == null || lb_Pages.SelectedItem is not PageItem item) return;
        var snap = _session is { IsConnected: true } ? _session.GetSnapshot() : null;
        if (snap == null) { ShowError(Loc.T("Pages_NeedConnection")); return; }
        try { _folder.ReplacePage(item.Entry, EncodePng(snap), DateTime.Now); Rebuild(item.Entry.Id); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void bt_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_folder == null || lb_Pages.SelectedItem is not PageItem item) return;
        var r = MessageBox.Show(Owner!, Loc.T("Pages_RemoveConfirm", item.Entry.Name), Loc.T("Pages_Remove"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try { _folder.RemovePage(item.Entry, deleteFile: true); Rebuild(null); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void bt_Up_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void bt_Down_Click(object sender, RoutedEventArgs e) => MoveSelected(+1);

    private void MoveSelected(int delta)
    {
        if (_folder == null || lb_Pages.SelectedItem is not PageItem item) return;
        var i = _folder.Pages.IndexOf(item.Entry.Id);
        if (!_folder.Pages.Move(i, i + delta)) return;
        _folder.Save();
        Rebuild(item.Entry.Id);
        lb_Pages.Focus();
    }

    private Guid? SelectedId() => (lb_Pages.SelectedItem as PageItem)?.Entry.Id;

    private void Rebuild(Guid? select)
    {
        _items.Clear();
        if (_folder != null)
        {
            foreach (var p in _folder.Pages.Items)
            {
                var missing = !_folder.FileExists(p);
                _items.Add(new PageItem
                {
                    Entry = p,
                    Info = missing ? Loc.T("Pages_FileMissing") : $"{p.CapturedAt:yyyy-MM-dd HH:mm} · {p.FileName}",
                    Thumbnail = missing ? null : Thumb(_folder.FullPath(p)),
                });
            }
        }
        if (select != null) lb_Pages.SelectedItem = _items.FirstOrDefault(i => i.Entry.Id == select);
        UpdateButtons();
    }

    private ImageSource? Thumb(string path)
    {
        if (_thumbs.TryGetValue(path, out var cached) && cached != null) return cached;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri(path);
            bi.DecodePixelWidth = 192;
            bi.EndInit();
            bi.Freeze();
            _thumbs[path] = bi;
            return bi;
        }
        catch (Exception) { return null; }
    }

    private static byte[] EncodePng(FrameSnapshot f)
    {
        var src = BitmapSource.Create(f.Width, f.Height, 96, 96, PixelFormats.Bgr32, null, f.Bgra, f.Width * 4);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private async void bt_Word_Click(object sender, RoutedEventArgs e)
    {
        if (_folder == null || _folder.Pages.Items.Count == 0) { ShowError(Loc.T("Word_NoPages")); return; }
        _word.Anlage = _folder.Pages.Anlage;
        _word.ProjectNumber = _folder.Pages.ProjectNumber;
        var o = WordExportWindow.Ask(Owner, _word);
        if (o == null) return;
        _word = o;
        _folder.Pages.Anlage = o.Anlage;
        _folder.Pages.ProjectNumber = o.ProjectNumber;
        _folder.Save();

        var dlg = new SaveFileDialog
        {
            Title = Loc.T("Word_SaveTitle"),
            Filter = Loc.T("Word_Filter"),
            DefaultExt = ".docx",
            InitialDirectory = _folder.Path,
            FileName = FileNaming.DefaultWordFileName(o.Anlage, DateTime.Today),
            OverwritePrompt = true,
        };
        if (dlg.ShowDialog(Owner) != true) return;

        o.CoverTitle = Loc.T("Cover_Title");
        o.CoverLines = new List<string> { Loc.T("Cover_Anlage", o.Anlage), Loc.T("Cover_Date", DateTime.Today.ToString("dd.MM.yyyy")) };
        if (!string.IsNullOrWhiteSpace(o.ProjectNumber)) o.CoverLines.Add(Loc.T("Cover_Project", o.ProjectNumber));

        var folder = _folder;
        var images = folder.Pages.Items.Select(p => new WordImage(folder.FullPath(p), p.Name, p.CapturedAt)).ToList();
        bt_Word.IsEnabled = false;
        try
        {
            var res = await Task.Run(() => WordExporter.Export(dlg.FileName, images, o));
            if (res.MissingFiles.Count > 0) ShowError(Loc.T("Word_Missing", string.Join("\n", res.MissingFiles)));
            SetStatus("Word_Done", Path.GetFileName(dlg.FileName));
            if (MessageBox.Show(Owner!, Loc.T("Word_OpenQuestion", dlg.FileName), Loc.T("Word_DialogTitle"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { UpdateButtons(); }
    }

    private void lb_Pages_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    // ---------- Drag & Drop und Tasten ----------

    private static DependencyObject? ParentOf(DependencyObject d)
    {
        if (d is ContentElement ce) return ContentOperations.GetParent(ce);
        if (d is Visual || d is System.Windows.Media.Media3D.Visual3D) return VisualTreeHelper.GetParent(d);
        return LogicalTreeHelper.GetParent(d);
    }

    private static PageItem? ItemAt(ListBox lb, object? source)
    {
        var d = source as DependencyObject;
        while (d != null && d is not ListBoxItem) d = ParentOf(d);
        return d is ListBoxItem lbi ? lb.ItemContainerGenerator.ItemFromContainer(lbi) as PageItem : null;
    }

    private void lb_Pages_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = ItemAt(lb_Pages, e.OriginalSource);
    }

    private void lb_Pages_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null) return;
        var d = e.GetPosition(null) - _dragStart;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(lb_Pages, item, DragDropEffects.Move);
    }

    private void lb_Pages_Drop(object sender, DragEventArgs e)
    {
        if (_folder == null || e.Data.GetData(typeof(PageItem)) is not PageItem src) return;
        var target = ItemAt(lb_Pages, e.OriginalSource);
        int from = _folder.Pages.IndexOf(src.Entry.Id);
        int to = target != null ? _folder.Pages.IndexOf(target.Entry.Id) : _folder.Pages.Items.Count - 1;
        if (!_folder.Pages.Move(from, to)) return;
        _folder.Save();
        Rebuild(src.Entry.Id);
    }

    /// <summary>F9 = Seite speichern, Ctrl+O = Ordner, Alt+↑/↓ = Seite verschieben. true = verarbeitet.</summary>
    public bool HandleKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F9) { CaptureCurrentPage(); return true; }
        if (key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { ChooseFolder(); return true; }
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down && lb_Pages.IsKeyboardFocusWithin)
        {
            MoveSelected(key == Key.Up ? -1 : +1);
            return true;
        }
        return false;
    }

    // ---------- Hilfen ----------

    private void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        ApplyStatus();
    }

    private void ApplyStatus() => tb_Status.Text = Loc.T(_statusKey, _statusArgs);

    private void UpdateButtons()
    {
        var connected = _session is { IsConnected: true };
        bt_Connect.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        bt_Disconnect.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        tb_Host.IsEnabled = tb_Port.IsEnabled = pb_Password.IsEnabled = !connected;
        if (!connected) tb_PanelHint.Visibility = Visibility.Visible;

        var sel = lb_Pages.SelectedItem as PageItem;
        var idx = lb_Pages.SelectedIndex;
        bt_Save.IsEnabled = connected;
        bt_Word.IsEnabled = _items.Count > 0;
        bt_Rename.IsEnabled = sel != null;
        bt_Remove.IsEnabled = sel != null;
        bt_Retake.IsEnabled = sel != null && connected;
        bt_Up.IsEnabled = sel != null && idx > 0;
        bt_Down.IsEnabled = sel != null && idx < _items.Count - 1;
    }

    private void ShowError(string message) =>
        MessageBox.Show(Owner!, message, Loc.T("Err_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
}
