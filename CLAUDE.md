# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Prague Tram Tracker is a Windows taskbar widget that displays real-time Prague public transport arrival times using the Golemio API. It shows a visual route with vehicle position, line number badge, and minutes to arrival.

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

- **TramTracker** (`src/TramTracker/`) - WinUI 3 app with taskbar widget
- **TaskbarWidget** (`lib/taskbar-widget/`) - Git submodule for taskbar widget injection

After cloning, initialize the submodule:
```bash
git submodule update --init --recursive
```

### Key Components

```
src/TramTracker/
├── Program.cs              # Entry point with ComWrappers init
├── App.xaml.cs             # Creates widget, starts polling timer
├── MainWindow.xaml.cs      # Hidden window (WinUI lifecycle requirement)
├── Widget/
│   ├── TramWidget.cs              # Injection orchestrator
│   └── TramWidgetContent.xaml     # Visual UI with route visualization
├── Services/
│   ├── IGolemioService.cs         # Interface for API operations
│   ├── GolemioService.cs          # Golemio API client
│   ├── SettingsService.cs         # Config persistence
│   └── EnvService.cs              # .env file loading for API key
└── Models/
    ├── AppConfig.cs               # Configuration model
    ├── GolemioModels.cs           # API response models
    └── TramState.cs               # Widget state model
```

### Widget System

The widget uses `TaskbarInjectionHelper` from the submodule:
1. Creates a host window with `DeferInjection=true`
2. Sets up `DesktopWindowXamlSource` for WinUI content
3. Injects into taskbar after XAML setup
4. Displays route visualization with vehicle position

### Visual Elements

- 3 stop indicators (gray circles) connected by line
- Moving vehicle indicator (green/orange/red based on delay)
- Traveled path painted green behind vehicle
- User's station (rightmost) highlighted white
- Line number badge (yellow)
- Arrival time in minutes

### Services

- **GolemioService**: Fetches departures from Golemio API
- **SettingsService**: Loads/saves config.json
- **EnvService**: Loads API key from .env file

### Data Storage

- Config: `%LOCALAPPDATA%\TramTracker\config.json`
- API Key: `.env` file in app directory (gitignored)

### Configuration Options

```json
{
  "StationName": "Chotkovy sady",
  "LineNumber": "12",
  "Direction": "Lehovec",
  "RefreshIntervalSeconds": 30,
  "DepartureLimit": 10,
  "TextColor": "auto"
}
```

**TextColor options:**
- `"auto"` - Automatically detects Windows light/dark mode (default)
- `"white"` - Always use white text
- `"black"` - Always use black text

### Environment Variables

Create a `.env` file with your Golemio API key:
```
GOLEMIO_API_KEY=your_api_key_here
```

Get your API key from: https://api.golemio.cz/

## Gotchas

- **Platform required**: WinUI 3 requires explicit platform. Use `-p:Platform=x64` for all commands.
- **Hidden MainWindow**: Don't call `Activate()` on MainWindow - it must stay hidden for widget-only mode.
- **Submodule**: Must initialize submodule before building.
- **API Key**: Must have valid Golemio API key in .env file.

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
