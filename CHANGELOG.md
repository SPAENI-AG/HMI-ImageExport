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
