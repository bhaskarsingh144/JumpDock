# Contributing to JumpDock

Thank you for your interest in contributing to JumpDock! This document provides guidelines for contributing to the project.

## Getting Started

### Prerequisites

- Windows 10 or Windows 11
- .NET Framework 4.5 or later (comes pre-installed with Windows)
- Basic knowledge of C# and Windows Forms
- C# 5 only in this tree (no interpolated strings, no `out var`)

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/bhaskarsingh144/JumpDock.git
   cd JumpDock
   ```

2. Run the build script:
   ```powershell
   .\scripts\build.ps1
   ```

3. The compiled executables will be in `bin\JumpDock.exe` and `bin\JumpDockLauncher.exe`

4. To run standalone (no installation):
   ```batch
   .\scripts\setup-standalone.bat
   ```

5. To install system-wide:
   ```batch
   .\scripts\setup.bat
   ```
   (Run as Administrator)

## Project Structure

```
JumpDock/
├── src/                       # C# source
│   ├── JumpDock.cs            # Entry + dock bar UI
│   ├── JumpLists.cs           # Jump source registry
│   ├── JumpListStore.cs       # Persistent pinned/recent items
│   ├── JumpListRefresh.cs     # Safe Refresh (never wipe on empty)
│   ├── JumpProviders.cs       # App-history harvest
│   ├── WindowsJumpList.cs     # Windows destinations
│   ├── ShellChrome.cs         # Edge park / virtual desktops (vendored)
│   └── ...
├── tests/                     # Unit tests (see scripts/test.ps1)
├── scripts/
│   ├── build.ps1              # Compile from source
│   ├── test.ps1               # Run unit tests
│   ├── setup.bat              # System-wide install (admin)
│   └── setup-standalone.bat   # Portable build + run.bat
├── docs/                      # Manual test matrices
├── icons/                     # Default tile artwork (seeded to LocalAppData)
├── samples/                   # Example dock.json / jumplists.json
├── assets/                    # README screenshots
├── bin/                       # Generated output
├── LICENSE
├── CONTRIBUTING.md
└── README.md
```

## Development Guidelines

### Code Style

- Use 4 spaces for indentation
- Follow C# naming conventions (PascalCase for public members, camelCase for private)
- Keep methods focused and concise
- Stay on C# 5 / .NET Framework 4.5 APIs

### Jump lists

- Registry: `JumpLists.SourceOf` → `AppHistory` | `WindowsDest` | `None`
- Refresh: `JumpListRefresh.RefreshOne` — **never** clear on empty harvest
- Cursor is `WindowsDest` (Windows AutomaticDestinations)
- Manual matrix: [docs/TEST-JumpLists.md](docs/TEST-JumpLists.md)

### Testing

Automated (no NuGet):

```powershell
.\scripts\test.ps1
```

This compiles and runs unit tests under `tests/` (JumpLists, JumpListStore, JumpListRefresh). Manual matrices live in `docs/TEST-*.md`.

Before submitting a pull request:
1. Build successfully (`.\scripts\build.ps1`)
2. Pass `.\scripts\test.ps1`
3. Run the app and verify pins / jump menus / Settings
4. For jump-list changes, walk through [docs/TEST-JumpLists.md](docs/TEST-JumpLists.md)

### Submitting Changes

`master` is protected. Do **not** push commits directly to `master`. All changes land via pull request.

1. Fork the repository (or use a branch on a clone if you are a collaborator)
2. Create a feature branch from the latest `master` (`git checkout -b feature/your-feature`)
3. Commit your changes with descriptive messages
4. Push the feature branch (`git push -u origin feature/your-feature`)
5. Open a pull request targeting `master`
6. Fill in the PR template (summary + test plan). Keep PRs focused when you can

Maintainers squash-merge into `master`. Feature branches are deleted after merge.

### Commit Message Format

Use clear, descriptive commit messages:
- `feat: add overflow reorder for More apps`
- `fix: keep jumplist recents on empty Refresh`
- `docs: update README with setup instructions`
- `chore: seed bundled icons on first run`

## Reporting Issues

When reporting bugs, please include:
- Windows version
- Screen resolution and **DPI / display scale**
- Steps to reproduce
- Expected vs actual behavior
- Whether the issue is dock chrome, pin icons, or jump lists

Do not commit personal `dock.json` / `jumplists.json` or machine-local shortcuts.

## Feature Requests

Feel free to open an issue for feature requests. Please describe:
- The use case
- Why it would be valuable
- Any implementation ideas you have

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (see LICENSE file).
