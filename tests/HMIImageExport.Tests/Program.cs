using System.Text.RegularExpressions;
using HMIImageExport.Core;

var failed = 0;
void Test(string name, Action body)
{
    try { body(); Console.WriteLine($"OK    {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FEHLER {name}: {ex.Message}"); }
}
void Check(bool ok, string msg) { if (!ok) throw new Exception(msg); }

Test("Strings: DE und EN vorhanden", () =>
{
    foreach (var (k, v) in Strings.All)
        Check(!string.IsNullOrWhiteSpace(v.De) && !string.IsNullOrWhiteSpace(v.En), $"Text fehlt für {k}");
});

Test("Strings: gleiche Platzhalter in beiden Sprachen", () =>
{
    static string[] Ph(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToArray();
    foreach (var (k, v) in Strings.All)
        Check(Ph(v.De).SequenceEqual(Ph(v.En)), $"Platzhalter unterschiedlich: {k}");
});

Test("Loc: Umschalten wirkt sofort und meldet LanguageChanged", () =>
{
    var n = 0;
    Loc.Instance.LanguageChanged += (_, _) => n++;
    Loc.Instance.Language = "de";
    Check(Loc.T("Tab_Settings") == "Einstellungen", "DE falsch");
    Loc.Instance.Language = "en";
    Check(Loc.T("Tab_Settings") == "Settings", "EN falsch");
    Check(n >= 1, "Ereignis fehlt");
    Loc.Instance.Language = "de";
});

Test("Loc: Platzhalter und unbekannter Schlüssel", () =>
{
    Loc.Instance.Language = "de";
    Check(Loc.T("About_Version", "1.2.3") == "Version 1.2.3", "Format falsch");
    Check(Loc.T("Gibt_Es_Nicht") == "[Gibt_Es_Nicht]", "Fallback falsch");
});

Console.WriteLine(failed == 0 ? "Alle Tests bestanden." : $"{failed} Test(s) fehlgeschlagen.");
return failed == 0 ? 0 : 1;
