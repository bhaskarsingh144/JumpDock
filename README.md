# JumpDock

**A top-edge Windows dock with per-app jump lists.**

Pin Chrome, Cursor, VS Code, IntelliJ, Teams, and more. Right-click a tile for pinned and recent items from that app’s own history and/or Windows destinations. No installer required for the standalone build. No telemetry. Runs locally.

![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D6?style=flat-square&logo=windows&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.5%2B-512BD4?style=flat-square)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)
![Size](https://img.shields.io/badge/Deps-None-success?style=flat-square)

---

## Screenshots

> Drop images into [`assets/`](assets/) and link them here.

---

## Features

### Dock bar

- Top-edge bar with large icons; size tracks Windows DPI × Fit (and width-fits on narrower screens)
- **More apps** overflow tile
- Drag reorder / manage pins; Settings for icon size and max visible
- Parks off-screen over maximized or full-screen windows; reveal on top-edge hover
- Pinned to all virtual desktops; hidden from Task View

### Jump lists (the point of JumpDock)

| Source | Examples |
| --- | --- |
| Windows Automatic / Custom Destinations | **Cursor**, Explorer, many Win32 apps |
| App-owned history | VS Code, IntelliJ, Notepad++, Chrome, GitHub Desktop, Compass, Lens, Termius |
| Chrome app tiles | GitHub PR, Jira, Confluence via `JumpDockLauncher` |

Pinned and recent items persist in `%LOCALAPPDATA%\JumpDock\jumplists.json`.

**Refresh** (jump menu) and **Reload** (bar menu) never wipe stored recents on an empty harvest. Cursor uses Windows destinations (`JumpSource.WindowsDest`).

### Shell chrome

Show/hide shares the same edge-park model as [QuickView](https://github.com/bhaskarsingh144/QuickView). This tree vendors `ShellChrome.cs` under `src/`.

---

## Quick start

### Standalone (no install)

```bat
scripts\setup-standalone.bat
```

Compiles to `bin\JumpDock.exe`, creates `run.bat`, and launches the dock. Default tile icons are seeded from repo `icons\` into `%LOCALAPPDATA%\JumpDock\icons\` (existing files are left alone).

### System install

Right-click `scripts\setup.bat` → **Run as administrator**.

Installs to `C:\Program Files\JumpDock`, copies bundled icons, adds Desktop and Start Menu shortcuts, and starts the app.

### Build only

```powershell
.\scripts\build.ps1
```

Output: `bin\JumpDock.exe` (+ `JumpDockLauncher.exe`)

### Tests

```powershell
.\scripts\test.ps1
```

Runs unit tests (JumpLists, JumpListStore, JumpListRefresh). Manual matrix: [docs/TEST-JumpLists.md](docs/TEST-JumpLists.md).

Primary target: **Windows 11**. Windows 10 paths are kept where already present.

---

## Config (machine-local)

| File | Purpose |
| --- | --- |
| `%LOCALAPPDATA%\JumpDock\dock.json` | Pin order and settings |
| `%LOCALAPPDATA%\JumpDock\jumplists.json` | Pinned + recent jump items |
| `%LOCALAPPDATA%\JumpDock\icons\` | Tile artwork (seeded from repo `icons\` on first run / migrate) |

First run migrates from `%LOCALAPPDATA%\WorkApps` or `C:\WorkApps` when JumpDock config is missing.

Examples: [`samples/dock.example.json`](samples/dock.example.json).

---

## Disabling auto-start

On first run JumpDock can add a Startup shortcut. To turn that off, delete:

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\JumpDock.lnk
```

---

## Uninstalling

**Installed with `setup.bat`**

```bat
C:\Program Files\JumpDock\uninstall.bat
```

**Standalone**

1. Close JumpDock  
2. Delete the repo folder  
3. Remove the Startup shortcut (path above)  
4. Optionally delete `%LOCALAPPDATA%\JumpDock`

---

## Project structure

```text
JumpDock/
├── src/              # C# sources + ShellChrome.cs
├── tests/            # JumpLists / JumpListStore / JumpListRefresh
├── scripts/          # build.ps1, test.ps1, setup*.bat
├── docs/             # Manual test matrices
├── icons/            # Default tile icons
├── samples/
├── assets/
├── bin/              # Build output
├── LICENSE
├── CONTRIBUTING.md
└── README.md
```

---

## Contributing

Bugs, fixes, and ideas welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for build steps and guidelines.

Work on a **feature branch** and open a pull request against `master` — direct pushes to `master` are blocked.

Jump list rules: never empty-`ReplaceRecents` / `ClearRecent` on failed harvest; use `JumpLists.SourceOf` for Refresh.

---

## Privacy

Everything stays on your PC. JumpDock does not collect, upload, or phone home.

---

## Troubleshooting

| Issue | What to try |
| --- | --- |
| Won't start | Confirm .NET Framework 4.5+; rebuild with `.\scripts\build.ps1` |
| Blank / missing tile icons | Confirm files under `%LOCALAPPDATA%\JumpDock\icons\`; re-run from the repo so bundled `icons\` can seed missing files |
| Tiny tiles on high-DPI / 4K | Icon size is a design unit; physical size follows Windows scale × Fit — see [docs/TEST-DisplayScale.md](docs/TEST-DisplayScale.md) |
| Empty jump list | Use **Refresh** in the jump menu; check `docs/TEST-JumpLists.md` |

---

## License

[MIT](LICENSE) © 2026 Bhaskar Singh
