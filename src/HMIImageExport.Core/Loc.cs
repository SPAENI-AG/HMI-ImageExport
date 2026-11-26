using System.ComponentModel;
using System.Globalization;

namespace HMIImageExport.Core;

/// <summary>Sprachumschaltung DE/EN. Wirkt sofort über Bindings ({l:Tr Key}) und das Ereignis LanguageChanged.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private string _language = "de";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public string Language
    {
        get => _language;
        set
        {
            var v = value == "en" ? "en" : "de";
            if (v == _language) return;
            _language = v;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string this[string key] => Lookup(key);

    public static string T(string key, params object[] args)
    {
        var s = Instance.Lookup(key);
        return args.Length == 0 ? s : string.Format(CultureInfo.CurrentCulture, s, args);
    }

    private string Lookup(string key)
    {
        if (!Strings.All.TryGetValue(key, out var v)) return $"[{key}]";
        return _language == "en" ? v.En : v.De;
    }
}
