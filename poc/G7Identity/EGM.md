# G7 Identity PoC

## Overview

Standalone Windows measurement tool that reproduces the existing interactive-process launch chain and records token, session, desktop, and launch facts.

## Interface

- Program.Main selects launch or probe mode and accepts --out for the report path [poc/G7Identity/Program.cs:Main].
- Program.Launch searches for winlogon, duplicates its primary token, resolves the desktop name, creates a probe process, and gathers its report [poc/G7Identity/Program.cs:Launch].
- Program.Probe records token identity, session ID, integrity level, desktop reachability, parent PID, command line, and whoami output [poc/G7Identity/Program.cs:Probe].
- Native declares the Win32 token, process, session, desktop, and Toolhelp bindings used by the probe [poc/G7Identity/Native.cs:Native].

## Invariants

- The launch path follows OpenProcessToken, DuplicateTokenEx(TokenPrimary), and CreateProcessAsUser; its desktop is winsta0\Winlogon when LogonUI or consent is detected, otherwise winsta0\Default [poc/G7Identity/Program.cs:Launch/ResolveDesktopNameLikeProduction; poc/G7Identity/Native.cs:DuplicateTokenEx/CreateProcessAsUser].
- G7Identity targets net8.0-windows, is outside Remotely.sln, and enables RollForward Major [poc/G7Identity/G7Identity.csproj:TargetFramework/RollForward].
- This source measures the winlogon-token path; it is not evidence that the separate AD-15 employee-token decision has been implemented [poc/G7Identity/Program.cs:Launch; DECISION-LOG.md:AD-15; GATE-REGISTER.md:G7].

## Side Effects

- Program.Launch may enable SeDebugPrivilege, opens handles to winlogon and its token, and starts a child process with CreateProcessAsUser [poc/G7Identity/Program.cs:TryEnableDebugPrivilege/Launch; poc/G7Identity/Native.cs:OpenProcess/OpenProcessToken/DuplicateTokenEx/CreateProcessAsUser].
- Program.Probe queries token, desktop, session, and process information and runs whoami.exe [poc/G7Identity/Program.cs:Probe/RunCaptured].
- Program.Write creates the report directory and writes JSON to the requested output path; Launch also writes a child report with a .child.json suffix [poc/G7Identity/Program.cs:Write/Launch].

## Verification

Build only; this PoC was not run for this documentation task:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build poc/G7Identity/G7Identity.csproj
```
