# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Prague Tram Tracker is a Windows taskbar widget that displays real-time Prague public transport arrival times using the Golemio API. It shows a visual route with vehicle position, line number badge, and minutes to arrival. Rendered with Direct2D via the TaskbarWidget submodule and published as a NativeAOT exe.

## Build Commands

```bash
# Build / run (JIT, fast iteration)
dotnet build -p:Platform=x64
dotnet run --project src/TramTracker/TramTracker.csproj -p:Platform=x64

# Publish the release exe: NativeAOT, single ~2 MB native file
# (needs the VS "Desktop development with C++" workload; vswhere.exe must be on PATH)
dotnet publish src/TramTracker/TramTracker.csproj -c Release -r win-x64 -p:Platform=x64 -o publish

# Build against a local taskbar-widget checkout instead of the submodule
dotnet build src/TramTracker/TramTracker.csproj -p:Platform=x64 -p:TaskbarWidgetDir=../taskbar-widget
```

## Architecture

- **TramTracker** (`src/TramTracker/`): the widget app, .NET 10 NativeAOT
- **TaskbarWidget** (`lib/taskbar-widget/`): git submodule. Direct2D-rendered taskbar widget toolkit with shared, deterministic cross-widget positioning

After cloning, initialize the submodule:
```bash
git submodule update --init --recursive
```

```
src/TramTracker/
├── Program.cs      # Entry point: TramApp.Start() + Widget.Run()
├── TramApp.cs      # Widget UI (badge, countdown, route track, tooltip, menu), refresh scheduling
├── Departures.cs   # Golemio fetch (WinHTTP via TaskbarWidget.Http) + JSON parsing → immutable TramState
└── TramConfig.cs   # config.json (case-insensitive), time windows, API key lookup
```

- Fetches run on the thread pool and post an immutable `TramState` back to the UI thread (`widget.Post`).
- Golemio returns at most 100 departures and has no line filter, so `Golemio.Fetch` finds the platform (stop id) the tracked line leaves from (station board, then platform-by-platform) and then queries only that platform (`ids=`), which covers ~2 hours.
- The countdown and vehicle position are computed at render time from the departure timestamp, with a repaint every 5 s.
- A failed fetch keeps the last good data, and the error is shown in the tooltip.

### Time-Based Configuration

Config supports time windows that switch station/line/direction by time of day:
- `TimeWindows` array with `StartTime`/`EndTime` (24h format, supports midnight crossing)
- Optional `MinMinutes` per window / `DefaultConfig`: hide trams closer than that (walking time); the track counts down to "leave now"
- Optional `Days` per window: `"Mon-Fri"`, `"Sat,Sun"`, `"weekdays"`, `"weekends"` or an array of those
- The first matching window wins, so a weekday-only window can sit above an every-day one for the same hours
- `DefaultConfig` fallback when no window matches
- Legacy flat fields still work when `TimeWindows` is null
- Other keys: `ApiKey`, `RefreshIntervalSeconds` (10–3600), `DepartureLimit`, `TextColor` (`auto`/`white`/`black`), `StartWithWindows`

### Data Storage

- Config: `%LOCALAPPDATA%\TramTracker\config.json` (re-read before every fetch)
- API Key: `GOLEMIO_API_KEY` in a `.env` file next to the exe or in the working directory, or `ApiKey` in config
- Logs: `%LOCALAPPDATA%\TaskbarWidget\logs\Tram.log` (set `TASKBARWIDGET_LOG=1` for verbose)

Get your API key from: https://api.golemio.cz/

## Gotchas

- **Platform required**: Use `-p:Platform=x64` for all build commands.
- **Submodule**: Must initialize submodule before building.
- **API Key**: Must have valid Golemio API key in .env file or config.
- **Config hot-reload**: `TramConfig.Reload()` runs before every fetch, so config changes apply without restart.
- **NativeAOT**: no reflection-based JSON. Parse with `JsonDocument`/`JsonNode`.

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
  - Builds the NativeAOT exe and zip
  - Extracts changelog notes
  - Creates GitHub release with artifacts

## Commit Guidelines

Do not add `Co-Authored-By: Claude` or similar co-author lines to commits.
