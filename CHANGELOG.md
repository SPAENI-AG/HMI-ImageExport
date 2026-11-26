# Changelog

## [Unveröffentlicht]

## [0.1.0] - 2026-11-26

Erste Testversion (Beta).

### Hinzugefügt
- VNC-Verbindung zu Touch-Panels (Host, Port, Passwort) mit eigenem RFB-Client (Raw, CopyRect, Hextile, VNC-Passwort), getestet mit Siemens-Panels. Panel-Bild im Fenster, Mausklick wird als Touch gesendet, Mauszeiger erscheint nicht im Screenshot.
- Seite speichern mit `F9`: Bild sofort als PNG im Anlagenordner, z. B. `01_Start_2026-11-26_1432.png`.
- Seitenliste mit Vorschau, Umbenennen, Neu aufnehmen, Entfernen, Sortieren (Drag & Drop, Pfeiltasten), Doppelklick zeigt das Bild gross, verschiebbare Breite.
- Mehrsprachige Panels: Sprach-Taste festlegen, pro Seite alle oder ausgewählte Sprachen automatisch durchschalten und aufnehmen.
- Word-Export ohne Word-Installation: ein Bild pro Seite auf Seitenbreite, A4 quer oder hoch, optional Beschreibungszeile und Deckblatt.
- Verbindungsprofile; Passwörter verschlüsselt per Windows-DPAPI (`%APPDATA%\HMIImageExport\settings.json`).
- Oberfläche und Hilfe auf Deutsch und Englisch (`F1`, lokal im Browser), Umschalten über Ansicht › Sprache.
- Update-Hinweis über GitHub Releases (Banner, «Nach Updates suchen»), kein automatisches Installieren.
- Portable ZIP für Windows x64 (self-contained, Einzeldatei, ohne Installation), derzeit nicht digital signiert.
