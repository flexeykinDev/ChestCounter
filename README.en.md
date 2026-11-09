[Русский](README.md) · **English**

# Fortnite Chest Counter

Counts the chests you open in Fortnite by reading your replay files. Shows an overlay on top of the game and an HTML report with stats.

This is a fork of [xdoofy92/ChestCounter](https://github.com/xdoofy92/ChestCounter) with a Russian and English interface and no internet access.

<img src="docs/main.png" alt="Main window" width="600">

<img src="docs/overlay.png" alt="Overlay: between matches, in a match, live" width="680">

## Changes in this fork

- Russian and English interface. Russian by default, English when Windows is in English. Switch it in the settings (RU / EN) or with `--lang ru|en`.
- No auto-update. The original checked GitHub and could download and replace its own exe. This version makes no network connections at all.
- New look: dark pixel interface, dot-matrix figures, one accent colour. Window, overlay, report and OBS page share the same style.
- The OBS page (`http://localhost:7373/`) is off by default. Turn it on in the settings; it only listens on 127.0.0.1.

<img src="docs/settings.png" alt="Settings" width="600">

## How it works

Fortnite saves a replay of each match in `%LOCALAPPDATA%\FortniteGame\Saved\Demos`. The tool reads these files (read-only) after the match. It does not touch the game process or game files and needs no Epic account.

Replays must be on in Fortnite: Settings → Game → Replays.

Data is stored in `%APPDATA%\ChestCounter`. The Report button opens the stats in your browser:

<img src="docs/report.png" alt="Report" width="700">

## Building

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and run `build.bat`. The exe ends up in `dist\ChestCounter.exe`.

## Shortcuts

| Keys | Action |
|---|---|
| Ctrl+Shift+O | show / hide the overlay |
| Ctrl+Shift+L | lock / unlock the overlay (locked = click-through) |

To see the overlay in game, use windowed fullscreen in Fortnite.

## License

MIT, like the original. Replay parsing is based on [FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor) (MIT), changes listed in [lib/PATCHES.md](lib/PATCHES.md).
