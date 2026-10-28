# Manual matrix — display scaling

JumpDock treats **Icon size** / **Peek** as 96-DPI design units. Physical size is:

`px = dip × (DeviceDpi / 96) × Fit`

`Fit` (≈0.62–1.0) comes from logical screen height so 1080p softens without fighting Windows scale %.

Rebuild happens on start, `WM_DPICHANGED`, and `WM_DISPLAYCHANGE`.

Automated: `.\scripts\test.ps1` (DisplayScale + DockLayoutScale suites).

## Profiles

| Machine | Typical panel | Suggested Windows scale | Check |
| --- | --- | --- | --- |
| Current laptop | 16″ 2.8K 16:10 (~2880×1800) | 150% or 200% | Tiles track DPI; not tiny vs jump menu |
| Work monitor | 27″ 4K 16:9 (3840×2160) | ~150% | Comfortable tiles; bar not oversized |
| Office laptop | 16″ 1080p 16:10 (1920×1200) | 100% or 125% | Fit softens slightly; bar still width-fits |

## Checklist (each profile)

1. Build and run: `.\scripts\build.ps1` then `bin\JumpDock.exe`
2. Confirm dock bar icon size looks proportional to system chrome (taskbar / title bars)
3. Open jump menu — chrome Dip-scales; tiles should not look much smaller than menu chrome
4. Open Settings — **Icon size (design)** 72/84/96 still changes relative size
5. Open More apps — grid icons match dock tile size
6. Edge reveal after maximize: hover top edge still reliable (ShellChrome strips Dip-scaled)
7. Change Windows **Display → Scale** (or move to another monitor and back): dock rebuilds without restart

## Regression

| ID | Result | Notes |
| --- | --- | --- |
| R1 `.\scripts\test.ps1` | | Exit 0 including DisplayScale + DockLayoutScale |
| R2 Crowded pins on 1920-wide | | Bar width-fits / Shrunk tooltip instead of clipping |
