// G7 identity-facts PoC — المرحلة 4 (R-05).
// الوضع `launch`: يعيد إنتاج سلسلة Win32Interop.CreateInteractiveSystemProcess حرفياً
// (فتح winlogon → OpenProcessToken(TOKEN_DUPLICATE) → DuplicateTokenEx(TokenPrimary)
//  → STARTUPINFO.lpDesktop = winsta0\<ResolveDesktopName> → CreateProcessAsUser).
// الوضع `probe`: يعمل بتوكن winlogon المكرّر ويسجّل الوقائع الست:
//   1 توكن العملية · 2 SID المستخدم · 3 معرّف الجلسة · 4 حالة الارتفاع
//   5 إمكانية الوصول لسطح المكتب · 6 آلية الإطلاق.
// بلا أي تصميم pipe ولا تغيير سلوك إنتاجي — أداة قياس فقط، خارج Remotely.sln.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using static G7IdentityPoC.Native;

namespace G7IdentityPoC;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static int Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "probe";
        var outPath = "g7-report.json";
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--out") outPath = args[i + 1];
        }

        return mode == "launch" ? Launch(outPath) : Probe(outPath);
    }

    // ---------- Parent: replicates the production launch chain ----------
    private static int Launch(string outPath)
    {
        var report = new Dictionary<string, object?>
        {
            ["role"] = "parent (launcher) — replicates Win32Interop.CreateInteractiveSystemProcess",
            ["utc"] = DateTime.UtcNow.ToString("O"),
            ["parentExe"] = Environment.ProcessPath,
            ["parentPid"] = Environment.ProcessId,
        };

        // 0. Enable SeDebugPrivilege (production agent runs as a SYSTEM service and already has it)
        report["seDebugPrivilegeEnabled"] = TryEnableDebugPrivilege();

        // 1. ResolveWindowsSession(targetSessionId: -1) — this host is Windows Server ⇒ console session
        var sessionId = WTSGetActiveConsoleSessionId();
        report["resolvedSessionId"] = sessionId;
        report["sessionUsername(WTS)"] = QuerySessionUsername(sessionId) ?? "(none)";

        // 2. Find winlogon PID within the session (same loop as Win32Interop.cs:121-128)
        uint winlogonPid = 0;
        foreach (var p in Process.GetProcessesByName("winlogon"))
        {
            if (p.SessionId == (int)sessionId) winlogonPid = (uint)p.Id;
        }
        report["winlogonPid"] = winlogonPid;
        if (winlogonPid == 0)
        {
            report["fatal"] = "no winlogon found in session";
            Write(report, outPath);
            return 2;
        }

        // 3. OpenProcess(MAXIMUM_ALLOWED) → OpenProcessToken(TOKEN_DUPLICATE) → DuplicateTokenEx(TokenPrimary)
        var hProcess = OpenProcess(MAXIMUM_ALLOWED, false, winlogonPid);
        report["openProcessWinlogonOk"] = hProcess != nint.Zero;
        if (hProcess == nint.Zero)
        {
            report["openProcessWinlogonError"] = Marshal.GetLastWin32Error();
            Write(report, outPath);
            return 3;
        }

        var hPToken = nint.Zero;
        var hDup = nint.Zero;
        try
        {
            var tokenOk = OpenProcessToken(hProcess, TOKEN_DUPLICATE, out hPToken);
            report["openProcessTokenWinlogonOk"] = tokenOk;
            if (!tokenOk)
            {
                report["openProcessTokenError"] = Marshal.GetLastWin32Error();
                Write(report, outPath);
                return 4;
            }

            var sa = new SECURITY_ATTRIBUTES { Length = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)) };
            var dupOk = DuplicateTokenEx(hPToken, MAXIMUM_ALLOWED, ref sa,
                SecurityIdentification, TokenPrimary, out hDup);
            report["duplicateTokenExOk"] = dupOk;
            if (!dupOk)
            {
                report["duplicateTokenExError"] = Marshal.GetLastWin32Error();
                Write(report, outPath);
                return 5;
            }

            // 4. lpDesktop — same string construction as Win32Interop.cs:158
            var desktopName = ResolveDesktopNameLikeProduction(sessionId, out var logonUiSeen, out var consentSeen);
            var si = new STARTUPINFO
            {
                cb = Marshal.SizeOf(typeof(STARTUPINFO)),
                lpDesktop = @"winsta0\" + desktopName,
            };
            report["lpDesktop"] = si.lpDesktop;
            report["resolveDesktopName.logonUiSeen"] = logonUiSeen;
            report["resolveDesktopName.consentSeen"] = consentSeen;

            var childOut = Path.GetFullPath(outPath + ".child.json");
            var exe = Environment.ProcessPath!;
            var commandLine = $"\"{exe}\" probe --out \"{childOut}\"";
            // الخطأ 1314 (ERROR_BAD_PATHNAME) معروف مع CreateProcessAsUser عند تمرير
            // lpCurrentDirectory=null؛ نمرر دليلًا مطلقًا لتعمل الأداة في بيئة القياس.
            var appDir = Path.GetDirectoryName(exe)!;

            var createOk = CreateProcessAsUser(hDup, exe, commandLine, ref sa, ref sa, false,
                NORMAL_PRIORITY_CLASS | CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_CONSOLE,
                nint.Zero, appDir, ref si, out var pi);
            report["launchMechanism"] = "CreateProcessAsUser(duplicated winlogon token, TokenPrimary, CREATE_NEW_CONSOLE)";
            report["createProcessAsUserOk"] = createOk;
            if (!createOk) report["createProcessAsUserError"] = Marshal.GetLastWin32Error();
            report["childPid"] = createOk ? pi.dwProcessId : -1;

            if (createOk)
            {
                WaitForSingleObject(pi.hProcess, 60_000);
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                if (File.Exists(childOut))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(childOut));
                    report["childFacts"] = JsonSerializer.SerializeToElement(doc.RootElement);
                }
                else
                {
                    report["childFatal"] = "child did not produce output file";
                }
            }
        }
        finally
        {
            if (hDup != nint.Zero) CloseHandle(hDup);
            if (hPToken != nint.Zero) CloseHandle(hPToken);
            CloseHandle(hProcess);
        }

        Write(report, outPath);
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOpts));
        return 0;
    }

    private static bool TryEnableDebugPrivilege()
    {
        // TOKEN_ADJUST_PRIVILEGES = 0x0040
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | 0x0040, out var hToken))
            return false;
        try
        {
            if (!LookupPrivilegeValue(null, "SeDebugPrivilege", out var luid)) return false;
            var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
            tp.Privileges.Luid = luid;
            tp.Privileges.Attributes = 0x00000002; // SE_PRIVILEGE_ENABLED
            Marshal.SetLastPInvokeError(0);
            AdjustTokenPrivileges(hToken, false, ref tp, 0, nint.Zero, nint.Zero);
            var err = Marshal.GetLastWin32Error();
            return err == 0 || err == 1300; // ERROR_NOT_ALL_ASSIGNED ⇒ قد تكون الامتيازات مفعّلة أصلًا
        }
        finally { CloseHandle(hToken); }
    }

    // Mirrors Win32Interop.ResolveDesktopName (:195-215): "Winlogon" if LogonUI/consent
    // is running in the target session, else "Default".
    private static string ResolveDesktopNameLikeProduction(uint sessionId, out bool logonUiSeen, out bool consentSeen)
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var logonUiPath = Path.Combine(winDir, "System32", "LogonUI.exe");
        var consentPath = Path.Combine(winDir, "System32", "consent.exe");

        bool Match(string name, string expected)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                if (p.SessionId != (int)sessionId) continue;
                try
                {
                    if (string.Equals(p.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // MainModule unreadable — process is present but path unverified
                    return true;
                }
            }
            return false;
        }

        logonUiSeen = Match("LogonUI", logonUiPath);
        consentSeen = Match("consent", consentPath);
        return logonUiSeen || consentSeen ? "Winlogon" : "Default";
    }

    // ---------- Child: runs under the duplicated winlogon token; records the six facts ----------
    private static int Probe(string outPath)
    {
        var facts = new Dictionary<string, object?>
        {
            ["role"] = "child probe — token = duplicated winlogon primary token",
            ["utc"] = DateTime.UtcNow.ToString("O"),
            ["pid"] = Environment.ProcessId,
        };

        // Fact 1: process token + Fact 2: user SID
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value;
            facts["1.token.accountName"] = identity.Name;
            facts["1.token.isLocalSystemSid"] = string.Equals(identity.User?.Value, systemSid, StringComparison.OrdinalIgnoreCase);
            facts["1.token.authenticationType"] = identity.AuthenticationType;
            facts["2.userSid"] = identity.User?.Value;

            var groups = new List<string>();
            if (identity.Groups != null)
            {
                foreach (var g in identity.Groups) groups.Add(g.Value);
            }
            facts["2.groupSids"] = groups;

            // Fact 4: mandatory level — WindowsIdentity.Groups لا يتضمن تسمية التكامل،
            // تُقاس من TokenIntegrityLevel (class 25) أدناه.
            facts["4.mandatoryLevel"] = "(see 4.integrityLevelSid)";
        }

        // Fact 4: elevation + elevation type via GetTokenInformation
        if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out var hToken))
        {
            try
            {
                var small = Marshal.AllocHGlobal(4);
                try
                {
                    // TOKEN_TYPE (class 8): 1=Primary 2=Impersonation — واقعة #1
                    if (GetTokenInformation(hToken, 8, small, 4, out _))
                        facts["1.token.type"] = Marshal.ReadInt32(small) == 1 ? "TokenPrimary" : "TokenImpersonation";
                    // TOKEN_IMPERSONATION_LEVEL (class 9) — واقعة #1
                    if (GetTokenInformation(hToken, 9, small, 4, out _))
                        facts["1.token.impersonationLevel"] = Marshal.ReadInt32(small) switch
                        {
                            0 => "SecurityAnonymous",
                            1 => "SecurityIdentification",
                            2 => "SecurityImpersonation",
                            3 => "SecurityDelegation",
                            _ => "unknown",
                        };
                }
                finally { Marshal.FreeHGlobal(small); }

                // TOKEN_ELEVATION (class 20): DWORD TokenIsElevated
                var buffer = Marshal.AllocHGlobal(4);
                try
                {
                    if (GetTokenInformation(hToken, 20, buffer, 4, out _))
                        facts["4.tokenElevation.isElevated"] = Marshal.ReadInt32(buffer) != 0;
                    // TOKEN_ELEVATION_TYPE (class 18): 1=Default 2=Enhanced 3=Limited
                    if (GetTokenInformation(hToken, 18, buffer, 4, out _))
                    {
                        facts["4.tokenElevationType.raw"] = Marshal.ReadInt32(buffer);
                        facts["4.tokenElevationType"] = Marshal.ReadInt32(buffer) switch
                        {
                            1 => "TokenElevationTypeDefault (UAC off or non-admin)",
                            2 => "TokenElevationTypeFull (elevated)",
                            3 => "TokenElevationTypeLimited (filtered UAC token)",
                            _ => "unknown",
                        };
                    }
                }
                finally { Marshal.FreeHGlobal(buffer); }

                // TOKEN_INTEGRITY_LEVEL (class 25): TOKEN_MANDATORY_LABEL { SID_AND_ATTRIBUTES Label }
                var labelPtr = Marshal.AllocHGlobal(16);
                try
                {
                    if (GetTokenInformation(hToken, 25, labelPtr, 16, out _))
                    {
                        var sidPtr = Marshal.ReadIntPtr(labelPtr); // حقل SID في بداية Struct
                        var label = SidToString(sidPtr);
                        facts["4.integrityLevelSid"] = label switch
                        {
                            "S-1-16-4096" => label + " (System)",
                            "S-1-16-8192" => label + " (Protected Process Light)",
                            "S-1-16-12288" => label + " (High)",
                            "S-1-16-16384" => label + " (Protected Process)",
                            _ => label,
                        };
                        facts["4.mandatoryLevel"] = facts["4.integrityLevelSid"];
                    }
                }
                finally { Marshal.FreeHGlobal(labelPtr); }
            }
            finally { CloseHandle(hToken); }
        }

        // Fact 3: session id
        facts["3.sessionId"] = Process.GetCurrentProcess().SessionId;
        facts["3.activeConsoleSessionId"] = WTSGetActiveConsoleSessionId();
        facts["3.sessionUsername(WTS)"] = QuerySessionUsername((uint)Process.GetCurrentProcess().SessionId) ?? "(none)";

        // Fact 5: desktop reachability
        var winstation = GetProcessWindowStation();
        facts["5.windowStation"] = winstation == nint.Zero ? "(none)" : GetUoiName(winstation);
        var threadDesktop = GetThreadDesktop(GetCurrentThreadId());
        facts["5.threadDesktop"] = threadDesktop == nint.Zero ? "(none)" : GetUoiName(threadDesktop);
        var inputDesktop = OpenInputDesktop(0, false, DESKTOP_READOBJECTS);
        if (inputDesktop != nint.Zero)
        {
            facts["5.inputDesktop"] = GetUoiName(inputDesktop);
            CloseDesktop(inputDesktop);
        }
        else
        {
            facts["5.inputDesktop"] = "(OpenInputDesktop failed: " + Marshal.GetLastWin32Error() + ")";
        }

        // Fact 6: launch mechanism as seen from the child
        facts["6.commandLine"] = Environment.CommandLine;
        facts["6.parentPid"] = GetParentProcessId();
        facts["6.workingDirectory"] = Environment.CurrentDirectory;
        facts["6.userInteractive"] = Environment.UserInteractive;

        // Cross-check: whoami /all under the same token (production-grade evidence style)
        try
        {
            facts["crossCheck.whoami_all"] = RunCaptured("whoami.exe", "/all");
        }
        catch (Exception ex)
        {
            facts["crossCheck.whoami_all.error"] = ex.Message;
        }

        Write(facts, outPath);
        return 0;
    }

    private static string RunCaptured(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        sb.Append(p.StandardOutput.ReadToEnd());
        sb.Append(p.StandardError.ReadToEnd());
        p.WaitForExit(15_000);
        return sb.ToString();
    }

    private static int GetParentProcessId()
    {
        var snap = CreateToolhelp32Snapshot(2 /*TH32CS_SNAPPROCESS*/, 0);
        if (snap == nint.Zero || snap == -1) return -1;
        try
        {
            var entry = new PROCESSENTRY32 { dwSize = Marshal.SizeOf(typeof(PROCESSENTRY32)) };
            if (!Process32First(snap, ref entry)) return -1;
            do
            {
                if (entry.th32ProcessID == Environment.ProcessId) return entry.th32ParentProcessID;
            }
            while (Process32Next(snap, ref entry));
        }
        finally { CloseHandle(snap); }
        return -1;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(nint Desktop);

    private static void Write(Dictionary<string, object?> data, string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }
}
