# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [v2.0.4] - 2026-02-17

### Fixed
- Hover overlay now activates over the entire widget area, not just the line number badge

## [v2.0.3] - 2026-02-16

### Fixed
- Widget now auto-positions correctly when running as the only widget
- Periodic position drift detection keeps widget aligned with tray area
- Widget repositions on display resolution and system settings changes
- Rapid left-clicks no longer ignored on every other click

## [v2.0.2] - 2026-02-13

### Fixed
- Widgets no longer display over fullscreen applications

## [v2.0.1] - 2026-02-12

### Changed
- Smooth lerp animation when dragging widgets to reorder
- Smooth animation when neighboring widget resizes (widgets slide instead of snapping)
- Enabled IL trimming — exe size reduced from 89 MB to ~14 MB
- Removed unused `System.Drawing.Common` dependency
- Switched to source-generated JSON serialization (trim-safe)

### Fixed
- Right-click no longer ignored on every other rapid click

## [v2.0.0] - 2026-02-12

### Changed
- Replaced WinUI 3 / Windows App SDK with pure Win32 GDI rendering (~10 MB exe vs ~153 MB)
- Widget rendering now uses `TaskbarWidget.Widget` API with immediate-mode GDI
- Route visualization drawn via Canvas API (circles, lines) instead of XAML shapes
- Refresh timer uses `widget.SetInterval()` instead of `DispatcherQueueTimer`
- No runtime prerequisites needed (removed WinUI 3 framework dependency)

### Added
- Drag-to-reorder support — reorder widgets by dragging
- Cross-widget atomic repositioning when widget resizes

### Removed
- All XAML files (`App.xaml`, `MainWindow.xaml`, `TramWidgetContent.xaml`)
- `Microsoft.WindowsAppSDK` and `Microsoft.Windows.SDK.BuildTools` NuGet dependencies

## [v1.4.1] - 2026-02-11

### Fixed
- Startup registry path now updates when app is moved to a new location

## [v1.4.0] - 2026-02-11

### Added
- Windows startup support — app can auto-launch on Windows login via registry
- `startWithWindows` configuration option (default: true)

## [v1.3.0] - 2026-02-05

### Added
- Automatic text color adaptation based on Windows light/dark mode using WinUI 3's native ActualTheme property
- TextColor configuration option with values: "auto" (default), "white", or "black"
- Real-time theme change detection using ActualThemeChanged event - text color updates instantly when Windows theme changes
- Text color automatically updates when config is reloaded

### Changed
- Arrival time text now displays in white for dark mode and black for light mode (when TextColor is "auto")

### Fixed
- Dark mode detection now uses WinUI 3 framework-native ActualTheme API instead of registry access for proper theme detection

## [v1.2.0] - 2026-02-04

### Added
- Next tram arrival time displayed in tooltip showing when the following tram will arrive

## [v1.1.0] - 2026-02-04

### Added
- Time-based configuration system allowing different stations/lines at different times of day
- Support for time windows (e.g., track line 12 from 10:00-16:00, line 17 from 16:00-23:59)
- Midnight-crossing time windows (e.g., 22:00-02:00 for night service)
- Dynamic configuration reloading without app restart
- Configuration changes take effect automatically on next refresh cycle (default 30 seconds)
- Automatic refresh interval adjustment when changed in config
- Configuration validation with debug warnings for invalid time formats
- Example configuration files (config.example.json and config.legacy.example.json)

### Changed
- Configuration is now reloaded on every API call to pick up changes immediately
- Refresh timer automatically adjusts when RefreshIntervalSeconds is changed

## [v1.0.1] - 2026-02-03

### Added
- API key can now be configured in config.json as fallback when .env file is not present

## [v1.0.0] - 2026-02-03

### Added
- Initial release
- Windows taskbar widget showing Prague tram arrival times
- Real-time data from Golemio API
- Visual route display with 3 stops and moving vehicle indicator
- Vehicle position based on minutes to arrival
- Delay color coding (green = on time, orange = slight delay, red = significant delay)
- Traveled path visualization (line painted behind vehicle)
- User station highlighted in white
- Line number badge with arrival time in minutes
- Configurable station, line number, and direction filtering
- 30-second polling interval (configurable)
- Tooltip with full departure details
- Native hover effect matching Windows taskbar style
