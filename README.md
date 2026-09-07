# Davey Jones' Locker

**Pirate Game Manager** — a free, open-source WPF application for downloading, organizing, and launching your game library.

---

## Features

- **Browse** — Integrated WebView2 browser with ad-blocking for browsing game sites.
- **Downloads** — Multi-threaded download manager with retry logic, concurrency control, disk-space checks, and automatic extraction.
- **Library** — Full game library with launch options, save-game backup, update detection, tagging, filtering, and hidden-game support.
- **Save Backups** — Automatic or manual ZIP backups of save-game folders, with configurable retention and restore.
- **Update Detection** — Background checks for newer versions by comparing version strings on the game page.
- **Storage** — Per-drive usage overview and cross-drive game moving.
- **Discord Rich Presence** — Stub integration ready to activate with your own Discord Application Client ID.
- **System Tray** — Minimize or close to tray, with balloon notifications.
- **About / Logs** — In-app log viewer and About page with keyboard shortcut reference.

---

## Requirements

- Windows 10 or later (64-bit)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

---

## Building from Source

```bash
git clone https://github.com/your-repo/davey-jones-locker.git
cd davey-jones-locker
dotnet build -c Release
```

Output: `bin\Release\net8.0-windows\DaveyJonesLocker.dll`

---

## Configuration

All settings are stored in `%APPDATA%\AgApp\`:

| File | Purpose |
|------|---------|
| `settings.json` | App settings |
| `library.json` | Game library |
| `app.log` | Application log |
| `SaveBackups\` | Save-game backup archives |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl + ,` | Open Settings |
| `Ctrl + F` | Focus search (Library page) |
| `Escape` | Deselect game (Library page) |

---

## Discord Rich Presence

1. Create an application at [discord.com/developers/applications](https://discord.com/developers/applications)
2. Copy your **Client ID**
3. Paste it in **Settings → Discord Rich Presence → Discord Application Client ID**
4. Check **Enable Discord Rich Presence**
5. Uncomment the `_client` lines in `Services/DiscordService.cs`

---

## License

MIT — see [LICENSE](LICENSE)
