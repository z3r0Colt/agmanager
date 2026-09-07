# Changelog

All notable changes to Davey Jones' Locker are documented here.

## [1.0.0] — 2026-09-07

### Phase 0 — Rename + AppLogger + Library Schema Migration
- Renamed application from "AgApp" to **Davey Jones' Locker**
- Added `AppLogger` (Info / Warn / Error, writes to `%APPDATA%\AgApp\app.log`)
- Added `LibraryData` wrapper with `SchemaVersion` for forward-compatible library saves
- Library load: tries `LibraryData` format first, falls back to legacy `List<InstalledGame>`, creates backup on schema upgrade

### Phase 1 — Process Tracking and Launch Options
- Added `GameSessionService` — tracks active game session via process polling (2 s tick, 5 consecutive misses = session ended)
- Per-game launch options: `LaunchTargetOverride`, `RunAsAdmin`, `WorkingDirectoryOverride`, `PreLaunchPath`/`Args`
- `LibraryService.LaunchGame(game, session)` overload that handles pre-launch, runas, working dir, and begins a session
- Now-Playing indicator in sidebar footer with elapsed timer

### Phase 2 — Save Game Backup and Restore
- Added `SaveBackupService` — `BackupAsync`, `RestoreAsync`, `ListBackups`, `BackupAllAsync`, pruning
- Uses `System.IO.Compression.ZipFile` for portable ZIP backups
- Per-game `SaveFolder` and `BackupSchedule` settings; automatic backup on session end when schedule = "OnExit"
- `DetectCandidates` heuristic scans known save-game locations

### Phase 3 — Download Robustness
- Download concurrency gate (`SemaphoreSlim`, configurable 1–5 simultaneous downloads)
- Disk-space check before starting download (requires 1.5× archive size free)
- Retry loop: up to 3 attempts with exponential back-off (2, 4, 8 s); `OperationCanceledException` exits without retry
- Queue reorder: MoveUp / MoveDown commands on `DownloadJob`
- `DownloadManager.Jobs` is now the single source of truth; `DownloadsViewModel` references it directly

### Phase 4 — Update Detection
- Added `UpdateCheckService` with background `System.Threading.Timer` (configurable interval, default 24 h)
- `CheckAsync` fetches the game's page URL, extracts a version string, and compares to stored `VersionString`
- `UpdateFound` event; `HasUpdate` flag on `InstalledGame`; "Update Available" banner in Library detail panel

### Phase 5 — Library Organization
- Library filters: `StatusFilter`, `TagFilter`, `ShowHidden` toggle
- `PickForMe` command — launches a random unplayed game
- `ImportFolderAsync` — imports an existing game folder from disk
- `ToggleHidden` command; `IsHidden` flag on `InstalledGame`
- Added **Storage** page: per-drive usage bars, game list sorted by size, per-game Move button
- `StorageService` — `GetDriveUsage`, `MoveGameAsync` (copy-verify-delete with progress reporting)

### Phase 6 — Discord Rich Presence Stub
- Added `DiscordRichPresence` NuGet package
- `DiscordService` wires to `GameSessionService` events; all Discord API calls are commented out pending Client ID configuration
- Settings UI: Enable toggle + Client ID field
- `AppSettings.EnableDiscordRichPresence`, `DiscordClientId`

### Phase 7 — App Housekeeping
- **About page** — app info, version, built-with credits, keyboard shortcut reference, GitHub and License links
- **Log viewer** — read-only view of `app.log` with auto-refresh (3 s), clear, and open-folder buttons
- **Close-to-tray** (`AppSettings.CloseToTray`) — OS close button hides to tray instead of quitting; title-bar X always quits
- **Start with Windows** (`AppSettings.StartWithWindows`) — writes/removes `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- **Library export/import** — export `List<InstalledGame>` to JSON; import merges into existing library via `AddOrUpdate`
- **Keyboard shortcuts** — `Ctrl+,` → Settings; `Ctrl+F` → focus library search; `Escape` → deselect game
- **GamepadService stub** — XInput P/Invoke structure ready; polling loop commented out until wired
- Settings UI updated with all new options
