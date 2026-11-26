namespace HMIImageExport.Core;

/// <summary>Alle sichtbaren Texte (DE/EN). Platzhalter {0}, {1}… müssen in beiden Sprachen gleich sein.</summary>
public static class Strings
{
    public static readonly IReadOnlyDictionary<string, (string De, string En)> All =
        new Dictionary<string, (string De, string En)>
        {
            ["Menu_File"] = ("_Datei", "_File"),
            ["Menu_Open"] = ("_Öffnen…", "_Open…"),
            ["Menu_Exit"] = ("_Beenden", "E_xit"),
            ["Menu_Edit"] = ("_Bearbeiten", "_Edit"),
            ["Menu_Settings"] = ("_Einstellungen", "_Settings"),
            ["Menu_View"] = ("_Ansicht", "_View"),
            ["Menu_Language"] = ("_Sprache", "_Language"),
            ["Menu_Help"] = ("_Hilfe", "_Help"),
            ["Menu_About"] = ("_Info", "_About"),
            ["Lang_De"] = ("Deutsch", "Deutsch"),
            ["Lang_En"] = ("English", "English"),
            ["Tab_Capture"] = ("Aufnahme", "Capture"),
            ["Tab_Settings"] = ("Einstellungen", "Settings"),
            ["Capture_Placeholder"] = ("Hier erscheint das Panel-Bild und die Seitenliste.", "The panel image and the page list will appear here."),
            ["Settings_Placeholder"] = ("Hier folgen die Verbindungsprofile und Optionen.", "Connection profiles and options will appear here."),
            ["About_Title"] = ("Info über {0}", "About {0}"),
            ["About_Version"] = ("Version {0}", "Version {0}"),
            ["About_Developer"] = ("Entwickler: {0}", "Developer: {0}"),
            ["About_Support"] = ("Support:", "Support:"),
            ["About_License"] = ("Lizenz: MIT", "License: MIT"),
            ["Common_Close"] = ("Schliessen", "Close"),
        };
}
