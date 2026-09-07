// Discord Rich Presence stub
// To enable: set your Discord Application Client ID in AppSettings.DiscordClientId
// and set EnableDiscordRichPresence = true in Settings.
// Requires a registered Discord application at https://discord.com/developers/applications
using AgApp.Models;
using DiscordRPC;

namespace AgApp.Services;

public class DiscordService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly GameSessionService _session;

    // Stub: real client commented out until user supplies a Client ID
    // private DiscordRpcClient? _client;

    private bool _disposed;

    public DiscordService(SettingsService settings, GameSessionService session)
    {
        _settings = settings;
        _session  = session;

        session.SessionStarted += OnSessionStarted;
        session.SessionEnded   += OnSessionEnded;
    }

    /// <summary>Initializes the Discord RPC client if enabled and a Client ID is configured.</summary>
    public void Initialize()
    {
        try
        {
            var cfg = _settings.Current;
            if (!cfg.EnableDiscordRichPresence) return;
            if (string.IsNullOrWhiteSpace(cfg.DiscordClientId)) return;

            /* --- Uncomment when you have a real Discord Application Client ID ---
            _client = new DiscordRpcClient(cfg.DiscordClientId)
            {
                Logger = new DiscordRPC.Logging.NullLogger(),
                SkipIdenticalPresence = true,
            };
            _client.Initialize();
            AppLogger.Info("[Discord] RPC client initialized.");
            */

            AppLogger.Info("[Discord] Rich Presence stub ready (client not started — no app ID wired).");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[Discord] Initialize failed", ex);
        }
    }

    private void OnSessionStarted(InstalledGame game)
    {
        try
        {
            /* --- Uncomment when real client is wired ---
            if (_client == null || !_client.IsInitialized) return;
            _client.SetPresence(new RichPresence
            {
                Details = game.Title,
                State   = "Playing",
                Timestamps = Timestamps.Now,
                Assets = new Assets
                {
                    LargeImageKey  = "logo",         // upload art in Discord dev portal
                    LargeImageText = "Davey Jones' Locker",
                },
            });
            */

            AppLogger.Info($"[Discord] Would set presence: Playing {game.Title}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[Discord] OnSessionStarted failed", ex);
        }
    }

    private void OnSessionEnded(InstalledGame game, TimeSpan _elapsed)
    {
        try
        {
            /* --- Uncomment when real client is wired ---
            _client?.ClearPresence();
            */

            AppLogger.Info($"[Discord] Would clear presence (session ended: {game.Title})");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[Discord] OnSessionEnded failed", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.SessionStarted -= OnSessionStarted;
        _session.SessionEnded   -= OnSessionEnded;
        /* --- Uncomment when real client is wired ---
        _client?.Dispose();
        */
    }
}
