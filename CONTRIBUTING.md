# Mitarbeit

Danke für Ihr Interesse an HMI-ImageExport.

## Vorgehen

1. Issue eröffnen (Fehler, Wunsch) oder bestehendes Issue kommentieren.
2. Fork/Branch, Änderung klein und fokussiert halten, Pull Request öffnen.
3. Vor dem Pull Request: `dotnet run --project tests/HMIImageExport.Tests` muss ohne Fehler laufen.

## Regeln

- **Keine Kundendaten:** keine echten Hostnamen, IP-Adressen, Passwörter, Panel-Screenshots, Firmen- oder Kundennamen in Code, Tests, Beispielen, Hilfe, Screenshots und Commits. Beispiele nutzen Platzhalter (`192.0.2.x`, «Beispielanlage»).
- **Texte:** jeder sichtbare Text steht in `src/HMIImageExport.Core/Strings.cs` mit Deutsch und Englisch (gleiche Platzhalter). Ein Test prüft das.
- **Hilfe:** neue Funktion = neuer Abschnitt in `Help/de.html` und `Help/en.html` mit gleicher `id`.
- **Logik** gehört nach `HMIImageExport.Core` (ohne UI, ohne NuGet-Pakete) und bekommt einen Test; die WPF-App enthält nur Oberfläche.
- **Optik:** Farben nur als Brushes in `App.xaml`, keine Hex-Werte in Views.
- **Firma:** immer «SPÄNI Elektro-Mechanik AG».
- **CHANGELOG:** Änderungen unter «Unveröffentlicht» eintragen.

Mit einem Beitrag stimmen Sie zu, dass er unter der [MIT-Lizenz](LICENSE) veröffentlicht wird.
