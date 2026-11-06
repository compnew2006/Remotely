using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;

namespace Remotely.Shared.Services;
public static class FileLoggerDefaults
{
    private static readonly SemaphoreSlim _logLock = new(1, 1);

    public static string LogsFolderPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var logsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Remotely",
                    "Logs");

                if (EnvironmentHelper.IsDebug)
                {
                    logsPath += "_Debug";
                }
                return logsPath;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                if (EnvironmentHelper.IsDebug)
                {
                    return "/var/log/remotely_debug";
                }
                return "/var/log/remotely";
            }

            throw new PlatformNotSupportedException();
        }
    }

    public static async Task<IDisposable> AcquireLock(CancellationToken cancellationToken = default)
    {
        await _logLock.WaitAsync(cancellationToken);
        return new CallbackDisposable(() => _logLock.Release());
    }

    /// <summary>
    /// Acquires the log lock synchronously.  Logging has to be synchronous,
    /// otherwise entries are lost when a process logs something and then
    /// exits immediately (the write is queued on the thread pool and the
    /// process is gone before it runs).
    /// </summary>
    public static IDisposable AcquireLock()
    {
        _logLock.Wait();
        return new CallbackDisposable(() => _logLock.Release());
    }

    public static string GetLogPath(string componentName) => 
        Path.Combine(LogsFolderPath, componentName, $"LogFile_{DateTime.Now:yyyy-MM-dd}.log");

}
