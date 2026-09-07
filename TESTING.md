# Testing Checklist — Davey Jones' Locker v1.0.0

Use this checklist before publishing a release. Check each item manually.

## Startup

- [ ] App launches without errors
- [ ] `%APPDATA%\AgApp\` folder is created on first run
- [ ] `settings.json` and `library.json` are created with defaults
- [ ] Window position/size restores correctly on second launch
- [ ] System tray icon appears

## Browse Page

- [ ] WebView2 loads and navigates correctly
- [ ] Ad-block filter runs without exceptions (check log)
- [ ] Initiating a site download creates a `DownloadJob` and switches to Downloads tab

## Downloads

- [ ] Download starts and progress updates
- [ ] Pause / Resume works
- [ ] Cancel removes job
- [ ] MoveUp / MoveDown reorders queue
- [ ] Concurrency gate: only N downloads run simultaneously (configure N in Settings)
- [ ] Disk-space check: insufficient space shows an error
- [ ] Retry: simulate network failure — job retries up to 3 times
- [ ] Extraction completes and game appears in Library
- [ ] "Extraction Complete" balloon tip appears (if enabled in Settings)

## Library

- [ ] Installed games display with cover art
- [ ] Search filters by title
- [ ] StatusFilter / TagFilter filter the list
- [ ] ShowHidden toggle shows/hides hidden games
- [ ] PickForMe launches a random game
- [ ] ImportFolder imports a game folder
- [ ] Launch Options expander: override path, RunAsAdmin, working dir, pre-launch
- [ ] Game launches and session tracking starts (Now Playing indicator in sidebar)
- [ ] Session timer updates every second
- [ ] Session ends when game closes; playtime is saved
- [ ] Save folder detection populates candidates
- [ ] Manual backup creates a ZIP in `%APPDATA%\AgApp\SaveBackups\`
- [ ] Restore unpacks the ZIP back to SaveFolder
- [ ] "Update Available" badge appears when HasUpdate = true
- [ ] OpenGamePage opens the browser with the stored URL
- [ ] Escape clears selection; Ctrl+F focuses search

## Storage Page

- [ ] Drive usage cards show correct free/used percentages
- [ ] Game list is sorted by size descending
- [ ] Move button: select destination folder, files copy and original is deleted

## Settings

- [ ] All fields save and reload correctly
- [ ] BrowseFolder picker works
- [ ] MaxConcurrentDownloads slider (1–5) applies to next download
- [ ] SaveBackupRoot and MaxBackupsPerGame persist
- [ ] Discord Rich Presence toggle and Client ID field save
- [ ] StartWithWindows: enable writes registry key, disable removes it
- [ ] CloseToTray: OS close hides to tray; title-bar X always quits
- [ ] Export Library: creates valid JSON file
- [ ] Import Library: merges games correctly

## About Page

- [ ] App name, version, tagline display
- [ ] GitHub link opens in browser
- [ ] MIT License link opens in browser or license file
- [ ] Keyboard shortcut table is correct

## Log Viewer

- [ ] Log text loads on page open
- [ ] Auto-refresh updates content every ~3 s
- [ ] Clear empties the log file
- [ ] Open Folder opens `%APPDATA%\AgApp\` in Explorer

## Tray Behavior

- [ ] Minimize button: hides to tray if MinimizeToTray = true, else minimizes
- [ ] Tray double-click restores window
- [ ] Tray context menu: Show / Exit work
- [ ] CloseToTray = true: Alt+F4 hides to tray with balloon tip

## Keyboard Shortcuts

- [ ] `Ctrl + ,` opens Settings from any page
- [ ] `Ctrl + F` focuses search box when Library is active
- [ ] `Escape` deselects selected game

## Release Build

- [ ] `dotnet build -c Release` succeeds with 0 errors, 0 warnings
- [ ] Release DLL is in `bin\Release\net8.0-windows\`
