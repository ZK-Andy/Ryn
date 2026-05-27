# Ryn

**Rich Yet Native** — A cross-platform, lightweight .NET framework for building desktop applications with web UIs.

Ryn gives .NET developers the Tauri experience without leaving C#. Native OS webviews, HTML/JS or Blazor frontends, NativeAOT-ready, source-generated IPC.

## Why Ryn?

- **C# backend, web frontend** — HTML/CSS/JS frontend, C# backend with `[RynCommand]` source-generated IPC
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

### Check your environment

```bash
dotnet run --project src/Ryn.Cli -- doctor
```

This checks your .NET SDK version, native libraries, WebView runtime, and build tools.

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

# Install WebKitGTK (Ubuntu/Debian)
sudo apt-get install libwebkitgtk-6.0-dev

bash build/download-native.sh         # downloads prebuilt saucer libs
dotnet build Ryn.slnx
dotnet test Ryn.slnx
dotnet run --project samples/Showcase
```

## Creating an App

### From the CLI

```bash
dotnet run --project src/Ryn.Cli -- new MyApp
cd MyApp
dotnet run
```

This scaffolds a project with project references to the local Ryn source. The generated app includes a sample IPC command, a dark-themed HTML frontend, and a `ryn.json` capability file.

### Content serving

Ryn serves frontend files via the `ryn://` custom scheme, keeping everything same-origin with IPC (no CORS issues). There are three ways to provide content:

```csharp
// Option 1: ContentDirectory — serves files from disk (recommended for dev)
opts.ContentDirectory = Path.Combine(AppContext.BaseDirectory, "wwwroot");

// Option 2: Html — inline HTML string
opts.Html = "<html>...</html>";

// Option 3: Url — external URL (e.g. Vite dev server)
opts.Url = new Uri("http://localhost:5173");
```

With `ContentDirectory`, files are read from disk on each request — changes are reflected on browser refresh without restarting the app.

### Windows requirements

On Windows, the entry point **must** use `[STAThread]` with a synchronous `Main` method. Without it, WebView2 initialization deadlocks silently. Ryn detects this at runtime and throws a clear error.

```csharp
public static class Program
{
    [System.STAThread]
    public static void Main()
    {
        var app = RynApplication.CreateBuilder()
            // ...
            .Build();

        app.Run(); // synchronous — blocks until window closes
    }
}
```

Do **not** use `async Task Main` or top-level statements on Windows — both default to MTA, which is incompatible with WebView2's COM requirements.

### How IPC works

Mark C# methods with `[RynCommand]` — the source generator creates dispatch tables at compile time:

```csharp
using Ryn.Ipc;

public static class MyCommands
{
    [RynCommand]
    public static string Greet(string name) => $"Hello, {name}!";

    [RynCommand]
    public static int Add(int a, int b) => a + b;
}
```

Call them from JavaScript:

```javascript
const greeting = await window.__ryn.invoke('greet', { name: 'World' });
const sum = await window.__ryn.invoke('add', { a: 2, b: 3 });
```

Supported parameter/return types: `int`, `long`, `float`, `double`, `bool`, `string`, `JsonElement`, primitive arrays (`int[]`, `string[]`), nullable types (`int?`), and complex DTOs via `[RynJsonContext]`.

### Security with ryn.json

Control what the frontend can access:

```json
{
  "capabilities": {
    "fs": {
      "allow": ["readTextFile", "readDir"],
      "scope": ["$APP_DATA"]
    },
    "shell": {
      "allow": ["execute"],
      "commands": ["echo", "git"]
    },
    "clipboard": true,
    "notification": true
  }
}
```

Missing `ryn.json` = allow all (dev mode). Present = deny by default. Empty `scope: []` or `commands: []` = explicit deny-all.

## Bundling for Distribution

```bash
dotnet run --project src/Ryn.Cli -- bundle
```

Options:
- `--aot` — Enable NativeAOT publishing
- `--self-contained` — Include .NET runtime
- `--icon path/to/icon.icns` — Set app icon (macOS)
- `--sign "Developer ID"` — Code sign (macOS)
- `--notarize` — Submit for Apple notarization (macOS)
- `--version 1.0.0` — Set bundle version

Output:
- **macOS**: `.app` bundle with Info.plist
- **Windows**: Self-contained folder with executable
- **Linux**: AppDir structure (use [appimagetool](https://appimage.github.io/) to create an AppImage)

## Sample Apps

| Sample | Description | Run |
|--------|-------------|-----|
| [HelloWindow](samples/HelloWindow) | Minimal IPC demo | `dotnet run --project samples/HelloWindow` |
| [Showcase](samples/Showcase) | Full-featured demo with all plugins | `dotnet run --project samples/Showcase` |
| [ViteApp](samples/ViteApp) | URL-backed frontend for Vite dev servers | `dotnet run --project samples/ViteApp` |
| [TerminalApp](samples/TerminalApp) | Terminal with shell.execute and streaming metrics | `dotnet run --project samples/TerminalApp` |
| [FileManager](samples/FileManager) | File browser with breadcrumb nav and preview | `dotnet run --project samples/FileManager` |
| [MarkdownEditor](samples/MarkdownEditor) | Split-pane editor with live preview and native dialogs | `dotnet run --project samples/MarkdownEditor` |
| [DevKit](samples/DevKit) | Developer toolkit exercising every Ryn capability | `dotnet run --project samples/DevKit` |

## Project Structure

```
src/
  Ryn.Core             — Window management, app lifecycle, configuration, events
  Ryn.Interop          — Auto-generated saucer C bindings via ClangSharp
  Ryn.Ipc              — JS <> C# IPC bridge, source generator, capabilities, observability
  Ryn.Plugins.*        — FileSystem, Dialog, Clipboard, Shell (spawn/PTY), Notification
  Ryn.Cli              — CLI: new, dev, build, bundle, doctor
samples/               — 6 example applications
templates/             — dotnet new template pack
tests/                 — 132 xUnit tests across 6 test projects
benchmarks/            — BenchmarkDotNet suites (IPC, marshaling, JSON, escaping)
docs/
  plan/PLAN.md         — Full project plan with milestone tracking
  plugin-authoring.md  — Guide for writing Ryn plugins
```

## Writing Plugins

See [docs/plugin-authoring.md](docs/plugin-authoring.md) for a complete guide on creating Ryn plugins with commands, options, DI, events, and capability scopes.

## License

[MIT](LICENSE)
