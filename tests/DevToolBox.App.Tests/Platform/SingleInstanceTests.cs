using DevToolBox.App.Platform;

namespace DevToolBox.App.Tests.Platform;

public sealed class SingleInstanceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "devtoolbox-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void SecondAcquire_FailsWhileTheFirstHoldsTheLock_AndSucceedsAfterRelease()
    {
        var first = SingleInstance.TryAcquire(_directory);
        Assert.NotNull(first);

        Assert.Null(SingleInstance.TryAcquire(_directory));

        first.Dispose();
        using var again = SingleInstance.TryAcquire(_directory);
        Assert.NotNull(again);
    }

    [Fact]
    public async Task Signal_RaisesActivationRequestedInTheRunningInstance()
    {
        using var instance = SingleInstance.TryAcquire(_directory)!;
        var activated = new TaskCompletionSource();
        instance.ActivationRequested += (_, _) => activated.TrySetResult();
        instance.ListenForActivation();

        SingleInstance.SignalRunningInstance(_directory);

        var finished = await Task.WhenAny(activated.Task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(activated.Task, finished);
    }
}
