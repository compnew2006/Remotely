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
    /// Whether the text of a password field is captured as it is typed.  The
    /// text of a password field is masked when this is false, so it is
    /// never stored.
    /// </summary>
    bool ShowPasswords { get; set; }

    /// <summary>
    /// Starts the capture.  Every burst of text that is typed is passed to
    /// the callback.
    /// </summary>
    /// <param name="onChunk">The callback that receives the captured text.</param>
    /// <param name="showPasswords">
    /// Whether the text of a password field is captured as it is typed.  The
    /// text of a password field is masked when this is false, so it is
    /// never stored.
    /// </param>
    void Start(Action<KeyboardLogChunk> onChunk, bool showPasswords = false);

    /// <summary>
    /// Stops the capture.
    /// </summary>
    void Stop();
}
