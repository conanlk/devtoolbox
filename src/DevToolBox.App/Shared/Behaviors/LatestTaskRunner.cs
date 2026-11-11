namespace DevToolBox.App.Shared.Behaviors;

/// <summary>
/// Runs work off the UI thread, keeping only the latest request: starting a new run cancels the
/// previous one, and results of superseded runs are dropped. Completion callbacks run on the
/// caller's synchronization context (the UI thread in the app).
/// </summary>
public sealed class LatestTaskRunner : IDisposable
{
    private CancellationTokenSource? _current;

    public Task RunAsync<T>(Func<CancellationToken, T> work, Action<T> onCompleted, Action<Exception>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return RunAsync(token => Task.FromResult(work(token)), onCompleted, onFailed);
    }

    public async Task RunAsync<T>(Func<CancellationToken, Task<T>> work, Action<T> onCompleted, Action<Exception>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(onCompleted);
        var cts = Restart();
        var token = cts.Token;
        try
        {
            var result = await Task.Run(() => work(token), token).ConfigureAwait(true);
            if (!cts.IsCancellationRequested)
            {
                onCompleted(result);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Superseded or cancelled: nothing to report.
        }
        catch (Exception ex) when (onFailed is not null && !cts.IsCancellationRequested)
        {
            onFailed(ex);
        }
    }

    public void Cancel() => _current?.Cancel();

    public void Dispose() => _current?.Cancel();

    // Superseded sources are cancelled, not disposed: a run that is still finishing may read them.
    private CancellationTokenSource Restart()
    {
        _current?.Cancel();
        _current = new CancellationTokenSource();
        return _current;
    }
}
