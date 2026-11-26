# Konzept: HMI-ImageExport (VNC-Panel-Screenshot-Tool)

Stand: 2026-11-26 · Entwurf zur Freigabe

## 1. Zweck

Touch-Panels (HMI) an Anlagen per VNC anzeigen, Seiten manuell durchblättern, Screenshots speichern und daraus ein Word-Dokument erstellen (Anlagendokumentation).

## 2. Ablauf (Benutzersicht)

1. Verbindung: Host, Port, Passwort eingeben → «Verbinden».
2. Panel-Bild erscheint im Fenster; Mausklick im Bild = Touch auf dem Panel.
3. `F9` oder «Seite speichern» → Name der Seite abfragen → PNG wird sofort gespeichert.
4. Liste der Seiten: Vorschau, Umbenennen, Entfernen, Neu aufnehmen, Reihenfolge ändern (Drag & Drop, Alt+↑/↓).
5. «Word erstellen» → Speicherort wählen → `.docx` mit allen Bildern der Liste.

## 3. Architektur (SPÄNI-WPF-Standard, .NET 10)

```
HMI-ImageExport.slnx
Directory.Build.props
src/HMIImageExport.Core    net10.0, ohne NuGet, ohne UI, Linux-testbar
src/HMIImageExport.App     net10.0-windows, WPF, Code-Behind
tests/HMIImageExport.Tests Konsolen-Testlauf
docs/  .github/workflows/
```

**Core** (testbar, keine UI-Abhängigkeit)
| Baustein | Aufgabe |
|---|---|
| `AppSettings`, `SecretStore`-Schnittstelle | Einstellungen JSON, Passwort geschützt |
| `PageEntry` / `PageList` | Seitenliste, Reihenfolge, Umbenennen, Entfernen |
| `FileNaming` | `01_Start_2026-11-26_1432.png`, ungültige Zeichen bereinigen, Kollisionen |
| `ProjectFolder` | Anlagenordner, Laden/Speichern der Liste (`pages.json`) |
| `WordExportOptions` | Beschreibungszeile ja/nein, Deckblatt ja/nein, Vorlage Dateiname |
| `Loc`/`Strings` | DE/EN |
| `UpdateChecker` | GitHub Releases |

**App**
| Baustein | Aufgabe |
|---|---|
| `VncSession` (Infrastructure) | Kapselt MarcusW.VncClient: Connect, Framebuffer, Maus-Events |
| `PanelView` | `Image` + `WriteableBitmap`, Koordinaten-Umrechnung Fenster → Panel |
| `PagesView` | Liste mit Vorschau, Drag & Drop |
| `WordExporter` | DocumentFormat.OpenXml (ohne Word) |
| `SecretStore` (DPAPI) | Passwort-Verschlüsselung |
| Fenster | MainWindow, AboutWindow, Hilfe (lokaler HelpServer) |

Tabs: **Aufnahme** (Panel + Seitenliste) und **Einstellungen**.

## 4. Technische Entscheidungen

- **VNC:** Eigener, schlanker RFB-Client im Core (`Vnc/RfbClient`), statt MarcusW.VncClient (Alpha-Stand 2021, nur .NET Core 3.1, keine WPF-Anzeige). Zielpanels: Siemens TP700 Comfort und Unified Panel (Sm@rtServer). Umfang: RFB 3.3/3.7/3.8, Anmeldung None und VNC-Passwort (max. 8 Zeichen), Encodings Raw, CopyRect, Hextile, DesktopSize. Austauschbar über `IVncSession`. Mit simuliertem Server testbar; Verhalten am echten Panel (Sm@rtServer aktivieren, Passwort, Port, max. Verbindungen) ist noch zu prüfen.
- **Anzeige:** WriteableBitmap, Skalierung «Uniform»; Klickkoordinaten werden auf Panel-Auflösung zurückgerechnet. Pro Klick Down+Up (Touch-Tap); optional Verzögerung einstellbar.
- **Screenshot:** aktueller Framebuffer → PNG (Originalauflösung des Panels, ohne Skalierung).
- **Speicherung:** PNG im Anlagenordner; `pages.json` hält Reihenfolge, Namen, Zeitstempel.
- **Word:** Bilder auf Seitenbreite (Inhaltsbreite A4 hochkant bzw. querformat wählbar), Seitenverhältnis erhalten; optional Beschriftung «Seitenname · Anlage · Datum»; eingebettete Kopien. Standardname `Anlage1_Touchpanel_2026-11-26.docx`.
- **Passwörter:** DPAPI in `%APPDATA%\HMIImageExport\settings.json` (`dpapi:…`), nie im Klartext/Log. Hinweis: VNC-Passwort ist protokollbedingt max. 8 Zeichen, DES-Challenge – Verbindung nur im Anlagennetz.
- **Verbindungsprofile:** mehrere Panels (Name, Host, Port, Passwort) in den Einstellungen.

## 5. Repository

- Öffentlich unter `spaeni-ag/hmi-imageexport`, MIT (gemäss SPÄNI-Standard, Freigabe Geschäftsleitung nötig).
- `.gitignore`: `bin/ obj/ *.user settings.json Screenshots/ Export/`.
- Keine Hosts, Passwörter, Kundenbilder im Repo; Beispielkonfiguration `docs/settings.example.json` mit Platzhaltern.
- README, LICENSE, CHANGELOG, SECURITY, CONTRIBUTING, CLAUDE.md, CI + Release-Workflow (portable ZIP win-x64).

## 6. Umsetzungsschritte

1. Gerüst: Solution, `Directory.Build.props`, App.xaml (Brushes/Styles), MainWindow, Footer, Info, Loc.
2. Core: Dateinamen, Seitenliste, Einstellungen + Tests.
3. VNC-Spike: Verbinden, Bild anzeigen, Klick senden (gegen Test-VNC-Server, dann echtes Panel).
4. Screenshot + Seitenliste (Umbenennen, Entfernen, Neu aufnehmen, Sortierung).
5. Word-Export.
6. Einstellungen, DPAPI, Hilfe DE/EN, Update-Banner.
7. CI/Release, `/check-public`, Veröffentlichung.

## 7. Entscheidungen (Vorschlag, bitte bestätigen)

| Punkt | Vorschlag |
|---|---|
| Repo | Öffentlich unter `spaeni-ag` (SPÄNI-Standard); Kundenbilder/Zugangsdaten sind per `.gitignore` ausgeschlossen |
| Deckblatt | Optional, in den Export-Optionen (Anlage, Datum, Projektnummer), Standard: aus |
| Beschreibungszeile | Optional pro Export, Standard: an |
| Panel-Hersteller | Offen → VNC-Spike (Schritt 3) klärt Maus-/Touch-Verhalten und Encodings |
| Projektnummer | Freitextfeld pro Anlage |

## 8. Nicht im Umfang

Automatisches Durchblättern, Aufzeichnen von Videos, Installer, Interop.Word.
