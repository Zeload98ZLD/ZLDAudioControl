# ZLD Audio Control 1.0.1

ZLD Audio Control verwandelt einen MIDI-/DJ-Controller in eine frei
konfigurierbare Windows-Audiosteuerung.

## Funktionen

- Windows-Masterlautstärke und Master-Mute
- MIDI-Lernmodus für den Master-Regler
- beliebige Programme als Audiokanäle hinzufügen
- Programm-Lautstärke und Mute
- Auto-Game-Kanal für das aktive Spiel
- MIDI-Lernmodus pro Programm
- Unterstützung von Hercules-Fadern mit mehreren MIDI-Signalen
- Play/Pause, vorheriger Titel und nächster Titel
- Hot-Cues als Medientasten anlernen
- Profile für Gaming, Musik und Streaming
- Dark- und Light-Modus
- Equalizer-APO-Steuerung:
  - Bass
  - Mitten
  - Höhen
  - Low-pass-/High-pass-Filter
  - MIDI-Lernmodus für EQ-Regler
- GitHub-Release-Prüfung
- automatischer Import der Einstellungen älterer HAC-Versionen
- ZLD-Logo und eigenes Windows-Programmsymbol
- Veröffentlichung als einzelne selbstständige EXE

## Visual Studio

1. `ZLDAudioControl.csproj` öffnen.
2. **Erstellen → Projektmappe bereinigen**
3. **Erstellen → Projektmappe neu erstellen**
4. Starten.

## Einzelne EXE erstellen

`build-release.cmd` doppelklicken.

Die fertige Datei liegt danach hier:

```text
bin\Release\net8.0-windows\win-x64\publish\ZLDAudioControl.exe
```

Alternativ in PowerShell:

```powershell
dotnet clean .\ZLDAudioControl.csproj -c Release
dotnet restore .\ZLDAudioControl.csproj
dotnet publish .\ZLDAudioControl.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Equalizer APO

Equalizer APO muss für das verwendete Ausgabegerät installiert sein.

In:

```text
C:\Program Files\EqualizerAPO\config\config.txt
```

folgende Zeile ergänzen:

```text
Include: hercules_dynamic.txt
```

Das Schreiben in `C:\Program Files` kann Administratorrechte benötigen.

## Updates

Im Update Center können GitHub-Inhaber und Repository-Name eingetragen werden.
Die Anwendung prüft anschließend das neueste GitHub Release und öffnet bei
Bedarf dessen Downloadseite.

## Hinweis zur bereitgestellten ZIP

Die ZIP enthält den vollständigen Quellcode und die Release-Skripte. In der
Erstellungsumgebung stand kein Windows-.NET-SDK zur Verfügung, deshalb konnte
die EXE hier nicht selbst kompiliert werden. Auf einem Windows-PC mit
Visual Studio/.NET 8 wird sie über `build-release.cmd` erzeugt.


## Patch 1.0.1

- Fehlenden `System.IO`-Import im Equalizer-Service ergänzt.
