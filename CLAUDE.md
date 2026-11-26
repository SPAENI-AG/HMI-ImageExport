# CLAUDE.md – HMI-ImageExport

WPF-Tool (.NET 10) der SPÄNI Elektro-Mechanik AG: Touch-Panels per VNC anzeigen, Seiten als Screenshots speichern, Word-Dokument erstellen. Öffentliches Repository (MIT) – nach dem SPÄNI-Standard `spaeni-wpf-app` aufgebaut (Referenz: d2c-DataUpload-MQTT).

## Aufbau

- `src/HMIImageExport.Core` – Logik ohne UI und ohne NuGet-Pakete (Dateinamen, Seitenliste, Einstellungen, **eigener RFB-Client** `Vnc/RfbClient`, Update-Prüfung, Hilfe-Server, Loc/Strings)
- `src/HMIImageExport.Export` – Word-Export (DocumentFormat.OpenXml)
- `src/HMIImageExport.App` – WPF, Code-Behind, kein MVVM-Framework; `App.xaml` = alle Brushes/Styles; `Views/`, `Help/`, `Assets/`
- `tests/HMIImageExport.Tests` – Konsolen-Testlauf ohne Framework (`Test("Name", () => …)`, Exit-Code ≠ 0 bei Fehlern)

## Befehle

- Tests (auch unter Linux): `dotnet run --project tests/HMIImageExport.Tests`
- Build (Windows): `dotnet build HMI-ImageExport.slnx`
- App: `dotnet run --project src/HMIImageExport.App`

## Regeln

- Firma immer **SPÄNI Elektro-Mechanik AG** (Ausnahmen: Domain `elektro-spaeni.ch`, GitHub-Organisation `SPAENI-AG`).
- Jeder sichtbare Text über `Loc`: Schlüssel in `Core/Strings.cs` mit DE+EN, gleiche Platzhalter; XAML `{l:Tr Key}`, Code `Loc.T("Key", …)`.
- Neue Funktion → Hilfe-Abschnitt in `Help/de.html` und `en.html` (gleiche `id`), ?-Knopf mit `Tag="anker"`. Anker stehen in `HelpAnchors`.
- Farben nur als Brushes in `App.xaml`. Steuerelemente mit Präfix `tb_ bt_ cb_ chk_ rb_ mi_ dg_ tc_ ti_ bd_ hl_ lb_ pb_ ic_`.
- Passwörter nie im Klartext speichern/loggen (DPAPI, `dpapi:…`). Einstellungen: `%APPDATA%\HMIImageExport\settings.json`.
- **Keine Kundendaten** im Repo: keine echten Hosts, IPs, Passwörter, Panel-Screenshots, Namen. Beispiele: `192.0.2.x`, «Beispielanlage». `.gitignore` schliesst `settings.json`, `Screenshots/`, `Export/` aus.
- Git-Identität: Ralph Kessler <ralph.kessler@outlook.com>. Historie nicht umschreiben, kein Force-Push ohne ausdrückliche Freigabe.

## VNC-Hinweise

- Ziel: Siemens-Panels (Sm@rtServer, UltraVNC-kompatibel). Der Client fordert Hextile/CopyRect/Raw sowie Cursor-Encodings an (Mauszeiger kommt getrennt, nicht ins Bild).
- Mehrsprachig: Ein Klick auf die Sprach-Taste schaltet zur nächsten Sprache (`LangSwitchMode = cycle`); weitere Verfahren sind noch nicht umgesetzt.

## Release

`<Version>` in `Directory.Build.props` erhöhen → CHANGELOG `## [x.y.z] - Datum` → Tests → commit → push → `git tag vx.y.z` → `git push origin vx.y.z`. Push und Tag erst nach Bestätigung. Der Workflow `release.yml` baut ZIP + SHA256 und erstellt das GitHub-Release.
