using Remotely.Shared.Models;

namespace Remotely.Desktop.Shared.Abstractions;

/// <summary>
/// Captures the text that is typed on this device, so it can be sent to the
/// server while it is being typed.
/// </summary>
public interface IKeyboardLogger
{
    /// <summary>
    /// Whether the capture is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the capture.  Every burst of text that is typed is passed to
    /// the callback.
    /// </summary>
    void Start(Action<KeyboardLogChunk> onChunk);

    /// <summary>
    /// Stops the capture.
    /// </summary>
    void Stop();
}
