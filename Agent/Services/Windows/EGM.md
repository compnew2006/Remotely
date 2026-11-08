# Windows Agent Services

## Overview

Windows-specific desktop launching, inventory, update verification, and process supervision.

## Interface

- AppLauncherWin implements IAppLauncher and tracks each launched desktop process [Agent/Services/Windows/AppLauncherWin.cs:AppLauncherWin.LaunchDesktopProcess].
- DeviceInfoGeneratorWin builds Windows inventory from Win32 session and memory APIs [Agent/Services/Windows/DeviceInfoGeneratorWin.cs:DeviceInfoGeneratorWin.CreateDevice].
- UpdaterWin implements IUpdater and verifies a downloaded update before launching its installer [Agent/Services/Windows/UpdaterWin.cs:UpdaterWin.VerifyUpdatePackage].
- DesktopProcessSupervisor implements IDesktopProcessSupervisor and IHostedService [Agent/Services/Windows/DesktopProcessSupervisor.cs:DesktopProcessSupervisor].

## Invariants

- DesktopProcessSupervisor does not relaunch a process after a clean exit or while its update-suppression flag is current [Agent/Services/Windows/DesktopProcessSupervisor.cs:DesktopProcessSupervisor.CheckTrackedProcesses].
- Restored process records are ignored when the process is absent or its start time does not match the saved process identity [Agent/Services/Windows/DesktopProcessSupervisor.cs:DesktopProcessSupervisor.RestoreState].
- UpdaterWin returns without installation when VerifyUpdatePackage fails [Agent/Services/Windows/UpdaterWin.cs:UpdaterWin.InstallLatestVersion].

## Side Effects

- AppLauncherWin starts the desktop client, using Win32Interop.CreateInteractiveSystemProcess when the agent is SYSTEM, then registers it with the supervisor [Agent/Services/Windows/AppLauncherWin.cs:AppLauncherWin.LaunchDesktopProcess].
- DesktopProcessSupervisor reads and writes DesktopProcesses.json beside the agent and may restart tracked client processes [Agent/Services/Windows/DesktopProcessSupervisor.cs:DesktopProcessSupervisor.SaveState].
- UpdaterWin downloads package, installer, manifest, and signature files to a randomized temporary directory before invoking PowerShell [Agent/Services/Windows/UpdaterWin.cs:UpdaterWin.InstallLatestVersion].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
