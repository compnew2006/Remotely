# macOS Agent Services

## Overview

macOS-specific agent launch, device inventory, and update implementations.

## Interface

- AppLauncherMac implements IAppLauncher [Agent/Services/MacOS/AppLauncherMac.cs:AppLauncherMac].
- DeviceInfoGeneratorMac builds device inventory using DeviceInfoGeneratorBase [Agent/Services/MacOS/DeviceInfoGeneratorMac.cs:DeviceInfoGeneratorMac.CreateDevice].
- UpdaterMac implements IUpdater [Agent/Services/MacOS/UpdaterMac.cs:UpdaterMac].

## Invariants

- AppLauncherMac reports the launch methods as under development rather than starting a desktop process [Agent/Services/MacOS/AppLauncherMac.cs:AppLauncherMac.LaunchRemoteControl].
- UpdaterMac skips checks in debug mode and prevents overlapping checks and installs with separate semaphores [Agent/Services/MacOS/UpdaterMac.cs:UpdaterMac.CheckForUpdates].

## Side Effects

- DeviceInfoGeneratorMac invokes zsh commands to read memory, CPU, and logged-in user information [Agent/Services/MacOS/DeviceInfoGeneratorMac.cs:DeviceInfoGeneratorMac.GetMemoryInGB].
- UpdaterMac downloads package files to the temporary directory and launches chmod and the installer through sudo [Agent/Services/MacOS/UpdaterMac.cs:UpdaterMac.InstallLatestVersion].
- AppLauncherMac sends unsupported-feature messages through the SignalR hub [Agent/Services/MacOS/AppLauncherMac.cs:AppLauncherMac.LaunchChatService].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
