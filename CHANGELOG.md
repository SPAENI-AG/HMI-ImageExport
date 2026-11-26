# Changelog

## [Unveröffentlicht]

### Hinzugefügt
- Projektgerüst (Core, App, Tests), SPÄNI-Optik, Hauptfenster mit Menü und Footer, Info-Fenster, DE/EN-Umschaltung.
- Konzept in `docs/konzept.md`.
- Core: Dateinamen (`FileNaming`), Seitenliste mit Sortierung (`PageList`), Anlagenordner mit sofortigem Speichern (`ProjectFolder`, `pages.json`), Einstellungen mit geschützten Passwörtern und Migration alter Klartext-Dateien (`SettingsStore`), Tests dazu.
- Core: `PanelGeometry` (Fensterkoordinaten → Panel-Pixel) und Schnittstelle `IVncSession`.
- Core: eigener RFB-Client (`RfbClient`, `VncAuth`) für Siemens-Panels (Raw, CopyRect, Hextile, VNC-Passwort) mit Tests gegen simulierten Server.
- App: Verbinden-Dialog, Panel-Anzeige mit Klick als Tipp, Seite speichern (F9), Seitenliste mit Vorschau, Umbenennen, Neu aufnehmen, Entfernen, Sortierung per Drag & Drop und Alt+↑/↓.
- Core: RfbClient liefert deckende Pixel (Alpha 255).
- RfbClient: Pseudo-Encodings Cursor (-239), XCursor (-240), PointerPos, LastRect werden überlesen (Siemens-Panel sendet den Mauszeiger ungefragt).
- Word-Export (DocumentFormat.OpenXml, ohne Word-Installation): ein Bild pro Seite auf Seitenbreite, A4 quer oder hoch, optionale Beschreibungszeile und Deckblatt, Speicherort-Dialog mit Standardname; neues Projekt `HMIImageExport.Export`.
- Einstellungen: Verbindungsprofile mit Passwort per DPAPI (%APPDATA%\HMIImageExport), Profilauswahl, letzter Anlagenordner und Word-Optionen werden gemerkt.
- Hilfe (DE/EN) über lokalen Webserver, ?-Knöpfe, F1, Menü Hilfe (Schnellstart, Fehlersuche, Installation, Einstellungsordner).
- Update-Hinweis über GitHub Releases mit Banner (Herunterladen, Versionshinweise, Überspringen), Menü «Nach Updates suchen».
- Mehrsprachige Aufnahme: Sprachen als Kürzel eingeben, Sprach-Taste im Panel-Bild festlegen, pro Seite werden alle Sprachen automatisch durchgeschaltet und gespeichert (Dateiname mit Kürzel, Gruppe, Word mit Sprache in der Beschriftung). Einstellung «Sprachwechsel am Panel» (bisher: Klick = nächste Sprache).
- RfbClient: TapAsync, UpdateCount, WaitForQuietAsync.
- Mauszeiger wird nicht mehr ins Bild gezeichnet (Cursor-Encodings angefordert); Option «Mauszeiger vor der Aufnahme an den Bildrand schieben».
- Mehrsprachig: beliebige Auswahl der aufzunehmenden Sprachen.
- Seitenliste: Breite per Griff verschiebbar (wird gemerkt); Doppelklick zeigt das Bild gross (◄ ► blättern, Esc).
