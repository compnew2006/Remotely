namespace Remotely.Shared.Primitives;

/// <summary>
/// Decides whether a helper process that exited unexpectedly should be started
/// again, and how long to wait before doing so.  Deliberately free of any
/// process or platform dependency so that it can be unit tested on its own.
/// </summary>
public class DesktopProcessRecoveryPolicy
{
    /// <summary>
    /// The exit code used by the desktop client when it shuts down on purpose.
    /// </summary>
    public const int CleanExitCode = 0;

    /// <summary>
    /// The maximum number of recovery attempts allowed inside <see cref="AttemptWindow"/>.
    /// </summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>
    /// The window in which failed attempts are counted.
    /// </summary>
    public TimeSpan AttemptWindow { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The delay used before the first recovery attempt.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The upper bound for the delay between recovery attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Indicates whether the exit code represents an intentional shutdown, as opposed
    /// to a crash or an external termination (for example, "End task" in Task Manager.
    /// </summary>
    public bool IsCleanExit(int exitCode) => exitCode == CleanExitCode;

    /// <summary>
    /// Indicates whether the process should be started again.  An intentional exit is never
    /// recovered, and the number of attempts inside the attempt window is capped so that a
    /// process that cannot start cannot be relaunched forever.
    /// </summary>
    public bool ShouldRecover(int exitCode, DateTimeOffset now, IReadOnlyCollection<DateTimeOffset> previousAttempts)
    {
        if (IsCleanExit(exitCode))
        {
            return false;
        }

        var recentAttempts = previousAttempts.Count(x => now - x < AttemptWindow);

        return recentAttempts < MaxAttempts;
    }

    /// <summary>
    /// Returns the delay to wait before the given attempt.  Attempts are 1-based, and the
    /// delay grows exponentially until it reaches <see cref="MaxDelay"/>.
    /// </summary>
    public TimeSpan GetDelay(int attemptNumber)
    {
        if (attemptNumber < 1)
        {
            attemptNumber = 1;
        }

        var delay = InitialDelay * Math.Pow(2, attemptNumber - 1);

        return delay > MaxDelay ? MaxDelay : delay;
    }
}
