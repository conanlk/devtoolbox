namespace DevToolBox.App.Tests.TestDoubles;

/// <summary>Deterministic time for debounce tests: timers fire only when <see cref="Advance"/> is called.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset? start = null) : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private readonly DateTimeOffset _start = start ?? new DateTimeOffset(2026, 10, 15, 7, 30, 0, TimeSpan.Zero);
    private TimeSpan _now;

    public override DateTimeOffset GetUtcNow() => _start + _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, _now + dueTime, period, this);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (var timer in _timers.ToList())
        {
            timer.FireIfDue(_now);
        }
    }

    private void Remove(ManualTimer timer) => _timers.Remove(timer);

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueAt, TimeSpan period, ManualTimeProvider owner) : ITimer
    {
        private bool _disposed;

        public void FireIfDue(TimeSpan now)
        {
            if (_disposed || now < dueAt)
            {
                return;
            }

            if (period > TimeSpan.Zero && period != Timeout.InfiniteTimeSpan)
            {
                dueAt = now + period;
            }
            else
            {
                Dispose();
            }

            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose()
        {
            _disposed = true;
            owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
