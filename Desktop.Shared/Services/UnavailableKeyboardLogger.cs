using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;

namespace Remotely.Desktop.Shared.Services;

/// <summary>
/// The keyboard capture is not available in this build, so nothing is
/// captured.
/// </summary>
internal sealed class UnavailableKeyboardLogger : IKeyboardLogger
{
    public bool IsRunning => false;

    public bool ShowPasswords { get; set; }

    public void Start(Action<KeyboardLogChunk> onChunk, bool showPasswords = false)
    {
        // Nothing can be captured.
    }

    public void Stop()
    {
        // Nothing has to be stopped.
    }
}
