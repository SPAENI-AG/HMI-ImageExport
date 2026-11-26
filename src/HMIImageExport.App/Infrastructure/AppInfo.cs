using System.Reflection;
using System.Windows.Data;
using System.Windows.Markup;
using HMIImageExport.Core;

namespace HMIImageExport.App.Infrastructure;

public static class AppInfo
{
    public const string Title = "HMI-ImageExport";
    public const string Company = "SPÄNI Elektro-Mechanik AG";
    public const string Developer = "Ralph Kessler";
    public const string SupportUrl = "https://support.elektro-spaeni.ch";
    public const string SupportEmail = "support@elektro-spaeni.ch";

    public static string Version { get; } =
        (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0)).ToString(3);
    /// <summary>"Organisation/Repo" aus Directory.Build.props; OWNER/… = Update-Prüfung aus.</summary>
    public static string GitHubRepository { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GitHubRepository")?.Value ?? "";
    public static string VersionText => $"Version {Version}";
    public static string Copyright => $"© {DateTime.Now.Year} {Company}";
}

/// <summary>XAML: {l:Tr Schlüssel} – Text aus Core/Strings.cs, wechselt sofort mit der Sprache.</summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }
            .ProvideValue(serviceProvider);
}
