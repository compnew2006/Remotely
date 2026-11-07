using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.UI.Services;
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
    private const uint WM_QUIT = 0x0012;
    private const int GWL_STYLE = -16;
    private const int ES_PASSWORD = 0x20;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_SHIFT = 0x10;
    private const int VK_CAPITAL = 0x14;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
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
    private readonly IUiDispatcher _uiDispatcher;
    private readonly StringBuilder _buffer = new();
    private readonly object _sync = new();

    private LowLevelKeyboardProc? _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private Action<KeyboardLogChunk>? _onChunk;
    private Timer? _flushTimer;

    /// <summary>
    /// The thread that installs the hook and pumps the Windows messages.
    /// </summary>
    private Thread? _hookThread;

    /// <summary>
    /// The thread that translates the keys that the hook has seen.
    /// </summary>
    private Thread? _translateThread;

    /// <summary>
    /// The id of the thread of the hook, as Windows knows it.  It is not
    /// the same as the managed id of the thread.
    /// </summary>
    private uint _hookThreadId;

    /// <summary>
    /// Whether the hook could not be installed.
    /// </summary>
    private bool _hookStartFailed;

    /// <summary>
    /// Whether the text of a password field is captured as it is typed.  The
    /// text of a password field is masked when this is false, so it is
    /// never stored.
    /// </summary>
    public bool ShowPasswords
    {
        get => _showPasswords;
        set => _showPasswords = value;
    }

    private bool _showPasswords;

    /// <summary>
    /// The number of events that the hook has received.
    /// </summary>
    private int _hookEventCount;

    /// <summary>
    /// Is set when the hook thread is ready, or when it failed to start.
    /// </summary>
    private readonly ManualResetEventSlim _hookInstalled = new(false);

    /// <summary>
    /// The keys that the hook has seen and that are not translated yet.
    /// </summary>
    private System.Collections.Concurrent.BlockingCollection<CapturedKey> _pendingKeys = new();

    /// <summary>
    /// A key that the hook has seen.
    /// </summary>
    private readonly record struct CapturedKey(int VirtualKeyCode, int ScanCode, bool IsPaste);

    /// <summary>
    /// The number of keys that were translated, for the diagnostics.
    /// </summary>
    private int _diagnosticCount;

    private string _windowTitle = string.Empty;
    private string _processName = string.Empty;
    private IntPtr _foregroundHandle = IntPtr.Zero;
    private bool _isPasswordField;
    private DateTimeOffset _lastCharAt = DateTimeOffset.MinValue;

    public KeyboardLoggerWin(ILogger<KeyboardLoggerWin> logger, IUiDispatcher uiDispatcher)
    {
        _logger = logger;
        _uiDispatcher = uiDispatcher;
    }

    public bool IsRunning => Volatile.Read(ref _hookHandle) != IntPtr.Zero;

    public void Start(Action<KeyboardLogChunk> onChunk, bool showPasswords = false)
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            _onChunk = onChunk;
            _showPasswords = showPasswords;

            // The delegate has to be kept alive, or the hook is removed when
            // it is collected.
            _hookProc = HookProc;

            _hookStartFailed = false;
            _hookInstalled.Reset();

            // The queue of the keys is emptied when the capture is
            // started again.
            _pendingKeys = new System.Collections.Concurrent.BlockingCollection<CapturedKey>();

            // A low level hook is called in the context of the thread that
            // installed it, and the call is made by sending a message to
            // that thread.  The thread therefore has to pump the Windows
            // messages, or the hook is never called.  The hook is installed
            // on its own thread, so it does not depend on the thread that
            // asks for the capture.
            //
            // The threads are started by the thread of the user interface,
            // because that thread uses the keyboard layout of the user.  The
            // new threads inherit that layout with its character table, so
            // the characters that are captured are the ones that are typed.
            _uiDispatcher.Post(() =>
            {
                PrepareKeyboardLayout();

                var thread = new Thread(HookThreadProc)
                {
                    IsBackground = true,
                    Name = "Remotely Keyboard Capture"
                };

                var translateThread = new Thread(TranslateThreadProc)
                {
                    IsBackground = true,
                    Name = "Remotely Keyboard Translate"
                };

                _hookThread = thread;
                _translateThread = translateThread;

                translateThread.Start();
                thread.Start();
            });

            if (!_hookInstalled.Wait(TimeSpan.FromSeconds(5)))
            {
                _logger.LogError("The keyboard hook was not installed in time.");
                return;
            }

            if (_hookStartFailed)
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
        _pendingKeys.CompleteAdding();

        var hookThread = _hookThread;

        if (hookThread is not null)
        {
            _hookThread = null;

            var threadId = _hookThreadId;
            _hookThreadId = 0;

            if (threadId != 0)
            {
                // The quit message ends the message loop of the hook
                // thread, which removes the hook on its way out.
                PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            hookThread.Join(TimeSpan.FromSeconds(5));
        }

        _translateThread?.Join(TimeSpan.FromSeconds(5));
        _translateThread = null;

        // The hook is removed here as well, in case the thread was not
        // able to end its own message loop.
        RemoveHook();

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

    /// <summary>
    /// Installs the hook and pumps the Windows messages of this thread.
    /// The hook is only called while the messages are retrieved here.
    /// </summary>
    private void HookThreadProc()
    {
        _hookThreadId = GetCurrentThreadId();

        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc!, GetModuleHandle(null), 0);

        if (_hookHandle == IntPtr.Zero)
        {
            _hookStartFailed = true;
            _logger.LogError(
                "The keyboard hook could not be installed.  Error: {error}.",
                Marshal.GetLastWin32Error());

            _hookInstalled.Set();
            return;
        }

        _hookInstalled.Set();

        var msg = new MSG();

        while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // The loop is only left when the capture is stopped.
        RemoveHook();
    }

    /// <summary>
    /// Removes the hook, if it is still installed.
    /// </summary>
    private void RemoveHook()
    {
        var handle = Interlocked.Exchange(ref _hookHandle, IntPtr.Zero);

        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            UnhookWindowsHookEx(handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while removing the keyboard hook.");
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var events = Interlocked.Increment(ref _hookEventCount);

        if (events == 1)
        {
            _logger.LogInformation("The keyboard hook received its first event.");
        }
        else if (events % 50 == 0)
        {
            _logger.LogInformation("The keyboard hook received {count} events.", events);
        }

        if (nCode >= 0)
        {
            var message = wParam.ToInt32();

            if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
            {
                try
                {
                    var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

                    var controlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    var altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

                    // The text of a paste is not typed, so the clipboard is
                    // read instead of the key.
                    var isPaste = controlDown && !altDown && data.vkCode == VK_V;

                    // The control and the alt keys modify the next key, so
                    // the character of a shortcut is not the character that
                    // was typed.
                    if (!isPaste && (controlDown || altDown))
                    {
                        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                    }

                    // The key is translated on another thread, because the
                    // hook has to return as fast as possible and the
                    // translation of a key is not allowed while the hook
                    // is running.
                    _pendingKeys.Add(new CapturedKey(data.vkCode, data.scanCode, isPaste));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while capturing a keystroke.");
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>
    /// Translates the keys that the hook has seen and keeps the text.
    /// </summary>
    private void TranslateThreadProc()
    {
        foreach (var key in _pendingKeys.GetConsumingEnumerable())
        {
            try
            {
                var character = key.IsPaste ? ReadClipboardText() : ToUnicode(key.VirtualKeyCode, key.ScanCode);

                if (!string.IsNullOrEmpty(character))
                {
                    AppendText(character);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while capturing a keystroke.");
            }
        }
    }

    private string? ToUnicode(int virtualKeyCode, int scanCode)
    {
        // The layout that the key is typed with is the layout of the focused
        // window's thread, resolved per keystroke. The layout of this thread
        // is the system default (usually US English) and translations were
        // done with it regardless of what the user typed with, so Arabic
        // keys came out as Latin letters.
        var fgWindow = GetForegroundWindow();
        IntPtr hkl = IntPtr.Zero;

        if (fgWindow != IntPtr.Zero)
        {
            var fgThreadId = GetWindowThreadProcessId(fgWindow, out _);
            hkl = GetKeyboardLayout(fgThreadId);
        }

        // The state of the keyboard is read from the system, because the
        // state of the thread that reads it does not hold the keys that are
        // typed in another window.
        var keyboardState = new byte[256];

        SetModifierState(keyboardState, VK_SHIFT);
        SetModifierState(keyboardState, VK_CONTROL);
        SetModifierState(keyboardState, VK_MENU);
        SetModifierState(keyboardState, VK_LWIN);
        SetModifierState(keyboardState, VK_RWIN);

        // The state of the shift keys is kept in their own entries too.
        keyboardState[VK_LSHIFT] = keyboardState[VK_RSHIFT] = keyboardState[VK_SHIFT];

        // The caps lock is a toggle, so its state is in the low bit.
        if ((GetAsyncKeyState(VK_CAPITAL) & 0x0001) != 0)
        {
            keyboardState[VK_CAPITAL] |= 0x01;
        }

            // The characters are translated with the focused window's layout.
            var buffer = new StringBuilder(8);
        var result = ToUnicodeEx(
            (uint)virtualKeyCode,
            (uint)scanCode,
            keyboardState,
            buffer,
            buffer.Capacity,
            0,
            hkl);

        // A negative result is a dead key, and a result of zero means that
        // the key does not produce a character.
        if (result <= 0)
        {
            return null;
        }

        // Only the characters that the function reported are used.  The
        // buffer can hold more than that, and the extra characters are not
        // part of the key that was pressed.
        var text = buffer.ToString(0, result);

        if (Interlocked.Increment(ref _diagnosticCount) <= 10)
        {
            var fgThread = GetWindowThreadProcessId(fgWindow, out _);

            _logger.LogInformation(
                "The key {vk} (scan {scan}) was translated to '{text}' with the layout {layout} (the focused window uses {focused}).",
                virtualKeyCode,
                scanCode,
                text,
                hkl.ToInt64(),
                GetKeyboardLayout(fgThread).ToInt64());
        }

        return text;
    }

    /// <summary>
    /// Uses the keyboard layout of the window that has the focus, because
    /// the text that is typed in that window uses that layout.  The layout
    /// is used by the threads that are started afterwards, because a thread
    /// inherits the layout of the thread that starts it.
    /// </summary>
    private void PrepareKeyboardLayout()
    {
        var foregroundWindow = GetForegroundWindow();

        if (foregroundWindow == IntPtr.Zero)
        {
            return;
        }

        var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var layout = GetKeyboardLayout(threadId);

        if (layout == IntPtr.Zero || layout == GetKeyboardLayout(0))
        {
            return;
        }

        // The locale of the thread has to be the locale of the layout,
        // and the layout is activated after that.  The character table of
        // the layout is chosen with the locale of the thread.
        var localeId = (uint)(layout.ToInt64() & 0xFFFF);

        try
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo((int)localeId);
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The locale {locale} could not be used.", localeId);
        }

        ActivateKeyboardLayout(layout, 0);
    }

    /// <summary>
    /// Copies the state of a modifier key into the keyboard state.
    /// </summary>
    private static void SetModifierState(byte[] keyboardState, int virtualKeyCode)
    {
        if ((GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0)
        {
            keyboardState[virtualKeyCode] |= 0x80;
        }
        else
        {
            keyboardState[virtualKeyCode] &= 0x7F;
        }
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
            isPasswordField && !_showPasswords ? Mask(text) : text,
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
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadLocale(uint localeId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadKeyboardLayout(uint localeId, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern uint GetKeyboardLayoutList(int n, IntPtr[] layouts);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId = 0);

    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint dwFlags);

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        StringBuilder lpChar,
        int cchBuff,
        uint wFlags,
        IntPtr hkl);

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
