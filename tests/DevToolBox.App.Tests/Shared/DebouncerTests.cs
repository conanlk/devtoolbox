using DevToolBox.App.Shared.Behaviors;
using DevToolBox.App.Tests.TestDoubles;

namespace DevToolBox.App.Tests.Shared;

public class DebouncerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void Run_ExecutesOnlyTheLastAction_AfterDelay()
    {
        var time = new ManualTimeProvider();
        using var debouncer = new Debouncer(Delay, time);
        var calls = new List<int>();

        debouncer.Run(() => calls.Add(1));
        time.Advance(TimeSpan.FromMilliseconds(50));
        debouncer.Run(() => calls.Add(2));
        time.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Empty(calls);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal([2], calls);
    }

    [Fact]
    public void Dispose_CancelsPendingAction()
    {
        var time = new ManualTimeProvider();
        var debouncer = new Debouncer(Delay, time);
        var called = false;

        debouncer.Run(() => called = true);
        debouncer.Dispose();
        time.Advance(Delay);

        Assert.False(called);
    }
}
