# Scrubbler 2

Scrubbler 2 is a plugin-based desktop app for fixing missing music scrobbles.

If a player, service, or import workflow failed to submit your listens, Scrubbler gives you a place to authenticate your scrobble account, install the plugins you need, review what will be sent, and submit those scrobbles manually or automatically.

## What It Does

- Sends scrobbles through account plugins you install and log into
- Supports manual scrobble workflows and auto-scrobble workflows through separate plugins
- Lets you enable multiple destination accounts at once
- Downloads and updates plugins from an online plugin catalog
- Checks GitHub Releases for app updates and can restart into the updated version for you

## How Scrubbler Works

Scrubbler itself is the host app. The actual scrobbling features come from plugins.

There are three main plugin types:

- `Account` plugins connect Scrubbler to a scrobble destination such as Last.fm-style services
- `Scrobble` plugins help you create/import scrobbles manually
- `Auto-Scrobble` plugins detect or prepare scrobbles automatically from supported sources

When you first open the app, it may look mostly empty until you install plugins.

## Quick Start

1. Download the latest release for your platform from the GitHub Releases page:
   `https://github.com/SHOEGAZEssb/Scrubbler-2/releases`
2. Extract the zip to a normal writable folder.
   Avoid running it directly from the zip, and avoid protected locations such as `Program Files` if you want plugin installs and in-app updates to work smoothly.
3. Launch the app:
   - Windows: `Scrubbler.Host.exe`
   - Linux/macOS: `Scrubbler.Host`
4. Open `Plugin Manager` -> `Available`.
5. Install at least:
   - one `Account` plugin
   - one `Scrobble` or `Auto-Scrobble` plugin
6. Open `Accounts`, log into your account plugin, and enable `Scrobble`.
7. Open the installed plugin under `Plugins` in the sidebar and use it to prepare/send scrobbles.

## Plugin Management

Scrubbler fetches available plugins from a plugin repository. The built-in default repository is:

`https://raw.githubusercontent.com/shoegazessb/scrubbler-plugins/main/plugins.json`

Inside the app you can:

- browse available plugins
- filter to compatible plugins
- install and uninstall plugins
- update installed plugins when newer versions are available
- choose which authenticated account provides account functions when multiple account plugins support them

Installed plugins are stored in a `Plugins` folder next to the app.

## Updates

The app includes built-in update checks in `Settings` -> `About`.

- You can check for updates manually
- You can enable update checks on startup
- Updates are delivered as portable zip packages from GitHub Releases

Current release packaging targets:

- `win-x64`
- `linux-x64`
- `osx-x64`

## Build From Source

### Prerequisites

- .NET 10 SDK
- Git with submodule support
- A desktop environment supported by Uno Platform for local runs

### Clone

```bash
git clone --recurse-submodules https://github.com/SHOEGAZEssb/Scrubbler-2.git
cd Scrubbler-2
```

If you already cloned without submodules:

```bash
git submodule update --init --recursive
```

### Build

```bash
cd Scrubbler
dotnet build -c Release
```

### Test

```bash
cd Scrubbler
dotnet test -c Release --no-build
```

### Run

You can open `Scrubbler/Scrubbler.sln` in Visual Studio, or run the desktop target from the command line:

```bash
cd Scrubbler
dotnet run --project Scrubbler.Host/Scrubbler.Host.csproj -f net10.0-desktop
```

## Repository Layout

- `Scrubbler/Scrubbler.Host` - main desktop app
- `Scrubbler/Scrubbler.Updater` - external updater used during in-place app updates
- `Scrubbler/Scrubbler.Test` - automated tests
- `Scrubbler/deps` - submodule with shared dependencies and package version definitions

## License

This project is licensed under the GPL v3. See [LICENSE](LICENSE).
