namespace DevToolBox.App.Shared.Behaviors;

/// <summary>
/// Runs only the last action requested within <see cref="Delay"/>. The action is posted back to the
/// synchronization context captured when <see cref="Run"/> was called (the UI thread in the app).
/// </summary>
public sealed class Debouncer(TimeSpan delay, TimeProvider? timeProvider = null) : IDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(150);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private ITimer? _timer;
    private int _generation;

    public TimeSpan Delay { get; } = delay;

    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var context = SynchronizationContext.Current;

        lock (_gate)
        {
            var generation = ++_generation;
            _timer?.Dispose();
            _timer = _timeProvider.CreateTimer(
                _ => Dispatch(context, () => RunIfCurrent(generation, action)),
                null,
                Delay,
                Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _generation++;
            _timer?.Dispose();
            _timer = null;
        }
    }

    // A timer that already fired can race with a newer Run; only the latest request may execute.
    private void RunIfCurrent(int generation, Action action)
    {
        if (Volatile.Read(ref _generation) == generation)
        {
            action();
        }
    }

    private static void Dispatch(SynchronizationContext? context, Action action)
    {
        if (context is null)
        {
            action();
        }
        else
        {
            context.Post(_ => action(), null);
        }
    }
}
