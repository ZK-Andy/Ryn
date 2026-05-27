# Ryn

**Rich Yet Native** — A cross-platform, lightweight .NET framework for building desktop applications with web UIs.

Ryn gives .NET developers the Tauri experience without leaving C#. Native OS webviews, HTML/JS or Blazor frontends, NativeAOT-ready, source-generated IPC.

## Why Ryn?

- **C# backend, web frontend** — HTML/CSS/JS or Blazor frontend, C# backend with `[RynCommand]` source-generated IPC
- **Lightweight** — Uses native OS webviews (WebView2, WKWebView, WebKitGTK), not bundled Chromium
- **NativeAOT** — Small, fast, self-contained binaries (~4.3MB) with no runtime dependency
- **Cross-platform** — Windows, macOS, Linux
- **Plugin system** — FileSystem, Dialog (native pickers), Clipboard, Shell (spawn with streaming), Notification
- **Vertical slice architecture** — Each feature is self-contained and independently testable

## Status

**Early development** — Not ready for production use.

## Project Structure

```
src/
  Ryn.Core          — Window management, app lifecycle, configuration
  Ryn.Interop       — Auto-generated saucer C bindings via ClangSharp
  Ryn.Ipc           — JavaScript ↔ C# IPC bridge with source generators
  Ryn.Plugins.*     — Native capabilities (FileSystem, Dialog, Clipboard, Shell, Notification)
  Ryn.Cli           — dotnet tool for scaffolding, dev, and building
tests/              — xUnit tests for all components
benchmarks/         — BenchmarkDotNet with allocation tracking
examples/           — Sample applications
docs/               — Architecture docs and project plan
```

## Quick Start

```bash
# install the CLI
dotnet tool install --global Ryn.Cli

# create a new app
dotnet ryn new myapp
cd myapp

# run in dev mode
dotnet ryn dev
```

## Building from Source

```bash
git clone https://github.com/user/Ryn.git
cd Ryn
dotnet build
dotnet test
```

## License

[MIT](LICENSE)
