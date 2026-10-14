using System.Runtime.InteropServices;

namespace DesktopLauncher.Services;

/// <summary>
/// Keeps one running instance per user session; a second launch asks the first one to show itself.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const int ASFW_ANY = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly RegisteredWaitHandle _wait;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent)
    {
        _mutex = mutex;
        _showEvent = showEvent;
        _wait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => ShowRequested?.Invoke(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Raised on a thread-pool thread.</summary>
    public event Action? ShowRequested;

    public static SingleInstance? TryAcquire(string name)
    {
        var mutex = new Mutex(true, MutexName(name), out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }
        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, EventName(name)));
    }

    public static void SignalExisting(string name)
    {
        if (!EventWaitHandle.TryOpenExisting(EventName(name), out var showEvent)) return;
        using (showEvent)
        {
            // The running instance is in the background, so let it take the foreground.
            AllowSetForegroundWindow(ASFW_ANY);
            showEvent.Set();
        }
    }

    private static string MutexName(string name) => $@"Local\{name}";

    private static string EventName(string name) => $@"Local\{name}-show";

    public void Dispose()
    {
        _wait.Unregister(null);
        _showEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
