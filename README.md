# DevToolBox

Offline, cross-platform desktop toolbox for developers (Windows · macOS · Linux). Everything runs locally: no network calls, no telemetry. Pasted data never leaves your machine.

Visual design: Claude Design canvas *DevToolbox UI* (dark graphite + amber, IBM Plex Sans / JetBrains Mono) — see [ADR-006](docs/decisions/006-devtoolbox-visual-design.md). Light and dark themes; projects and namespaces are `DevToolBox.*`.

> Status: **M0–M3 implemented** — app shell, all 12 v1 tools plus App Icons, menu bar/tray icon. Still to do in M4: command palette, recent tools, per-tool settings, signed installers (unsigned ones: [docs/installers.md](docs/installers.md)); see [PROJECT_BRIEF.md](PROJECT_BRIEF.md).

**Jump to:** [Install](#install) · [Getting started](#getting-started) · [Run](#run-the-app) · [Build & publish](#build-and-publish) · [Troubleshooting](#troubleshooting)

## Install

One command installs the latest release. It downloads the installer from GitHub Releases, checks it against the
release's `SHA256SUMS.txt`, and installs it:

**macOS** (universal: Apple Silicon and Intel; asks for your password to install into `/Applications`)
```bash
curl -fsSL https://raw.githubusercontent.com/conanlk/devtoolbox/master/install.sh | bash
```

**Linux** (x64; installs the AppImage for the current user, adds a menu entry and `~/.local/bin/devtoolbox`)
```bash
curl -fsSL https://raw.githubusercontent.com/conanlk/devtoolbox/master/install.sh | bash
```

**Windows** (PowerShell; per-user install, no admin rights)
```powershell
irm https://raw.githubusercontent.com/conanlk/devtoolbox/master/install.ps1 | iex
```

The builds aren't code-signed. Installing with these commands avoids the "unverified developer" prompts that
browser downloads trigger. To install from a downloaded file instead, see
[docs/installers.md](docs/installers.md#unsigned-builds). Options (a specific version, uninstalling on Linux) are
listed at the top of [install.sh](install.sh) and [install.ps1](install.ps1).

## Home
- **Smart paste:** paste a JWT, JSON, timestamp, UUID, URL, Base64, hash… and it suggests the right tool, opening it with your content. It only analyzes what you paste into that box — the clipboard is never read on its own.
- **Favorites** (star a tool) and **category filters** (Convert · Encode · Format & test · Generate).
- **Language:** English or Tiếng Việt from the flag buttons in the header — the whole app relabels instantly and remembers the choice (first launch follows the OS language). See [ADR-007](docs/decisions/007-localization.md).

## Tools
| Category | Tool | Highlights |
|---|---|---|
| Convert | Timestamp ⇄ Date | Auto-detects s / ms / µs / ns; local, UTC, ISO 8601, RFC 2822, relative; any time zone; live "now" |
| Convert | Image ⇄ Base64 | Drag-and-drop, file picker or clipboard; raw Base64, data URI, `<img>`, CSS `url()`; Base64 → preview + save |
| Encode / Decode | Base64 / Hex | Base64, Base64URL, hex, binary; UTF-8 safe; auto-detects direction; swap |
| Convert | JWT decoder | Header/payload as JSON, exp/nbf/iat as dates, expired/valid badge, optional HS256/384/512 verification |
| Encode / Decode | HTML | Unsafe-only or all non-ASCII; named or numeric entities |
| Encode / Decode | URL | `encodeURIComponent` vs `encodeURI` semantics, form `+`; URL parser with editable query table |
| Format & test | JSON formatter | 2/4 spaces, tabs, minify, sort keys; exact line/column errors; virtualized tree; numbers never rounded; 5 MB+ off the UI thread |
| Generate | Password | CSPRNG, unbiased; passwords or EFF-wordlist passphrases; exclusions, bulk, entropy meter, in-memory session history |
| Generate | UUID | v4 / v7, bulk, case/hyphen/brace options; inspector shows version, variant, embedded time |
| Generate | Hash | MD5, SHA-1, SHA-2, SHA-3 (where the OS supports it), CRC32; HMAC; text or streamed files with progress; compare |
| Generate | Barcode | QR, Code 128, EAN-13, UPC-A, Code 39; check-digit validation; PNG/SVG export, copy image |
| Generate | App icons | One 1024×1024+ image → full iOS set (iPhone, iPad, App Store + `Contents.json`) and Android legacy/round/adaptive mipmaps + Play Store 512; pick platforms/Android extras and padding; previews under 5 launcher masks (dark/light wallpaper); output file table; project-ready ZIP |
| Format & test | Regex tester | .NET flavor, all options incl. NonBacktracking; live highlighting, groups, replace preview; 2 s timeout |

## Getting started

### 1. Prerequisites

| | Windows 10/11 | macOS 14+ | Linux (Ubuntu 22.04+ or similar) |
|---|---|---|---|
| **To run & develop** | [.NET SDK 10.0.100+](https://dotnet.microsoft.com/download/dotnet/10.0) | .NET SDK 10.0.100+ | .NET SDK 10.0.100+, plus `sudo apt install libx11-6 libice6 libsm6 libfontconfig1` |
| **To publish (Native AOT)** | Visual Studio 2022 *Build Tools* with the **"Desktop development with C++"** workload | Xcode or Command Line Tools (`xcode-select --install`) | `sudo apt install clang zlib1g-dev` |

Check the SDK:
```bash
dotnet --list-sdks
```
You need a `10.0.x` entry. The exact version is pinned in `global.json` (any newer 10.0 feature band is accepted).

Optional tools: **Python 3** only to regenerate icons (`build/icons/`). No Node.js, no database, no internet access at runtime.

### 2. Get the code
Copy or clone the repository, then open a terminal in its root folder (the one containing `DevToolBox.slnx`). The first build restores NuGet packages; after that, everything works offline.

### 3. Open in an IDE (optional)
| IDE | How |
|---|---|
| JetBrains Rider | Open `DevToolBox.slnx`, run the **DevToolBox.App** configuration. Install the *AvaloniaRider* plugin for XAML previews. |
| Visual Studio 2026 (or 2022 17.14+ with .NET 10 SDK) | Open `DevToolBox.slnx`, set **DevToolBox.App** as startup project, press F5. Install the *Avalonia for Visual Studio* extension for previews. |
| VS Code | Install *C# Dev Kit* and the *Avalonia* extension, open the folder, run/debug `src/DevToolBox.App`. |

## Run the app

```bash
dotnet run --project src/DevToolBox.App
```
- Runs a Debug build. Debug builds enable Avalonia's developer tools support (`WithDeveloperTools()` in `Program.cs`) for inspecting the UI; see the [Avalonia DevTools docs](https://docs.avaloniaui.net/) for how to attach them.
- Faster, closer to production:
  ```bash
  dotnet run --project src/DevToolBox.App -c Release
  ```
- Quick self-check that starts the app, opens every tool, prints the startup time and exits:
  ```bash
  dotnet run --project src/DevToolBox.App -- --smoke-exit
  ```

### Test and check style
```bash
dotnet test
```
```bash
dotnet format --verify-no-changes
```
`dotnet test` runs the Core unit tests and the headless UI tests (real Skia rendering, no window appears). Both commands are the gates used in CI.

### Where the app keeps its settings
Theme, favorites, sidebar state and window position are stored in one JSON file. Delete it to reset the app:

| OS | Path |
|---|---|
| Windows | `%APPDATA%\DevToolBox\settings.json` |
| macOS | `~/Library/Application Support/DevToolBox/settings.json` |
| Linux | `$XDG_CONFIG_HOME/DevToolBox/settings.json` (default `~/.config/DevToolBox/`) |

## Build and publish

### Debug / Release build (any OS)
```bash
dotnet build -c Release
```
Builds every project; this is what you need for development and for running the tests.

### Publish a standalone app (Native AOT)
Produces a self-contained, natively compiled app that starts in ~0.15 s and doesn't need .NET installed on the target machine. **Native AOT must be published on the same OS it targets** (build the Windows app on Windows, the Linux app on Linux).

| Target | Command (run on that OS) | Output |
|---|---|---|
| macOS · Apple Silicon | `dotnet publish src/DevToolBox.App -c Release -r osx-arm64 -o artifacts/publish/osx-arm64` | `artifacts/publish/osx-arm64/DevToolBox.app` (+ the bare `DevToolBox`) |
| macOS · Intel | `dotnet publish src/DevToolBox.App -c Release -r osx-x64 -o artifacts/publish/osx-x64` | `artifacts/publish/osx-x64/DevToolBox.app` (+ the bare `DevToolBox`) |
| Windows · x64 | `dotnet publish src/DevToolBox.App -c Release -r win-x64 -o artifacts/publish/win-x64` | `artifacts\publish\win-x64\DevToolBox.exe` |
| Windows · ARM64 | `dotnet publish src/DevToolBox.App -c Release -r win-arm64 -o artifacts/publish/win-arm64` | `artifacts\publish\win-arm64\DevToolBox.exe` |
| Linux · x64 | `dotnet publish src/DevToolBox.App -c Release -r linux-x64 -o artifacts/publish/linux-x64` | `artifacts/publish/linux-x64/DevToolBox` |
| Linux · ARM64 | `dotnet publish src/DevToolBox.App -c Release -r linux-arm64 -o artifacts/publish/linux-arm64` | `artifacts/publish/linux-arm64/DevToolBox` |

Example on an Apple Silicon Mac:
```bash
dotnet publish src/DevToolBox.App -c Release -r osx-arm64 -o artifacts/publish/osx-arm64
```
```bash
open artifacts/publish/osx-arm64/DevToolBox.app
```

**macOS app bundle:** a macOS publish also wraps the output in `DevToolBox.app` (Info.plist, `DevToolBox.icns`, ad-hoc signature; see `build/macos/`). Run and ship the `.app` (drag it to `/Applications`): only then do Finder, the Dock and *System Settings › General › Login Items* show the DevToolBox name and icon instead of the generic "exec" one. Skip it with `-p:CreateMacAppBundle=false`.

**What to ship:** the executable plus the native libraries next to it (`libSkiaSharp`, `libHarfBuzzSharp`, and `libAvaloniaNative` on macOS). Copy the whole folder; `*.pdb`, `*.xml` and `*.dSYM` are debug symbols and can be left out. Size: ~43 MB uncompressed, ~18 MB zipped (macOS arm64).

**Check a published build** (CI does this on all three OSes):
```bash
./artifacts/publish/osx-arm64/DevToolBox --smoke-exit
```
It should print `startup_ms=…` and `tools_ok=13/13`, then exit with code 0.

### Installers
Windows `Setup.exe`, a universal macOS `.pkg` and a Linux AppImage, each built on its own OS with one script — see [docs/installers.md](docs/installers.md):
```bash
build/installers/macos/build-pkg.sh
```

### Version and build number
`version.json` (repo root) is the only place the version lives:
```json
{ "version": "0.1.0", "build": 12 }
```
- **`version`** (major.minor.patch): edit it by hand when you release, e.g. `0.2.0`.
- **`build`**: increased by 1 by every successful `dotnet publish` and saved back to the file. A failed publish doesn't count; plain `dotnet build` / `dotnet test` use the number as is. Publishing several runtimes counts once per runtime.
- To use a fixed number without touching the file (e.g. CI): `dotnet publish … -p:BuildNumber=42`.

The app shows it in the sidebar footer as `v0.1.0 (build 12)`; the binary carries `0.1.0.12` (assembly/file version), the macOS bundle `CFBundleShortVersionString` 0.1.0 and `CFBundleVersion` 12, and `--smoke-exit` prints `version=0.1.0.12`.

### CI
`.github/workflows/ci.yml` runs on Windows, macOS and Linux: restore → format check → build → test → AOT publish → smoke test, and uploads each app as a downloadable artifact (`DevToolBox-<rid>`). It starts working once the project is pushed to GitHub.

### Installers
`.github/workflows/installers.yml` builds the Windows `Setup.exe`, macOS `.pkg` and Linux AppImage. Pushing a tag `v<version>` also creates a draft GitHub Release with a `SHA256SUMS.txt`. Publish the draft to make it available to the [install commands](#install). Builds are **unsigned**, so see the Gatekeeper/SmartScreen notes below.

## Troubleshooting

| Problem | Fix |
|---|---|
| `A compatible .NET SDK was not found` / `global.json` error | Install a .NET 10 SDK (`dotnet --list-sdks` must show `10.0.x`). |
| AOT publish: `Platform linker not found` / `clang not found` / `link.exe not found` | Install the AOT prerequisites from the table above for that OS. |
| AOT publish for another OS fails | Expected — publish each OS on that OS (or let CI build it). |
| Linux: app exits with an X11 / fontconfig error | `sudo apt install libx11-6 libice6 libsm6 libfontconfig1`. Under Wayland, XWayland must be available. |
| macOS: *"DevToolBox can't be opened because Apple cannot check it"* (a copied, unsigned build) | Right-click → **Open** once, or run `xattr -dr com.apple.quarantine <folder>`. Not needed for builds you made yourself. |
| Windows: SmartScreen *"Windows protected your PC"* | **More info → Run anyway** (the build is not code-signed yet). |
| macOS Dock shows a generic icon | Rebuild — the Dock icon is set at startup from `Assets/AppIcon-macOS.png`. |
| macOS Login Items shows *DevToolBox* with the generic "exec" icon | The login item points at the bare executable. Start `DevToolBox.app` once (it rewrites the entry to the bundle), then reopen System Settings. |
| The app looks wrong or keeps an old window size | Delete the settings file (see *Where the app keeps its settings*). |
| A test hangs or a UI test fails only on your machine | Run `dotnet test --logger "console;verbosity=normal"` to see which test; headless UI tests need no display but do need the native Skia library restored by the first build. |

## Developer guide
Architecture, conventions, adding a tool, localization, testing, versioning and packaging: **[docs/developer-guide.md](docs/developer-guide.md)**.

## Project layout
```
src/DevToolBox.Core     Pure tool logic (no UI, no network) → Result<T>, per-tool folders
src/DevToolBox.App      Avalonia app: Shell/, Platform/, Tools/, Shared/, Themes/
tests/               DevToolBox.Core.Tests (xUnit) · DevToolBox.App.Tests (Avalonia.Headless)
docs/decisions/      ADRs
build/icons/         App/tray icons, Lucide → Avalonia icon generator
build/macos/         DevToolBox.app packaging (Info.plist template, publish target)
```

## Adding a tool
1. Logic + tests: `src/DevToolBox.Core/Tools/<Name>/` and `tests/DevToolBox.Core.Tests/Tools/<Name>/`.
2. UI: `src/DevToolBox.App/Tools/<Name>/` with `<Name>Tool.cs` (a `ToolDefinition`), `<Name>View.axaml`, `<Name>ViewModel.cs`.
3. Register: add one line to `src/DevToolBox.App/Tools/ToolCatalog.cs` (name, short name, one-sentence description, category, 2–4 char badge). `ShippedToolsTests` then smoke-tests it automatically.
4. Optional: implement `IAcceptsInput` so Smart paste can open the tool with content, and add a rule to `ContentDetector`.

## App icon, Dock and menu bar
- Concept A from the design; sources and generated `.ico` / `.icns` / PNGs are in `build/icons/app/` (see its README).
- **Dock (macOS):** set at startup through the native API, so even `dotnet run` shows the DevToolBox icon instead of the generic "exec" one. Bundled builds also get it from `DevToolBox.icns`.
- **Menu bar / tray:** a DevToolBox glyph (template image on macOS, tinted for light/dark menu bars) with Open, Favorites, All tools ▸ category ▸ tool, Open at login, and Quit.

## Running in the background

- **Closing the window doesn't quit.** The app keeps running in the menu bar / tray; click the icon (or launch the app again) to bring the window back. It exits only through **Quit DevToolBox** in the tray menu, ⌘Q on macOS, or OS logout/shutdown. On macOS the Dock icon disappears while the window is closed.
- **Open at login** (tray menu checkmark, on by default in release builds) registers a per-user entry that starts the app hidden with `--background`:
  - Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `DevToolBox`
  - macOS: `~/Library/LaunchAgents/com.devtoolbox.app.plist`
  - Linux: `~/.config/autostart/devtoolbox.desktop` (honours `$XDG_CONFIG_HOME`)

  The entry is rewritten on each start so it follows the executable if you move it. Debug builds (`dotnet run`) don't enable it by default, so a `bin/` folder never ends up as a login item.
- **Single instance:** a second launch only brings the running window forward (lock file `instance.lock` in the settings folder).
- **Linux:** the tray icon needs a StatusNotifierItem host (KDE, Xfce, Cinnamon; on GNOME the *AppIndicator* extension). Without one, launch the app again to reopen the window.

## Adding icons
Source SVGs live in `build/icons/lucide/`. Add the new icon's SVG from [lucide.dev](https://lucide.dev) there, then regenerate:
```bash
python3 -I build/icons/lucide_to_axaml.py build/icons/lucide src/DevToolBox.App/Themes/Icons.axaml
```
Use it in XAML as `<controls:LucideIcon Kind="IconName" />` (PascalCase of the file name).

## Keyboard shortcuts
| Shortcut | Action |
|---|---|
| Cmd/Ctrl + K (or F) | Focus tool search |
| Cmd/Ctrl + B | Toggle sidebar |
| Enter (in search) | Open first match |
