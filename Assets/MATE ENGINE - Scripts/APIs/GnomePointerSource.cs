using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>D-Bus interface exported by the "MateEngine Pointer" GNOME Shell extension (see gnome-pointer-extension.sh).</summary>
[DBusInterface("org.mateengine.Pointer")]
internal interface IGnomePointer : IDBusObject
{
    /// <summary>Pointer position and size of the whole desktop, both in the compositor's coordinates.</summary>
    Task<(int x, int y, int width, int height)> GetAsync();
}

/// <summary>
/// Global pointer position on GNOME Wayland.
///
/// An X11 client under XWayland only learns where the pointer is while it is over one of its own windows'
/// input regions; everywhere else XQueryPointer keeps returning the pixel where the pointer left. Wayland has
/// no protocol to ask for the pointer, so the only source is the compositor itself: a tiny GNOME Shell
/// extension answers over D-Bus. Without the extension this source simply never becomes live and callers
/// fall back to the X server.
/// </summary>
public sealed class GnomePointerSource : IDisposable
{
    private const int PollMilliseconds = 33;
    private const int RetryMilliseconds = 5000;
    private const double StaleSeconds = 0.5;

    private readonly IGnomePointer _proxy;
    private readonly CancellationTokenSource _cancel = new();
    private readonly object _lock = new();
    private Vector2Int _position;
    private Vector2Int _desktopSize;
    private long _updatedTicks;

    public GnomePointerSource(Connection connection)
    {
        // gnome-shell exports the extension's object on its own connection, which owns org.gnome.Shell.
        _proxy = connection.CreateProxy<IGnomePointer>("org.gnome.Shell", "/org/mateengine/Pointer");
        var token = _cancel.Token;
        Task.Run(() => PollLoop(token));
    }

    /// <summary>True while the extension is answering.</summary>
    public bool IsLive
    {
        get
        {
            lock (_lock) return IsFresh();
        }
    }

    /// <summary>Latest pointer position and desktop size, in the compositor's coordinates.</summary>
    public bool TryGetPosition(out Vector2Int position, out Vector2Int desktopSize)
    {
        lock (_lock)
        {
            position = _position;
            desktopSize = _desktopSize;
            return IsFresh();
        }
    }

    private bool IsFresh() =>
        _updatedTicks != 0 && (Stopwatch.GetTimestamp() - _updatedTicks) / (double)Stopwatch.Frequency <= StaleSeconds;

    private async Task PollLoop(CancellationToken token)
    {
        bool wasLive = false, hintShown = false;
        while (!token.IsCancellationRequested)
        {
            int delay = PollMilliseconds;
            try
            {
                var (x, y, width, height) = await _proxy.GetAsync().ConfigureAwait(false);
                lock (_lock)
                {
                    _position = new Vector2Int(x, y);
                    _desktopSize = new Vector2Int(width, height);
                    _updatedTicks = Stopwatch.GetTimestamp();
                }
                if (!wasLive)
                {
                    wasLive = true;
                    Debug.Log("GNOME pointer extension found: the cursor is tracked across the whole desktop.");
                }
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested) break;
                wasLive = false;
                if (!hintShown)
                {
                    hintShown = true;
                    Debug.Log("GNOME pointer extension is not answering (" + e.GetType().Name + "): the cursor is only " +
                              "tracked while it is over the avatar. Run gnome-pointer-extension.sh and log in again " +
                              "to let the avatar follow the cursor everywhere.");
                }
                delay = RetryMilliseconds;
            }

            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _cancel.Cancel();
        _cancel.Dispose();
    }
}
