# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Prague Tram Tracker is a Windows taskbar widget that displays real-time Prague public transport arrival times using the Golemio API. It shows a visual route with vehicle position, line number badge, and minutes to arrival. Uses pure Win32 GDI rendering via the TaskbarWidget submodule.

## Build Commands

```bash
# Build the solution
dotnet build -p:Platform=x64

# Build release
dotnet build --configuration Release -p:Platform=x64

# Run the app (x64)
dotnet run --project src/TramTracker/TramTracker.csproj -p:Platform=x64

# Publish single-file exe (x64)
dotnet publish src/TramTracker/TramTracker.csproj --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:WindowsPackageType=None -o publish
```

## Architecture

### Solution Structure

- **TramTracker** (`src/TramTracker/`) - Win32 GDI app with taskbar widget
- **TaskbarWidget** (`lib/taskbar-widget/`) - Git submodule for immediate-mode GDI widget toolkit

After cloning, initialize the submodule:
```bash
git submodule update --init --recursive
```

### Key Components

```
src/TramTracker/
├── Program.cs              # Entry point, creates services, runs message loop
├── Widget/
│   └── TramWidget.cs       # Render callback with route visualization + canvas drawing
├── Services/
│   ├── IGolemioService.cs         # Interface for API operations
│   ├── GolemioService.cs          # Golemio API client with state change events
│   ├── SettingsService.cs         # Config persistence (%LOCALAPPDATA%\TramTracker\)
│   ├── EnvService.cs              # .env file loading for API key
│   └── StartupService.cs          # Windows startup registry
└── Models/
    ├── AppConfig.cs               # Configuration with time-based window support
    ├── GolemioModels.cs           # API response models
    ├── TramState.cs               # Widget state (vehicle position, arrival, tooltip)
    ├── TimeWindowConfig.cs        # Time-of-day route switching
    └── TrackingConfig.cs          # Station/line/direction tuple
```

### Widget System

TramWidget uses the `TaskbarWidget.Widget` API (immediate-mode Win32 GDI):
1. Creates `new Widget("Tram", render: ctx => { ... })` with a render callback
2. Render callback draws route canvas, line badge, and arrival time using `ctx.Horizontal()`, `ctx.Canvas()`, `ctx.Panel()`
3. `widget.Show()` handles taskbar injection and positioning
4. `widget.SetInterval()` triggers periodic API fetches
5. `widget.Invalidate()` re-renders when `GolemioService.StateChanged` fires
6. `Widget.RunMessageLoop()` runs the Win32 message loop

### Time-Based Configuration

Config supports time windows that switch station/line/direction by time of day:
- `TimeWindows` array with `StartTime`/`EndTime` (24h format, supports midnight crossing)
- `DefaultConfig` fallback when no window matches
- Legacy flat fields still work when `TimeWindows` is null

### Data Storage

- Config: `%LOCALAPPDATA%\TramTracker\config.json`
- API Key: `.env` file in app directory (gitignored) or `ApiKey` field in config

### Environment Variables

Create a `.env` file with your Golemio API key:
```
GOLEMIO_API_KEY=your_api_key_here
```

Get your API key from: https://api.golemio.cz/

## Gotchas

- **Platform required**: Use `-p:Platform=x64` for all build commands.
- **Submodule**: Must initialize submodule before building.
- **API Key**: Must have valid Golemio API key in .env file or config.
- **Config hot-reload**: `SettingsService.LoadConfig()` is called on every fetch cycle, so config changes apply without restart.

## Releases

Version is derived from git tags. The GitHub Actions workflow automatically creates releases when a tag is pushed.

### How to Release

1. **Update CHANGELOG.md** with the new version section:
   ```markdown
   ## [v1.1.0] - YYYY-MM-DD

   ### Added
   - New feature description

   ### Changed
   - Changed behavior description

   ### Fixed
   - Bug fix description
   ```

2. **Commit the changelog**:
   ```bash
   git add CHANGELOG.md
   git commit -m "docs: update changelog for v1.1.0"
   git push
   ```

3. **Create and push the tag**:
   ```bash
   git tag v1.1.0
   git push origin v1.1.0
   ```

4. The workflow will automatically:
   - Build the exe and zip artifacts
   - Extract release notes from CHANGELOG.md for this version
   - Create a GitHub release with the artifacts and notes

### Changelog Format

Follow [Keep a Changelog](https://keepachangelog.com/) format:
- `### Added` - New features
- `### Changed` - Changes in existing functionality
- `### Deprecated` - Soon-to-be removed features
- `### Removed` - Removed features
- `### Fixed` - Bug fixes
- `### Security` - Security fixes

### Version Numbering

Follow [Semantic Versioning](https://semver.org/):
- **Major** (v2.0.0): Breaking changes
- **Minor** (v1.1.0): New features, backwards compatible
- **Patch** (v1.0.1): Bug fixes, backwards compatible

## CI/CD

### Workflows

- **CI** (`.github/workflows/ci.yml`): Runs on all pushes and PRs
  - Builds debug and release
  - Uploads portable exe artifact

- **Release** (`.github/workflows/release.yml`): Runs on version tags
  - Builds single-file exe and zip
  - Extracts changelog notes
  - Creates GitHub release with artifacts

## Commit Guidelines

Do not add `Co-Authored-By: Claude` or similar co-author lines to commits.
