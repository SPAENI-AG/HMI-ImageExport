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
