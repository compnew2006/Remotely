# Linux Agent Services

## Overview

Linux-specific agent launch, device inventory, and update implementations.

## Interface

- AppLauncherLinux implements the shared app-launch contract [Agent/Services/Linux/AppLauncherLinux.cs:AppLauncherLinux].
- DeviceInfoGeneratorLinux implements device inventory and Linux memory and user queries [Agent/Services/Linux/DeviceInfoGeneratorLinux.cs:DeviceInfoGeneratorLinux.CreateDevice].
- UpdaterLinux implements scheduled package checks and installation [Agent/Services/Linux/UpdaterLinux.cs:UpdaterLinux].

## Invariants

- AppLauncherLinux tries Xorg authentication first and falls back to Xwayland before launching the desktop process [Agent/Services/Linux/AppLauncherLinux.cs:AppLauncherLinux.StartLinuxDesktopApp].
- UpdaterLinux skips update work in debug mode and serializes checks with _checkForUpdatesLock [Agent/Services/Linux/UpdaterLinux.cs:UpdaterLinux.CheckForUpdates].

## Side Effects

- AppLauncherLinux starts desktop processes through sudo and sets DISPLAY and XAUTHORITY [Agent/Services/Linux/AppLauncherLinux.cs:AppLauncherLinux.StartLinuxDesktopApp].
- DeviceInfoGeneratorLinux invokes users and reads /proc/meminfo through IProcessInvoker [Agent/Services/Linux/DeviceInfoGeneratorLinux.cs:DeviceInfoGeneratorLinux.GetCurrentUser].
- UpdaterLinux downloads installer and package files to the temporary directory, then invokes chmod and the installer through sudo [Agent/Services/Linux/UpdaterLinux.cs:UpdaterLinux.InstallLatestVersion].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
