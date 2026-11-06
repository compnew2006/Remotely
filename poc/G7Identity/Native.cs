// PoC native bindings — replicates the exact P/Invoke chain used by
// Desktop.Native/Windows/Win32Interop.cs CreateInteractiveSystemProcess (:105-193)
// plus the extra queries needed to RECORD the six G7 identity facts.
// This file is PoC-only and is not part of Remotely.sln.
using System.Runtime.InteropServices;
using System.Text;

namespace G7IdentityPoC;

internal static class Native
{
    public const uint MAXIMUM_ALLOWED = 0x02000000;
    public const uint TOKEN_QUERY = 0x0008;
    public const uint TOKEN_DUPLICATE = 0x0002;
    public const uint CREATE_NEW_CONSOLE = 0x00000010;
    public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    public const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    public const uint DESKTOP_READOBJECTS = 0x0001;
    public const int SecurityIdentification = 1;
    public const int TokenPrimary = 1;
    public const int UOI_NAME = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES
    {
        public int Length;
        public nint lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
        public int dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(nint ProcessToken, uint DesiredAccess, out nint TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool DuplicateTokenEx(nint ExistingToken, uint DesiredAccess,
        ref SECURITY_ATTRIBUTES TokenAttributes, int ImpersonationLevel, int TokenType, out nint NewToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetTokenInformation(nint TokenHandle, int InfoClass, nint Buffer,
        uint Length, out uint ReturnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool LookupPrivilegeValue(string? SystemName, string Name, out LUID Luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool AdjustTokenPrivileges(nint TokenHandle, bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState, int BufferLength, nint PreviousState, nint ReturnLength);

    [DllImport("kernel32.dll")]
    public static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(uint DesiredAccess, bool InheritHandles, uint ProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(nint Handle);

    [DllImport("kernel32.dll")]
    public static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int WaitForSingleObject(nint Handle, uint Milliseconds);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessAsUser(nint Token, string? ApplicationName, string? CommandLine,
        ref SECURITY_ATTRIBUTES ProcessAttributes, ref SECURITY_ATTRIBUTES ThreadAttributes,
        bool InheritHandles, uint CreationFlags, nint Environment, string? CurrentDirectory,
        ref STARTUPINFO StartupInfo, out PROCESS_INFORMATION ProcessInformation);

    [DllImport("kernel32.dll")]
    public static extern int GetCurrentThreadId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSQuerySessionInformation(nint hServer, uint SessionId,
        int InfoClass, out nint Buffer, out int StrLen); // 5 = WTSUserName

    [DllImport("wtsapi32.dll")]
    public static extern void WTSFreeMemory(nint Memory);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint OpenInputDesktop(uint Flags, bool Inherit, uint Access);

    [DllImport("user32.dll")]
    public static extern nint GetProcessWindowStation();

    [DllImport("user32.dll")]
    public static extern nint GetThreadDesktop(int ThreadId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool GetUserObjectInformation(nint hObject, int Index, nint Info,
        uint Length, out uint LengthNeeded);

    // Toolhelp — لالتقاط الأب الفعلي للعملية المسبار (واقعة #6)
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint CreateToolhelp32Snapshot(uint Flags, uint ProcessId);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PROCESSENTRY32
    {
        public int dwSize;
        public int cntUsage;
        public int th32ProcessID;
        public nuint th32DefaultHeapID;
        public int th32ModuleID;
        public int cntThreads;
        public int th32ParentProcessID;
        public int pcPriClassBase;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Process32First(nint Snapshot, ref PROCESSENTRY32 Entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Process32Next(nint Snapshot, ref PROCESSENTRY32 Entry);

    public static string GetUoiName(nint handle)
    {
        GetUserObjectInformation(handle, UOI_NAME, nint.Zero, 0, out var needed);
        if (needed == 0) return "(unknown)";
        // UOI_NAME Unicode: قد يكون needed فردي البايتات — نجهّز +2 أمانًا
        var buf = Marshal.AllocHGlobal((int)needed + 2);
        try
        {
            if (GetUserObjectInformation(handle, UOI_NAME, buf, needed + 2, out _))
            {
                return Marshal.PtrToStringUni(buf) ?? "(null)";
            }
            return "(error " + Marshal.GetLastWin32Error() + ")";
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool ConvertSidToStringSid(nint Sid, out nint StringSid);

    [DllImport("kernel32.dll")]
    public static extern nint LocalFree(nint Handle);

    public static string SidToString(nint sid)
    {
        if (sid == nint.Zero) return "(null)";
        if (ConvertSidToStringSid(sid, out var strPtr))
        {
            var s = Marshal.PtrToStringUni(strPtr) ?? "(null)";
            LocalFree(strPtr); // ConvertSidToStringSid توثّق ذاكرتها بـLocalFree
            return s;
        }
        return "(error " + Marshal.GetLastWin32Error() + ")";
    }

    public static string? QuerySessionUsername(uint sessionId)
    {
        if (WTSQuerySessionInformation(nint.Zero, sessionId, 5, out var buffer, out var len) && len > 1)
        {
            var name = Marshal.PtrToStringAnsi(buffer);
            WTSFreeMemory(buffer);
            return name;
        }
        return null;
    }
}
