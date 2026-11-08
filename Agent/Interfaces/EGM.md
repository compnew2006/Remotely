# Agent Interfaces

## Overview

Contracts for agent launch, device inventory, scripting, updates, and desktop-process supervision.

## Interface

- IAppLauncher declares chat, remote-control, and screen-caster launch operations [Agent/Interfaces/IAppLauncher.cs:IAppLauncher].
- IDesktopProcessSupervisor declares Track and Untrack for a launch record, session, and role [Agent/Interfaces/IDesktopProcessSupervisor.cs:IDesktopProcessSupervisor].
- IDeviceInformationService.CreateDevice returns a DeviceClientDto [Agent/Interfaces/IDeviceInformationService.cs:IDeviceInformationService].
- IScriptingShell.IsDisposed exposes shell lifecycle state [Agent/Interfaces/IScriptingShell.cs:IScriptingShell].
- IUpdater declares BeginChecking, CheckForUpdates, and InstallLatestVersion [Agent/Interfaces/IUpdater.cs:IUpdater].

## Invariants

- Supervisor tracking is addressed by target session and role when removing a process [Agent/Interfaces/IDesktopProcessSupervisor.cs:IDesktopProcessSupervisor.Untrack].
- The interfaces specify operation shapes; platform implementations supply runtime behavior [Agent/Interfaces/IAppLauncher.cs:IAppLauncher].

## Side Effects

- The contracts themselves do not perform operations; effects belong to their implementations [Agent/Interfaces/IUpdater.cs:IUpdater].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
