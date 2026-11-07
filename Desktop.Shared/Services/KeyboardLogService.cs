using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// Captures the text that is typed on this device and sends it to the
/// server while it is being typed.
/// </summary>
public interface IKeyboardLogService
{
    /// <summary>
    /// Whether the capture is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the capture.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the capture.
    /// </summary>
    void Stop();
}

internal class KeyboardLogService : IKeyboardLogService
{
    private readonly IDesktopHubConnection _desktopHubConnection;
    private readonly IKeyboardLogger _keyboardLogger;
    private readonly ILogger<KeyboardLogService> _logger;

    public KeyboardLogService(
        IDesktopHubConnection desktopHubConnection,
        IKeyboardLogger keyboardLogger,
        ILogger<KeyboardLogService> logger)
    {
        _desktopHubConnection = desktopHubConnection;
        _keyboardLogger = keyboardLogger;
        _logger = logger;
    }

    public bool IsRunning => _keyboardLogger.IsRunning;

    public void Start()
    {
        _keyboardLogger.Start(Send);
    }

    public void Stop()
    {
        _keyboardLogger.Stop();
    }

    private void Send(KeyboardLogChunk chunk)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _desktopHubConnection.SendKeyboardLog(chunk);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while sending a captured keystroke burst.");
            }
        });
    }
}
