using System.Diagnostics;
using AgApp.Models;

namespace AgApp.Services;

public class GameSessionService
{
    private readonly LibraryService _library;
    private InstalledGame? _currentGame;
    private Process? _rootProcess;
    private DateTime _sessionStart;
    private CancellationTokenSource? _cts;
    private TimeSpan _lastSaved = TimeSpan.Zero;

    public event Action<InstalledGame>? SessionStarted;
    public event Action<InstalledGame, TimeSpan>? SessionEnded;

    public InstalledGame? CurrentGame => _currentGame;
    public TimeSpan CurrentElapsed => _currentGame != null
        ? DateTime.Now - _sessionStart : TimeSpan.Zero;

    public GameSessionService(LibraryService library) => _library = library;

    public void BeginSession(InstalledGame game, Process rootProcess)
    {
        _cts?.Cancel();
        _currentGame = game;
        _rootProcess = rootProcess;
        _sessionStart = DateTime.Now;
        _lastSaved = TimeSpan.Zero;
        game.IsNowPlaying = true;
        game.CurrentSessionTime = TimeSpan.Zero;
        SessionStarted?.Invoke(game);
        _cts = new CancellationTokenSource();
        _ = TrackAsync(_cts.Token);
    }

    private async Task TrackAsync(CancellationToken ct)
    {
        int noProcessCount = 0;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(2000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            if (ct.IsCancellationRequested) break;

            var elapsed = DateTime.Now - _sessionStart;
            if (_currentGame != null)
                _currentGame.CurrentSessionTime = elapsed;

            // Save every ~60 seconds
            if (_currentGame != null && elapsed - _lastSaved >= TimeSpan.FromSeconds(60))
            {
                var delta = elapsed - _lastSaved;
                _library.UpdatePlayTime(_currentGame.Id, delta);
                _lastSaved = elapsed;
            }

            bool anyAlive = AnyProcessAlive();
            if (!anyAlive)
            {
                noProcessCount++;
                if (noProcessCount >= 5) // 10 seconds of no process
                {
                    EndSession();
                    break;
                }
            }
            else
            {
                noProcessCount = 0;
            }
        }
    }

    private bool AnyProcessAlive()
    {
        try
        {
            if (_rootProcess != null && !_rootProcess.HasExited) return true;
        }
        catch { }

        if (_currentGame?.InstallPath is not { Length: > 0 } installPath) return false;

        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var fn = proc.MainModule?.FileName ?? "";
                    if (!proc.HasExited && fn.StartsWith(installPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private void EndSession()
    {
        if (_currentGame == null) return;
        var total = DateTime.Now - _sessionStart;
        var unsaved = total - _lastSaved;
        if (unsaved > TimeSpan.Zero)
            _library.UpdatePlayTime(_currentGame.Id, unsaved);
        _currentGame.IsNowPlaying = false;
        _currentGame.CurrentSessionTime = TimeSpan.Zero;
        SessionEnded?.Invoke(_currentGame, total);
        _currentGame = null;
        _rootProcess = null;
    }
}
