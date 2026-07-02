namespace NanoVault.Core.Threading;

/// <summary>
/// Cooperative pause support for long-running operations, mirroring the
/// CancellationTokenSource / CancellationToken pattern.
/// </summary>
public sealed class PauseTokenSource
{
    private readonly object _gate = new();
    private TaskCompletionSource<bool> _resumeSignal = CreateCompleted();

    private static TaskCompletionSource<bool> CreateCompleted()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult(true);
        return tcs;
    }

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return !_resumeSignal.Task.IsCompleted;
            }
        }
    }

    public PauseToken Token => new(this);

    public void Pause()
    {
        lock (_gate)
        {
            if (_resumeSignal.Task.IsCompleted)
            {
                _resumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            _resumeSignal.TrySetResult(true);
        }
    }

    internal Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (_gate)
        {
            waitTask = _resumeSignal.Task;
        }

        return waitTask.IsCompleted ? Task.CompletedTask : waitTask.WaitAsync(cancellationToken);
    }
}

/// <summary>Passed into operations that must honour a pause request.</summary>
public readonly struct PauseToken
{
    private readonly PauseTokenSource? _source;

    internal PauseToken(PauseTokenSource source) => _source = source;

    public static PauseToken None => default;

    public bool IsPaused => _source?.IsPaused ?? false;

    public Task WaitWhilePausedAsync(CancellationToken cancellationToken = default) =>
        _source?.WaitWhilePausedAsync(cancellationToken) ?? Task.CompletedTask;
}
