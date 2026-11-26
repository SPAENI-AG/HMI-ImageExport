# Sicherheit

## Unterstützte Versionen

Sicherheitskorrekturen erscheinen für die jeweils neueste veröffentlichte Version.

## Sicherheitslücke melden

Bitte **nicht** als öffentliches Issue melden, sondern

- über GitHub: *Security* › *Report a vulnerability* (privater Bericht), oder
- per E-Mail an support@elektro-spaeni.ch (Betreff «HMI-ImageExport Sicherheit»).

Bitte beschreiben Sie das Problem, die betroffene Version und wenn möglich die Schritte zur Reproduktion. Fügen Sie keine Zugangsdaten oder Kundendaten bei.

## Hinweise zum Betrieb

- Passwörter werden mit der Windows-DPAPI an das Benutzerkonto gebunden gespeichert (`%APPDATA%\HMIImageExport\settings.json`).
- VNC ist ein unverschlüsseltes Protokoll mit schwacher Passwort-Anmeldung (max. 8 Zeichen). Nur in vertrauenswürdigen Anlagennetzen verwenden, bei Bedarf über VPN oder SSH-Tunnel.
- Die Hilfe läuft über einen lokalen Webserver, der nur an `127.0.0.1` lauscht und ausschliesslich Dateien aus dem Hilfe-Ordner ausliefert.
- Update-Hinweise nutzen die GitHub-API; es wird nichts automatisch installiert.
