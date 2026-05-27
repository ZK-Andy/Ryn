# Ryn

**Rich Yet Native** — A cross-platform, lightweight .NET framework for building desktop applications with web UIs.

Ryn gives .NET developers the Tauri experience without leaving C#. Native OS webviews, HTML/JS or Blazor frontends, NativeAOT-ready, source-generated IPC.

## Why Ryn?

- **C# backend, web frontend** — HTML/CSS/JS or Blazor frontend, C# backend with `[RynCommand]` source-generated IPC
- **Lightweight** — Uses native OS webviews (WebView2, WKWebView, WebKitGTK), not bundled Chromium
- **NativeAOT** — Small, fast, self-contained binaries (~4.3MB) with no runtime dependency
- **Cross-platform** — Windows, macOS, Linux
- **Plugin system** — FileSystem, Dialog (native pickers), Clipboard, Shell (spawn/PTY streaming), Notification
- **Security model** — Unified `ryn.json` capability scopes with deny-all semantics

## Status

**Early development** — Actively tested on macOS. Windows and Linux build in CI but need community testing.

## Getting Started (from source)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (preview)
- [GitHub CLI](https://cli.github.com/) (`gh`) for downloading native libraries
- Git with submodule support

### macOS

```bash
git clone --recursive https://github.com/Yupmoh/Ryn.git
cd Ryn
bash build/download-native.sh         # downloads prebuilt saucer libs
dotnet build Ryn.slnx
dotnet test Ryn.slnx
dotnet run --project samples/Showcase  # run the demo app
```

To build native libs from source (requires cmake + ninja):
```bash
bash build/build-native.sh
```

### Windows

```powershell
git clone --recursive https://github.com/Yupmoh/Ryn.git
cd Ryn
.\build\download-native.ps1           # downloads prebuilt saucer libs
dotnet build Ryn.slnx
dotnet test Ryn.slnx
dotnet run --project samples\Showcase
```

### Linux

```bash
git clone --recursive https://github.com/Yupmoh/Ryn.git
cd Ryn
bash build/download-native.sh         # downloads prebuilt saucer libs
dotnet build Ryn.slnx
dotnet test Ryn.slnx
dotnet run --project samples/Showcase
```

WebKitGTK dependencies (Ubuntu/Debian):
```bash
sudo apt-get install libwebkitgtk-6.0-dev
```

### Creating a new app

```bash
dotnet run --project src/Ryn.Cli -- new MyApp
cd MyApp
dotnet run
```

### Bundling for distribution

```bash
dotnet run --project src/Ryn.Cli -- bundle
```

This creates:
- **macOS**: `.app` bundle with Info.plist
- **Windows**: Self-contained folder with executable
- **Linux**: AppDir structure (use [appimagetool](https://appimage.github.io/) to create an AppImage)

## Project Structure

```
src/
  Ryn.Core          — Window management, app lifecycle, configuration
  Ryn.Interop       — Auto-generated saucer C bindings via ClangSharp
  Ryn.Ipc           — JavaScript <> C# IPC bridge with source generators
  Ryn.Plugins.*     — Native capabilities (FileSystem, Dialog, Clipboard, Shell, Notification)
  Ryn.Cli           — CLI tool for scaffolding, dev, build, bundle
samples/
  HelloWindow/      — Minimal IPC demo
  Showcase/         — Full-featured demo with all plugins
  ViteApp/          — URL-backed frontend for Vite dev server integration
tests/              — 109 xUnit tests
benchmarks/         — BenchmarkDotNet suites (IPC, marshaling, JSON, escaping)
```

## License

[MIT](LICENSE)
