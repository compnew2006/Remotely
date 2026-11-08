# Windows Native Bindings

## Overview

Windows P/Invoke declarations and interop helpers for process tokens, desktops, sessions, input, display capture, and system information.

## Interface

- ADVAPI32 and SECUR32 declare token, process, and LSA operations [Desktop.Native/Windows/ADVAPI32.cs:ADVAPI32.DuplicateTokenEx], [Desktop.Native/Windows/SECUR32.cs:SECUR32.LsaLogonUser].
- User32, GDI32, and Kernel32 expose UI, input, desktop, graphics, process, and memory APIs [Desktop.Native/Windows/User32.cs:User32.SendInput], [Desktop.Native/Windows/GDI32.cs:GDI32.BitBlt], [Desktop.Native/Windows/Kernel32.cs:Kernel32.GlobalMemoryStatusEx].
- WTSAPI32 and Win32Interop expose Windows session discovery and higher-level session/process helpers [Desktop.Native/Windows/WTSAPI32.cs:WTSAPI32.WTSEnumerateSessions], [Desktop.Native/Windows/Win32Interop.cs:Win32Interop.GetActiveSessions].
- Shlwapi.IsOS provides the OS classification used by callers [Desktop.Native/Windows/Shlwapi.cs:Shlwapi.IsOS].

## Invariants

- CreateInteractiveSystemProcess resolves the target session, duplicates a winlogon token as a primary token, and selects winsta0 plus the resolved desktop [Desktop.Native/Windows/Win32Interop.cs:Win32Interop.CreateInteractiveSystemProcess].
- GetActiveSessions includes the active console session and adds active non-console WTS sessions [Desktop.Native/Windows/Win32Interop.cs:Win32Interop.GetActiveSessions].
- Native structs and enums define the layouts and values consumed by these P/Invoke declarations [Desktop.Native/Windows/ADVAPI32.cs:ADVAPI32.PROCESS_INFORMATION], [Desktop.Native/Windows/User32.cs:User32.INPUT].

## Side Effects

- CreateInteractiveSystemProcess may create a process in the resolved interactive Windows session [Desktop.Native/Windows/Win32Interop.cs:Win32Interop.CreateInteractiveSystemProcess].
- User32 and GDI32 calls can inject input, switch desktop state, access clipboard data, and capture pixels [Desktop.Native/Windows/User32.cs:User32.SendInput], [Desktop.Native/Windows/GDI32.cs:GDI32.BitBlt].
- WTSAPI32 and token APIs query session and security-token state [Desktop.Native/Windows/WTSAPI32.cs:WTSAPI32.WTSQuerySessionInformation], [Desktop.Native/Windows/ADVAPI32.cs:ADVAPI32.GetTokenInformation].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
