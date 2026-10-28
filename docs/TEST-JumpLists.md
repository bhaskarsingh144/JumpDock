# Manual test — jump lists and Reload (JumpDock v1, Windows 11)

## Setup

1. Build: `scripts\build.ps1` then run `bin\JumpDock.exe` (or Startup).
2. Unit gate: `scripts\test.ps1` must pass first.

## Per-app Refresh

| App | Source | Steps | Expected |
| --- | --- | --- | --- |
| Cursor | WindowsDest | Right-click → Refresh | Recents stay or grow from Windows destinations; **never wipe to empty** if harvest fails |
| VS Code | AppHistory | Refresh | Recents from VS Code storage; pins kept |
| IntelliJ IDEA | AppHistory | Refresh | Recents from IDE; pins kept |
| Chrome | AppHistory | Refresh | Recents + hub pins; pins kept |
| File Explorer | AppHistory | Refresh | Folders/files; pins kept |
| GitHub PR / Jira / Confluence | AppHistory | Refresh | Chrome-app URLs; pins kept |
| GitHub Desktop / Compass / Lens / Termius | AppHistory | Refresh | Provider recents when installed; empty harvest keeps store |
| Edge / Teams / VPN / Company Portal | None | Open menu | Refresh glyph disabled / tip “No jump list”; store unchanged |

## Bar Reload

| # | Steps | Expected |
| --- | --- | --- |
| 1 | Note Cursor + VS Code recent counts | — |
| 2 | Right-click empty bar → Reload | Counts preserved or refreshed safely; **no wipe** |
| 3 | Edit `dock.json` order, Reload | Tile order updates; jump data intact |

## Missing app

| # | Steps | Expected |
| --- | --- | --- |
| 1 | Pin path to missing exe (or uninstall) | Dock still shows tile |
| 2 | Left-click | No crash; launch failure handled |
| 3 | Jump menu Refresh | No wipe of persisted pins/recents |

## Taskbar parity (Cursor)

| # | Steps | Expected |
| --- | --- | --- |
| 1 | Open folders in Cursor so taskbar jump list has entries | — |
| 2 | Dock Cursor → Refresh | Same folders appear when Windows destinations exist |
