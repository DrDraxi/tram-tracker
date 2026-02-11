# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

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
